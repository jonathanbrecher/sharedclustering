using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters;
using AncestryDnaClustering.Models.HierarchicalClustering.Distance;
using AncestryDnaClustering.Models.HierarchicalClustering.MatrixBuilders;
using AncestryDnaClustering.Models.HierarchicalClustering.PrimaryClusterFinders;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.HierarchicalCustering
{
    public class HierarchicalCustering
    {
        private readonly int _minClusterSize;
        private readonly Func<List<IClusterableMatch>, IDistanceMetric> _distanceMetricFactory;
        private readonly IMatrixBuilder _matrixBuilder;
        private readonly IPrimaryClusterFinder _primaryClusterFinder;
        private readonly ICorrelationWriter _correlationWriter;
        private readonly ProgressData _progressData;

        public HierarchicalCustering(
            int minClusterSize,
            Func<List<IClusterableMatch>, IDistanceMetric> distanceMetricFactory,
            IMatrixBuilder matrixBuilder,
            IPrimaryClusterFinder primaryClusterFinder,
            ICorrelationWriter correlationWriter,
            ProgressData progressData)
        {
            _minClusterSize = minClusterSize;
            _distanceMetricFactory = distanceMetricFactory;
            _matrixBuilder = matrixBuilder;
            _primaryClusterFinder = primaryClusterFinder;
            _correlationWriter = correlationWriter;
            _progressData = progressData;
        }

        public async Task ClusterAsync(List<IClusterableMatch> clusterableMatches, HashSet<string> testGuidsToFilter, double minCentimorgansToCluster)
        {
            var minCentimorgansToClusterOver20 = Math.Max(minCentimorgansToCluster, 20);
            var maxIndex = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= minCentimorgansToClusterOver20).Max(match => match.Index);
            var clusterableMatchesToCorrelate = clusterableMatches.Where(match => match.Index <= maxIndex);
            if (testGuidsToFilter.Any())
            {
                clusterableMatchesToCorrelate = clusterableMatchesToCorrelate.Where(match => testGuidsToFilter.Contains(match.Match.TestGuid));
            }

            var clusterableMatchesToCorrelateList = clusterableMatchesToCorrelate.ToList();
            if (clusterableMatchesToCorrelateList.Count == 0)
            {
                return;
            }

            var matchesByIndex = clusterableMatches.ToDictionary(match => match.Index);

            var immediateFamily = clusterableMatchesToCorrelateList.Where(match => match.Match.SharedCentimorgans > 200).ToList();
            if (immediateFamily.Count > clusterableMatchesToCorrelateList.Count / 2)
            {
                immediateFamily.Clear();
            }

            var matrix = await _matrixBuilder.CorrelateAsync(clusterableMatchesToCorrelateList, immediateFamily);

            var nodes = await ClusterAsync(clusterableMatchesToCorrelateList, immediateFamily, maxIndex, matrix, _progressData);

            var primaryClusters = _primaryClusterFinder.GetPrimaryClusters(nodes.First())
                .Where(cluster => cluster.NumChildren >= _minClusterSize)
                .ToList();

            var indexClusterNumbers = primaryClusters
                .SelectMany((cluster, clusterNum) => cluster.GetOrderedLeafNodes().Select(leafNode => new { LeafNode = leafNode, ClusterNum = clusterNum + 1}))
                .ToDictionary(pair => pair.LeafNode.Index, pair => pair.ClusterNum);

            if (minCentimorgansToCluster < minCentimorgansToClusterOver20)
            {
                var leafNodes = nodes.First().GetOrderedLeafNodes().ToList();

                var primaryClustersSet = new HashSet<ClusterNode>(primaryClusters);

                var extendedClusters = await ExtendClustersAsync(clusterableMatches, primaryClustersSet, leafNodes, minCentimorgansToCluster);

                await Recluster(nodes, extendedClusters, immediateFamily, matchesByIndex, matrix);
            }

            await _correlationWriter.OutputCorrelationAsync(nodes, matchesByIndex, indexClusterNumbers);
        }

        private async Task<List<ClusterNode>> ClusterAsync(List<IClusterableMatch> clusterableMatches, List<IClusterableMatch> immediateFamily, int maxIndex, ConcurrentDictionary<int, double[]> matrix, ProgressData progressData)
        {
            var distanceMetric = _distanceMetricFactory(immediateFamily);

            var matchNodes = await GetLeafNodesAsync(clusterableMatches, matrix, maxIndex, distanceMetric, progressData).ConfigureAwait(false);

            var nodes = await BuildClustersAsync(matchNodes, distanceMetric, progressData).ConfigureAwait(false);

            return nodes;
        }

        private async Task Recluster(List<ClusterNode> nodes, Dictionary<ClusterNode, List<IClusterableMatch>> extendedClusters, List<IClusterableMatch> immediateFamily, Dictionary<int, IClusterableMatch> matchesByIndex, ConcurrentDictionary<int, double[]> matrix)
        {
            _progressData.Reset($"Reclustering {extendedClusters.Count} primary clusters", extendedClusters.Count);

            var primaryClustersTasks = extendedClusters
                .Where(kvp => kvp.Value.Count > 0)
                .Select(async kvp =>
            {
                var nodeToRecluster = kvp.Key;
                var additionalMatches = kvp.Value;
                var leafNodesByIndex = nodeToRecluster.GetOrderedLeafNodes().ToDictionary(leafNode => leafNode.Index);
                var clusterableMatches = leafNodesByIndex.Keys.Select(index => matchesByIndex[index]).Concat(additionalMatches).ToList();

                var maxIndex = additionalMatches.Max(match => match.Coords.Max());
                _matrixBuilder.ExtendMatrix(matrix, additionalMatches, maxIndex);

                var reclusteredNodes = await ClusterAsync(clusterableMatches, immediateFamily, clusterableMatches.Max(match => match.Index), matrix, ProgressData.SuppressProgress).ConfigureAwait(false);

                if (reclusteredNodes.Count == 0)
                {
                    _progressData.Increment();
                    return nodeToRecluster;
                }
                var reclusteredNode = reclusteredNodes.First();
                foreach (var reclusteredLeafNode in reclusteredNode.GetOrderedLeafNodes())
                {
                    if (leafNodesByIndex.TryGetValue(reclusteredLeafNode.Index, out var originalLeafNode))
                    {
                        reclusteredLeafNode.Parent.ReplaceChild(reclusteredLeafNode, originalLeafNode);
                    }
                }
                if (nodeToRecluster.Parent != null)
                {
                    nodeToRecluster.Parent.ReplaceChild(nodeToRecluster, reclusteredNode);
                }
                else
                {
                    nodes.Remove(nodeToRecluster);
                    nodes.Add(reclusteredNode);
                }
                _progressData.Increment();
                return reclusteredNode;
            });
            var primaryClusters = await Task.WhenAll(primaryClustersTasks);

            _progressData.Reset();
        }

        private void FindNodesToRecluster(ClusterNode clusterNode, int minSize, int maxSize, double parentDistance, List<ClusterNode> nodesToRecluster)
        {
            if (clusterNode == null || clusterNode.NumChildren < minSize)
            {
                return;
            }

            if (clusterNode.Parent != null && clusterNode.NumChildren <= maxSize)
            {
                nodesToRecluster.Add(clusterNode);
                return;
            }

            FindNodesToRecluster(clusterNode.First as ClusterNode, minSize, maxSize, clusterNode.Distance, nodesToRecluster);
            FindNodesToRecluster(clusterNode.Second as ClusterNode, minSize, maxSize, clusterNode.Distance, nodesToRecluster);
        }

        private async Task<Dictionary<ClusterNode, List<IClusterableMatch>>> ExtendClustersAsync(List<IClusterableMatch> clusterableMatches, HashSet<ClusterNode> primaryClusters, List<LeafNode> leafNodes, double minCentimorgansToCluster)
        {
            var maxClusteredIndex = leafNodes.Max(leafNode => leafNode.Index);
            var otherMatches = clusterableMatches
                .Where(match => match.Index > maxClusteredIndex && match.Match.SharedCentimorgans >= minCentimorgansToCluster && match.Coords.Count >= _minClusterSize).ToList();
            var leafNodesByMatchIndex = leafNodes.ToDictionary(leafNode => leafNode.Index);

            _progressData.Reset($"Extending clusters with {otherMatches.Count} matches...", otherMatches.Count);

            var extendedClusters = await Task.Run(() => otherMatches.AsParallel().Select(match =>
            {
                var parentClusters = match.Coords
                    .Select(coord => leafNodesByMatchIndex.TryGetValue(coord, out var leafNode) ? leafNode : null)
                    .Where(leafNode => leafNode != null)
                    .SelectMany(leafNode => leafNode.GetParents())
                    .ToList();

                var bestParentCluster = parentClusters
                    .Where(parentCluster => primaryClusters.Contains(parentCluster))
                    .GroupBy(c => c)
                    .Where(g => g.Count() >= Math.Max(_minClusterSize, 0.35 * g.Key.NumChildren)
                        || (g.Count() >= Math.Max(_minClusterSize, 0.5 * match.Coords.Count)))
                    .Select(g => g.Key)
                    .OrderByDescending(parentCluster => parentCluster.NumChildren)
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

        private async Task<List<ClusterNode>> BuildClustersAsync(List<Node> nodes, IDistanceMetric distanceMetric, ProgressData progressData)
        {
            progressData.Reset($"Building clusters for {nodes.Count} matches...", nodes.Count - 1);

            await Task.Run(async () =>
            {
                while (nodes.Count > 1)
                {
                    var nodesToCluster = nodes
                        .SelectMany(node => node.FirstLeaf == node.SecondLeaf ? new[] { node.FirstLeaf } : new[] { node.FirstLeaf, node.SecondLeaf })
                        .Select(leafNode => leafNode.NeighborsByDistance.FirstOrDefault())
                        .Where(neighbor => neighbor != null)
                        .OrderBy(neighbor => neighbor.DistanceSquared)
                        .ToList();

                    ClusterNode clusterNode;
                    if (nodesToCluster.Count == 0)
                    {
                        var nodesLargestFirst = nodes.OrderByDescending(node => node.GetOrderedLeafNodes().Count()).Take(2).ToList();
                        clusterNode = new ClusterNode(nodesLargestFirst[0], nodesLargestFirst[1], double.PositiveInfinity, distanceMetric);
                    }
                    else
                    {
                        var nodeToCluster = nodesToCluster.First();
                        var firstNode = nodeToCluster.Node;
                        var secondNode = nodeToCluster.Parent;
                        var first = firstNode.GetHighestParent();
                        var second = secondNode.GetHighestParent();
                        clusterNode = new ClusterNode(first, second, nodeToCluster.DistanceSquared, distanceMetric);
                    }

                    if (clusterNode.First.FirstLeaf != clusterNode.First.SecondLeaf)
                    {
                        var removeNeighborsTasks = nodes.Select(node => Task.Run(() => 
                        {
                            node.FirstLeaf.NeighborsByDistance?.RemoveAll(neighbor => neighbor.Node == clusterNode.First.SecondLeaf);
                            if (node.FirstLeaf != node.SecondLeaf)
                            {
                                node.SecondLeaf.NeighborsByDistance?.RemoveAll(neighbor => neighbor.Node == clusterNode.First.SecondLeaf);
                            }
                        }));
                        await Task.WhenAll(removeNeighborsTasks);
                    }
                    if (clusterNode.Second.FirstLeaf != clusterNode.Second.SecondLeaf)
                    {
                        var removeNeighborsTasks = nodes.Select(node => Task.Run(() =>
                        {
                            node.FirstLeaf.NeighborsByDistance?.RemoveAll(neighbor => neighbor.Node == clusterNode.Second.FirstLeaf);
                            if (node.FirstLeaf != node.SecondLeaf)
                            {
                                node.SecondLeaf.NeighborsByDistance?.RemoveAll(neighbor => neighbor.Node == clusterNode.Second.FirstLeaf);
                            }
                        }));
                        await Task.WhenAll(removeNeighborsTasks);
                    }

                    nodes.Remove(clusterNode.First);
                    nodes.Remove(clusterNode.Second);

                    var leafNodes = new HashSet<LeafNode>(clusterNode.GetOrderedLeafNodes());
                    clusterNode.FirstLeaf.NeighborsByDistance.RemoveAll(neighbor => leafNodes.Contains(neighbor.Node));
                    clusterNode.SecondLeaf.NeighborsByDistance.RemoveAll(neighbor => leafNodes.Contains(neighbor.Node));

                    nodes.Add(clusterNode);

                    progressData.Increment();
                }
            });

            progressData.Reset("Done");

            return nodes.OfType<ClusterNode>().ToList();
        }

        private async Task<List<Node>> GetLeafNodesAsync(List<IClusterableMatch> clusterableMatches, ConcurrentDictionary<int, double[]> matrix, int maxIndex, IDistanceMetric distanceMetric, ProgressData progressData)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());

            progressData.Reset($"Calculating coordinates for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var leafNodes = await Task.Run(() =>
            {
                return clusterableMatches
                    .Select(match => new LeafNode(match.Index, matrix[match.Index], distanceMetric))
                    .ToList<Node>();
            });

            progressData.Reset($"Finding closest pairwise distances for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = await Task.Run(() => Enumerable.Range(0, maxIndex + 1)
                .ToDictionary(i => i, i => leafNodes.Where(leafNode => leafNode.Coords.ContainsKey(i)).ToList<Node>()));

            var calculateNeighborsByDistanceTasks = leafNodes.Select(async leafNode =>
            {
                leafNode.NeighborsByDistance = await Task.Run(() => GetNeighborsByDistance(leafNode, buckets));
                progressData.Increment();
            });

            await Task.WhenAll(calculateNeighborsByDistanceTasks);

            var result =  leafNodes.Where(leafNode => leafNode.NeighborsByDistance.Count > 0).ToList();

            progressData.Reset();
            return result;
        }

        private List<Neighbor> GetNeighborsByDistance(Node node, IDictionary<int, List<Node>> buckets)
        {
            var neighbors = node.Coords
                .SelectMany(coord => buckets.TryGetValue(coord.Key, out var bucket) ? bucket : Enumerable.Empty<Node>())
                .Where(otherNode => otherNode != node)
                .GroupBy(otherNode => otherNode)
                .Select(g => new Neighbor(g.Key, node))
                .OrderBy(neighbor => neighbor.DistanceSquared)
                .ToList();
            return neighbors;
        }
    }
}