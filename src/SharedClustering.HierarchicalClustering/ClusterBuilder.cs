using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedClustering.Core;
using SharedClustering.HierarchicalClustering.Distance;

namespace SharedClustering.HierarchicalClustering
{
    public class ClusterBuilder : IClusterBuilder
    {
        private readonly int _minClusterSize;

        public ClusterBuilder(int minClusterSize)
        {
            _minClusterSize = minClusterSize;
        }

        public async Task<List<ClusterNode>> BuildClustersAsync(IReadOnlyCollection<IClusterableMatch> clusterableMatches, IReadOnlyDictionary<int, float[]> matrix, IDistanceMetric distanceMetric, IProgressData progressData)
        {
            var nodes = await GetLeafNodesAsync(clusterableMatches, matrix, distanceMetric, progressData).ConfigureAwait(false);

            var nodeCount = nodes
                .SelectMany(node => node.NeighborsByDistance.Select(neighbor => neighbor.Node.FirstLeaf.Index))
                .Concat(nodes.Select(node => node.FirstLeaf.Index))
                .Distinct().Count();
            progressData.Reset($"Building clusters for {nodeCount} matches...", nodes.Count - 1);

            await Task.Run(async () =>
            {
                // Collect isolated nodes off to the side as we find them
                var isolatedNodes = new List<Node>();

                while (nodes.Count > 1)
                {
                    // This is a little verbose, but optimized for performance -- O(N) overall.
                    Node secondNode = null;
                    var neighborToCluster = new Neighbor { DistanceSquared = double.MaxValue };
                    foreach (var node in nodes)
                    {
                        if (node.FirstLeaf.NeighborsByDistance.Count > 0 && node.FirstLeaf.NeighborsByDistance.First().DistanceSquared < neighborToCluster.DistanceSquared)
                        {
                            secondNode = node;
                            neighborToCluster = node.FirstLeaf.NeighborsByDistance.First();
                        }
                        if (node.FirstLeaf != node.SecondLeaf && node.SecondLeaf.NeighborsByDistance.Count > 0 && node.SecondLeaf.NeighborsByDistance.First().DistanceSquared < neighborToCluster.DistanceSquared)
                        {
                            secondNode = node;
                            neighborToCluster = node.SecondLeaf.NeighborsByDistance.First();
                        }
                    }

                    var foundNodesToCluster = secondNode != null;

                    ClusterNode clusterNode;
                    if (!foundNodesToCluster)
                    {
                        // Some of the nodes might have no neighbors because they are fully isolated.
                        // In other words, none of the leaf nodes in the cluster has any shared matches outside of the cluster.
                        // This might happen for a very distant cluster with no sharing in closer relatives,
                        // or for example a split between maternal and paternal relatives.
                        var isIsolatedNodes = nodes.ToLookup(node =>
                        {
                            var leafNodeIndexes = node.GetOrderedLeafNodesIndexes();
                            return node.GetOrderedLeafNodes().All(leafNode => leafNodeIndexes.IsSupersetOf(leafNode.Coords.Keys));
                        });
                        var newIsolatedNodes = isIsolatedNodes[true].ToList();
                        if (newIsolatedNodes.Count > 0)
                        {
                            // Segregate the isolated nodes, since there is nothing that will make them un-isolated.
                            isolatedNodes.AddRange(newIsolatedNodes);
                            nodes = isIsolatedNodes[false].ToList();

                            // If there are fewer than 2 nodes remaining after segregating the isolated nodes, we're done.
                            if (nodes.Count <= 1)
                            {
                                break;
                            }
                        }

                        // All of the remaining nodes have at least one shared match in some other cluster.
                        // Make a larger cluster by joining the smallest cluster with the other node that has the greatest overlap with it.
                        var smallestNode = nodes.OrderBy(node => node.NumChildren).First();
                        var smallestNodeLeafNodesCoords = smallestNode.GetOrderedLeafNodes().SelectMany(leafNode => distanceMetric.SignificantCoordinates(leafNode.Coords)).ToHashSet();
                        var otherNode = nodes
                            .Where(node => node != smallestNode)
                            .OrderByDescending(node => node.NumSharedCoords(smallestNode))
                            .ThenBy(node => node.NumChildren)
                            .First();
                        clusterNode = new ClusterNode(otherNode, smallestNode, double.PositiveInfinity, distanceMetric);
                    }
                    else
                    {
                        var firstNode = neighborToCluster.Node;
                        var first = firstNode.GetHighestParent();
                        var second = secondNode.GetHighestParent();
                        clusterNode = new ClusterNode(first, second, neighborToCluster.DistanceSquared, distanceMetric);
                    }

                    var nodesToRemove = GetNodesToRemove(clusterNode);

                    var nodesWithRemovedNeighbors = (await RemoveNodesAsync(nodes, nodesToRemove.ToList())).ToHashSet();

                    nodes.Remove(clusterNode.First);
                    nodes.Remove(clusterNode.Second);

                    // The first and last leaf nodes in the new cluster cannot have each other as neighbors.
                    if (clusterNode.FirstLeaf.NeighborsByDistance.RemoveAll(neighbor => clusterNode.SecondLeaf == neighbor.Node) > 0)
                    {
                        nodesWithRemovedNeighbors.Add(clusterNode.FirstLeaf);
                    }
                    if (clusterNode.SecondLeaf.NeighborsByDistance.RemoveAll(neighbor => clusterNode.FirstLeaf == neighbor.Node) > 0)
                    {
                        nodesWithRemovedNeighbors.Add(clusterNode.SecondLeaf);
                    }

                    await MaybeRecalculateNeighborsAsync(nodes, nodesWithRemovedNeighbors);

                    nodes.Add(clusterNode);

                    progressData.Increment();
                }

                // If any isolated nodes were found, add them to the end in order of decreasing size.
                if (isolatedNodes.Count > 0)
                {
                    var nodesLargestFirst = isolatedNodes.OrderByDescending(n => n.NumChildren).ToList();
                    var node = nodesLargestFirst.First();
                    foreach (var otherNode in nodesLargestFirst.Skip(1))
                    {
                        node = new ClusterNode(node, otherNode, double.PositiveInfinity, distanceMetric);
                    }

                    if (nodes.Count > 0)
                    {
                        node = new ClusterNode(nodes.Last(), node, double.PositiveInfinity, distanceMetric);
                        nodes.Remove(nodes.Last());
                    }
                    nodes.Add(node);
                }
            });

            progressData.Reset("Done");

            return nodes.OfType<ClusterNode>().ToList();
        }

