using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SharedClustering.Core;
using SharedClustering.HierarchicalClustering.CorrelationWriters;
using SharedClustering.HierarchicalClustering.Distance;
using SharedClustering.HierarchicalClustering.MatrixBuilders;
using SharedClustering.HierarchicalClustering.PrimaryClusterFinders;

namespace SharedClustering.HierarchicalClustering
{
    public class HierarchicalClustering
    {
        private readonly int _minClusterSize;
        private readonly Func<List<IClusterableMatch>, IDistanceMetric> _distanceMetricFactory;
        private readonly IMatrixBuilder _matrixBuilder;
        private readonly IPrimaryClusterFinder _primaryClusterFinder;
        private readonly ICorrelationWriter _correlationWriter;
        private readonly ICoreFileUtils _coreFileUtils;
        private readonly IProgressData _progressData;

        public HierarchicalClustering(
            int minClusterSize,
            Func<List<IClusterableMatch>, IDistanceMetric> distanceMetricFactory,
            IMatrixBuilder matrixBuilder,
            IPrimaryClusterFinder primaryClusterFinder,
            ICorrelationWriter correlationWriter,
            ICoreFileUtils coreFileUtils,
            IProgressData progressData)
        {
            _minClusterSize = minClusterSize;
            _distanceMetricFactory = distanceMetricFactory;
            _matrixBuilder = matrixBuilder;
            _primaryClusterFinder = primaryClusterFinder;
            _correlationWriter = correlationWriter;
            _coreFileUtils = coreFileUtils;
            _progressData = progressData;
        }

        public async Task<List<string>> ClusterAsync(
            List<IClusterableMatch> clusterableMatches,
            Dictionary<int, IClusterableMatch> matchesByIndex,
            HashSet<string> testIdsToFilter,
            double lowestClusterableCentimorgans,
            double minCentimorgansToCluster,
            List<Tag> tags,
            string worksheetName)
        {
            var minCentimorgansToClusterTruncated = Math.Max(lowestClusterableCentimorgans, minCentimorgansToCluster);
            var maxIndex = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= minCentimorgansToClusterTruncated).Max(match => match.Index);
            var clusterableMatchesToCorrelate = clusterableMatches.Where(match => match.Index <= maxIndex);
            if (testIdsToFilter.Any())
            {
                clusterableMatchesToCorrelate = clusterableMatchesToCorrelate.Where(match => testIdsToFilter.Contains(match.Match.TestGuid));
            }

            var clusterableMatchesToCorrelateList = clusterableMatchesToCorrelate.ToList();
            if (clusterableMatchesToCorrelateList.Count == 0)
            {
                return new List<string>();
            }

            if (clusterableMatchesToCorrelateList.Count > _correlationWriter.MaxColumns)
            {
                if (!_coreFileUtils.AskYesNo(
                    $"At most {_correlationWriter.MaxColumns} matches can be written to one file.{Environment.NewLine}{Environment.NewLine}" +
                    $"{clusterableMatchesToCorrelateList.Count} matches will be split into several output files.{Environment.NewLine}{Environment.NewLine}" +
                    "Continue anyway?",
                    "Too many matches"))
                {
                    return new List<string>();
                }
            }

            var immediateFamily = clusterableMatchesToCorrelateList.Where(match => match.Match.SharedCentimorgans > 200).ToList();
            if (immediateFamily.Count > clusterableMatchesToCorrelateList.Count / 2)
            {
                immediateFamily.Clear();
            }

            var matrix = await _matrixBuilder.CorrelateAsync(clusterableMatchesToCorrelateList, immediateFamily);

            var nodes = await ClusterAsync(clusterableMatchesToCorrelateList, immediateFamily, matrix, _progressData);

            var primaryClusters = _primaryClusterFinder.GetPrimaryClusters(nodes.FirstOrDefault())
                .Where(cluster => cluster.NumChildren >= _minClusterSize)
                .ToList();

            var indexClusterNumbers = primaryClusters
                .SelectMany((cluster, clusterNum) => cluster.GetOrderedLeafNodes().Select(leafNode => new { LeafNode = leafNode, ClusterNum = clusterNum + 1 }))
                .ToDictionary(pair => pair.LeafNode.Index, pair => pair.ClusterNum);