        private async Task<List<Node>> GetLeafNodesAsync(IReadOnlyCollection<IClusterableMatch> clusterableMatches, IReadOnlyDictionary<int, float[]> matrix, IDistanceMetric distanceMetric, IProgressData progressData)
        {
            clusterableMatches = clusterableMatches.Where(match => matrix.ContainsKey(match.Index)).ToList();
            var average = clusterableMatches.Average(match => match.Coords.Count);

            progressData.Reset($"Calculating coordinates for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            // Create raw leaf nodes based on the provided clusterableMatches and matrix. No information about neighbors is available yet.
            var leafNodes = await Task.Run(() =>
            {
                return clusterableMatches
                    .Select(match => new LeafNode(match.Index, matrix[match.Index], distanceMetric))
                    .ToList();
            });

            progressData.Reset($"Finding closest pairwise distances for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            // Populate nearest neighbors for each leaf node.
            await CalculateNeighborsAsync(leafNodes, leafNodes, distanceMetric, progressData);

            var result = leafNodes.ToList<Node>();

            progressData.Reset();
            return result;
        }

        private async Task CalculateNeighborsAsync(List<LeafNode> leafNodesAll, List<LeafNode> leafNodesToRecalculate, IDistanceMetric distanceMetric, IProgressData progressData)
        {
            // Calculating nearest neighbors is nominally an O(N^2) operation.
            // But, the process of DNA analysis usually produces nodes that only have a small number of shared matches.
            // A significant optimization is possible by bucketing the matches according to their shared matches.
            // As a result, it is then possible only to look at the distances between matches that have at least one shared match in common.
            // All other matches with no matches in common at all are considered to have infinite distance and need not be considered explicitly in the list of neighbors.
            var buckets = leafNodesAll
               .SelectMany(leafNode => distanceMetric.SignificantCoordinates(leafNode.Coords).Select(coord => new { Coord = coord, LeafNode = leafNode }))
               .GroupBy(pair => pair.Coord, pair => pair.LeafNode)
               .ToDictionary(g => g.Key, g => g.ToList());

            var significantCoords = leafNodesAll.ToDictionary(leafNode => leafNode.Index, leafNode => distanceMetric.SignificantCoordinates(leafNode.Coords).ToList());

            var calculateNeighborsByDistanceTasks = leafNodesToRecalculate.Select(async leafNode =>
            {
                leafNode.NeighborsByDistance = await Task.Run(() => GetNeighborsByDistance(leafNode, buckets, significantCoords, distanceMetric));
                progressData?.Increment();
            });

            await Task.WhenAll(calculateNeighborsByDistanceTasks);
        }

        private static IEnumerable<LeafNode> GetNodesToRemove(ClusterNode clusterNode)
        {
            // If joining clusters with more than one node, then the interior nodes are no longer available for further clustering.
            if (clusterNode.First.FirstLeaf != clusterNode.First.SecondLeaf)
            {
                yield return clusterNode.First.SecondLeaf;
            }
            if (clusterNode.Second.FirstLeaf != clusterNode.Second.SecondLeaf)
            {
                yield return clusterNode.Second.FirstLeaf;
            }
        }

        private static async Task<IEnumerable<LeafNode>> RemoveNodesAsync(ICollection<Node> nodes, List<LeafNode> nodesToRemove)
        {
            // If at least one node is unavailable for further clustering, then remove those nodes from the lists of neighbors of all other nodes.
            if (nodesToRemove.Count > 0)
            {
                // Find the exposed leaf nodes that might have the the unavailable nodes as potential neighbors.
                var leafNodesWithNeighbors = nodes.Select(node => node.FirstLeaf)
                    .Concat(nodes.Where(node => node.SecondLeaf != node.FirstLeaf).Select(node => node.SecondLeaf))
                    .Where(leafNode => leafNode.NeighborsByDistance?.Count > 0);

                var removeNeighborsTasks = leafNodesWithNeighbors
                .Where(node => nodesToRemove.Any(nodeToRemove => nodeToRemove.Index < node.Index))
                .Select(node => Task.Run(() =>
                {
                    var numNeighborsRemoved = node.NeighborsByDistance.RemoveAll(neighbor => nodesToRemove.Contains(neighbor.Node));
                    return numNeighborsRemoved > 0 ? node : null;
                }));

                var affectedNodes = await Task.WhenAll(removeNeighborsTasks);
                return affectedNodes.Where(node => node != null);
            }
            return Enumerable.Empty<LeafNode>();
        }

        // Most nodes end up clustered with one of their closest neighbors.
        // We still need to calculate all neighbors to determine which are the closest ones,
        // but after that we save only the closest few.
        //
        // This changes the memory usage from O(N^2) to O(N)
        // and also greatly speeds the cluster creation because there are fewer neighbors that need to be removed as clusters are formed.
        //
        // The drawback is that a small fraction of the nodes won't end up clustering with one of their closest neighbors.
        // For those, the neighbors will need to be "refilled" (via MaybeRecalculateNeighborsAsync) when the last neighbor has been removed before the cluster is formed.
        private const int _maxNeighbors = 25;

        private List<Neighbor> GetNeighborsByDistance(LeafNode leafNode, IDictionary<int, List<LeafNode>> buckets, IDictionary<int, List<int>> significantCoords, IDistanceMetric distanceMetric)
        {
            var leafNodeSignificantCoords = distanceMetric.SignificantCoordinates(leafNode.Coords).ToList();
            var neighbors = leafNodeSignificantCoords
                // Get every node with at least one shared match in common
                .SelectMany(coord => buckets.TryGetValue(coord, out var bucket) ? bucket : Enumerable.Empty<LeafNode>())
                // We only need one direction A -> B (not also B -> A) since we're ultimately going to look at the smallest distances.
                .Where(neighborNode => neighborNode.Index < leafNode.Index)
                // Ignore neighbors that have fewer than _minClusterSize coords in common
                .Where(neighborNode => significantCoords[neighborNode.Index].Intersect(leafNodeSignificantCoords).Count() >= _minClusterSize)
                // Make sure that each node is considered only once (might have been in more than one bucket if more than one shared match in common).
                .Distinct()
                .Select(neighborNode => new Neighbor(neighborNode, leafNode))
                .LowestN(neighbor => neighbor.DistanceSquared, _maxNeighbors)
                .ToList();
            return neighbors;
        }

        private List<Neighbor> GetNeighborsByDistance(IEnumerable<LeafNode> leafNodesAll, LeafNode leafNode)
        {
            var neighbors = leafNodesAll
                // We only need one direction A -> B (not also B -> A) since we're ultimately going to look at the smallest distances.
                .Where(neighborNode => neighborNode.Index < leafNode.Index)
                .Select(neighborNode => new Neighbor(neighborNode, leafNode))
                .Where(neighbor => neighbor.DistanceSquared != double.PositiveInfinity)
                .LowestN(neighbor => neighbor.DistanceSquared, _maxNeighbors)
                .ToList();
            return neighbors;
        }

        // Since only _maxNeighbors are precalculated, it may be necessary to refill the list if a node does not cluster with one of its closest neighbors.
        private async Task MaybeRecalculateNeighborsAsync(ICollection<Node> nodes, IEnumerable<LeafNode> nodesWithRemovedNeighbors)
        {
            var nodesWithLastNeighborRemoved = nodesWithRemovedNeighbors.Where(node => node.NeighborsByDistance.Count == 0).ToList();
            if (nodesWithLastNeighborRemoved.Count > 0)
            {
                var recalculateTasks = nodesWithLastNeighborRemoved.Select(leafNode => Task.Run(() =>
                {
                    var highestParent = leafNode.GetHighestParent();
                    var leafNodes = nodes.Select(node => node.FirstLeaf)
                       .Concat(nodes.Where(node => node.SecondLeaf != node.FirstLeaf).Select(node => node.SecondLeaf))
                       .Where(node => node != highestParent.FirstLeaf && node != highestParent.SecondLeaf);
                    leafNode.NeighborsByDistance = GetNeighborsByDistance(leafNodes, leafNode);
                }));
                await Task.WhenAll(recalculateTasks);
            }
        }
    }
}