            if (nodes.Count > 0 && maxIndex < clusterableMatches.Max(match => match.Index))
            {
                var leafNodes = nodes.First().GetOrderedLeafNodes().ToList();

                var primaryClustersSet = new HashSet<Node>(primaryClusters);

                var extendedClusters = await ExtendClustersAsync(clusterableMatches, primaryClustersSet, leafNodes, minCentimorgansToCluster);

                await Recluster(nodes, extendedClusters, immediateFamily, matchesByIndex, matrix);
            }

            return await _correlationWriter.OutputCorrelationAsync(nodes, matchesByIndex, indexClusterNumbers, tags, worksheetName);
        }

        private async Task<List<ClusterNode>> ClusterAsync(IReadOnlyCollection<IClusterableMatch> clusterableMatches, List<IClusterableMatch> immediateFamily, IReadOnlyDictionary<int, float[]> matrix, IProgressData progressData)
        {
            var distanceMetric = _distanceMetricFactory(immediateFamily);

            var matchNodes = await GetLeafNodesAsync(clusterableMatches, matrix, distanceMetric, progressData).ConfigureAwait(false);

            var nodes = await BuildClustersAsync(matchNodes, distanceMetric, progressData).ConfigureAwait(false);

            return nodes;
        }

        private async Task Recluster(ICollection<ClusterNode> nodes, Dictionary<Node, List<IClusterableMatch>> extendedClusters, List<IClusterableMatch> immediateFamily, IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex, ConcurrentDictionary<int, float[]> matrix)
        {
            _progressData.Reset($"Reclustering {extendedClusters.Count} primary clusters", extendedClusters.Count);

            var primaryClustersTaskData = extendedClusters
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    var nodeToRecluster = kvp.Key;
                    var additionalMatches = kvp.Value;
                    var leafNodesByIndex = nodeToRecluster.GetOrderedLeafNodes().ToDictionary(leafNode => leafNode.Index);
                    var clusterableMatches = leafNodesByIndex.Keys.Select(index => matchesByIndex[index]).Concat(additionalMatches).ToList();
                    return new
                    {
                        NodeToRecluster = nodeToRecluster,
                        AdditionalMatches = additionalMatches,
                        LeafNodesByIndex = leafNodesByIndex,
                        ClusterableMatches = clusterableMatches,
                    };
                }).ToList();

            var additionalMatchesDistinct = primaryClustersTaskData.SelectMany(data => data.AdditionalMatches).Distinct().ToList();
            var maxIndex = additionalMatchesDistinct.SelectMany(match => match.Coords).DefaultIfEmpty(-1).Max();
            if (maxIndex < 0)
            {
                _progressData.Reset();
                return;
            }

            _matrixBuilder.ExtendMatrix(matrix, additionalMatchesDistinct, maxIndex);

            var primaryClustersTasks = primaryClustersTaskData
                .Select(async data =>
                {
                    var reclusteredNodes = await ClusterAsync(data.ClusterableMatches, immediateFamily, matrix, new SuppressProgress()).ConfigureAwait(false);

                    if (reclusteredNodes.Count == 0)
                    {
                        _progressData.Increment();
                        return data.NodeToRecluster;
                    }
                    var nodeToReclusterParent = data.NodeToRecluster.Parent;
                    var reclusteredNode = reclusteredNodes.First();
                    foreach (var reclusteredLeafNode in reclusteredNode.GetOrderedLeafNodes())
                    {
                        if (data.LeafNodesByIndex.TryGetValue(reclusteredLeafNode.Index, out var originalLeafNode))
                        {
                            reclusteredLeafNode.Parent.ReplaceChild(reclusteredLeafNode, originalLeafNode);
                        }
                    }
                    if (nodeToReclusterParent != null)
                    {
                        nodeToReclusterParent.ReplaceChild(data.NodeToRecluster, reclusteredNode);
                    }
                    else
                    {
                        if (data.NodeToRecluster is ClusterNode clusterNode)
                        {
                            nodes.Remove(clusterNode);
                        }
                        nodes.Add(reclusteredNode);
                    }
                    _progressData.Increment();
                    return reclusteredNode;
                });
            await Task.WhenAll(primaryClustersTasks);

            _progressData.Reset();
        }

        private async Task<Dictionary<Node, List<IClusterableMatch>>> ExtendClustersAsync(IEnumerable<IClusterableMatch> clusterableMatches, ICollection<Node> primaryClusters, IReadOnlyCollection<LeafNode> leafNodes, double minCentimorgansToCluster)
        {
            var maxClusteredIndex = leafNodes.Max(leafNode => leafNode.Index);
            var otherMatches = clusterableMatches
                .Where(match => match.Index > maxClusteredIndex && match.Match.SharedCentimorgans >= minCentimorgansToCluster && match.Coords.Count >= _minClusterSize).ToList();
            var leafNodesByMatchIndex = leafNodes.ToDictionary(leafNode => leafNode.Index);

            _progressData.Reset($"Extending clusters with {otherMatches.Count} matches...", otherMatches.Count);

            var extendedClusters = await Task.Run(() => otherMatches.Select(match =>
            {
                var parentClusters = match.Coords
                    .Select(coord => leafNodesByMatchIndex.TryGetValue(coord, out var leafNode) ? leafNode : null)
                    .Where(leafNode => leafNode != null)
                    .SelectMany(leafNode => leafNode.GetParents().OfType<Node>().Concat(new[] { leafNode }))
                    .ToList();

                var bestParentCluster = parentClusters
                    .Where(primaryClusters.Contains)
                    .GroupBy(c => c)
                    .Select(g => new { ParentCluster = g.Key, OverlapCount = g.Key.GetOrderedLeafNodesIndexes().Intersect(match.Coords).Count() })
                    .Where(pair => pair.OverlapCount >= _minClusterSize)
                    .OrderByDescending(pair => pair.OverlapCount)
                    .Select(pair => pair.ParentCluster)
                    .FirstOrDefault();

                _progressData.Increment();

                return new { Match = match, BestParentCluster = bestParentCluster };
            })
            .Where(pair => pair.BestParentCluster != null)
            .GroupBy(pair => pair.BestParentCluster, pair => pair.Match)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(match => match.Match.SharedCentimorgans).ToList()));

            _progressData.Reset("Done");

            return extendedClusters;
        }

        private static async Task<List<ClusterNode>> BuildClustersAsync(ICollection<Node> nodes, IDistanceMetric distanceMetric, IProgressData progressData)
        {
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
                        var smallestNodeLeafNodes = new HashSet<int>(smallestNode.GetOrderedLeafNodesIndexes());
                        var otherNode = nodes
                            .Where(node => node != smallestNode)
                            .OrderByDescending(node => smallestNodeLeafNodes.Intersect(node.GetOrderedLeafNodesIndexes()).Count())
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

                    var nodesWithRemovedNeighbors = new HashSet<LeafNode>(await RemoveNodesAsync(nodes, nodesToRemove.ToList()));

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

                    await RecalculateNeighborsAsync(nodes, nodesWithRemovedNeighbors, distanceMetric);

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
            // If at least one node is unavailable for further clustering, then remove those nodes from the lists of neighbors.
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

        private static async Task RecalculateNeighborsAsync(ICollection<Node> nodes, IEnumerable<LeafNode> nodesWithRemovedNeighbors, IDistanceMetric distanceMetric)
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
                    leafNode.NeighborsByDistance = GetNeighborsByDistance(leafNodes, leafNode, distanceMetric);
                }));
                await Task.WhenAll(recalculateTasks);
            }
        }

        private async Task<List<Node>> GetLeafNodesAsync(IReadOnlyCollection<IClusterableMatch> clusterableMatches, IReadOnlyDictionary<int, float[]> matrix, IDistanceMetric distanceMetric, IProgressData progressData)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count);

            progressData.Reset($"Calculating coordinates for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var leafNodes = await Task.Run(() =>
            {
                return clusterableMatches
                    .Where(match => matrix.ContainsKey(match.Index))
                    .Select(match => new LeafNode(match.Index, matrix[match.Index], distanceMetric))
                    .ToList();
            });

            progressData.Reset($"Finding closest pairwise distances for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            await CalculateNeighborsAsync(leafNodes, leafNodes, distanceMetric, progressData);

            var result = leafNodes.ToList<Node>();

            progressData.Reset();
            return result;
        }

        private static async Task CalculateNeighborsAsync(List<LeafNode> leafNodesAll, List<LeafNode> leafNodesToRecalculate, IDistanceMetric distanceMetric, IProgressData progressData)
        {
            var buckets = leafNodesAll
               .SelectMany(leafNode => distanceMetric.SignificantCoordinates(leafNode.Coords).Select(coord => new { Coord = coord, LeafNode = leafNode }))
               .GroupBy(pair => pair.Coord, pair => pair.LeafNode)
               .ToDictionary(g => g.Key, g => g.ToList());

            var calculateNeighborsByDistanceTasks = leafNodesToRecalculate.Select(async leafNode =>
            {
                leafNode.NeighborsByDistance = await Task.Run(() => GetNeighborsByDistance(leafNode, buckets, distanceMetric));
                progressData?.Increment();
            });

            await Task.WhenAll(calculateNeighborsByDistanceTasks);
        }

        // Most nodes end up clustered with one of their closest neighbors.
        // We still need to calculate all neighbors to determine which are the closest ones,
        // but after that we save only the closest few.
        //
        // This changes the memory usage from O(N^2) to O(N)
        // and also greatly speeds the cluster creation because there are fewer that need to be removed as clusters are formed.
        //
        // The drawback is that a small fraction of the nodes won't end up clustering with one of their closest neighbors.
        // For those, the neighbors will need to be "refilled" when the last neighbor has been removed before the cluster is formed.
        const int _maxNeighbors = 25;

        private static List<Neighbor> GetNeighborsByDistance(LeafNode leafNode, IDictionary<int, List<LeafNode>> buckets, IDistanceMetric distanceMetric)
        {
            var neighbors = distanceMetric.SignificantCoordinates(leafNode.Coords)
                // Get every node with at least one shared match in common
                .SelectMany(coord => buckets.TryGetValue(coord, out var bucket) ? bucket : Enumerable.Empty<LeafNode>())
                // We only need one direction A -> B (not also B -> A) since we're ultimately going to look at the smallest distances.
                .Where(neighborNode => neighborNode.Index < leafNode.Index)
                // Make sure that each node is considered only once (might have been in more than one bucket if more than one shared match in common.
                .Distinct()
                .Select(neighborNode => new Neighbor(neighborNode, leafNode))
                .LowestN(neighbor => neighbor.DistanceSquared, _maxNeighbors)
                .ToList();
            return neighbors;
        }

        private static List<Neighbor> GetNeighborsByDistance(IEnumerable<LeafNode> leafNodesAll, LeafNode leafNode, IDistanceMetric distanceMetric)
        {
            var neighbors = leafNodesAll
                .Where(neighborNode => neighborNode.Index < leafNode.Index)
                .Select(neighborNode => new Neighbor(neighborNode, leafNode))
                .Where(neighbor => neighbor.DistanceSquared != double.PositiveInfinity)
                .LowestN(neighbor => neighbor.DistanceSquared, _maxNeighbors)
                .ToList();
            return neighbors;
        }

        public List<IClusterableMatch> ExcludeLargeClusters(List<IClusterableMatch> clusterableMatches, int maxClusterSize)
        {
            _progressData.Reset($"Excluding clusters greater than {maxClusterSize} members");

            var clusterableMatchesOver20cM = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= 20).ToList();
            
            // Tentatively exclude matches who have more shared matches than maxClusterSize.
            // This typically excludes too many matches. For example, it will almost always exclude very close matches such as parents/children
            var matchesToExclude = clusterableMatchesOver20cM.Where(match => match.Count > maxClusterSize).ToList();

            // Also include matches where at least 3/4 of their shared matches are excluded
            var matchIndexesToExclude = new HashSet<int>(matchesToExclude.Select(match => match.Index));
            var partiallyExcludedMatches = clusterableMatchesOver20cM
                .Except(matchesToExclude)
                .AsParallel()
                .Where(match => match.Match.SharedCentimorgans >= 20 && match.Coords.Intersect(matchIndexesToExclude).Count() > match.Count / 2);

            matchesToExclude = matchesToExclude.Concat(partiallyExcludedMatches).ToList();

            // Restrict the excluded matches to those matches that have more than maxClusterSize shared matches that will also be excluded.
            while (true)
            {
                matchIndexesToExclude = new HashSet<int>(matchesToExclude.Select(match => match.Index));

                var matchesToExcludeUpdated = matchesToExclude
                    .AsParallel()
                    .Where(match =>
                    {
                        var intersectionSize = match.Coords.Intersect(matchIndexesToExclude).Count();
                        return intersectionSize > maxClusterSize || intersectionSize > match.Count / 2;
                    })
                    .ToList();

                if (matchesToExclude.Count == matchesToExcludeUpdated.Count)
                {
                    break;
                }

                matchesToExclude = matchesToExcludeUpdated;
            }

            return clusterableMatches.Except(matchesToExclude).ToList();
        }
    }
}