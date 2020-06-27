using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedClustering.Core;
using SharedClustering.HierarchicalClustering.Distance;
using SharedClustering.HierarchicalClustering.MatrixBuilders;

namespace SharedClustering.HierarchicalClustering
{
    public class ClusterExtender : IClusterExtender
    {
        private readonly ClusterBuilder _clusterBuilder;
        private readonly int _minClusterSize;
        private readonly IMatrixBuilder _matrixBuilder;
        private readonly IProgressData _progressData;

        public ClusterExtender(
            ClusterBuilder clusterBuilder,
            int minClusterSize,
            IMatrixBuilder matrixBuilder,
            IProgressData progressData)
        {
            _clusterBuilder = clusterBuilder;
            _minClusterSize = minClusterSize;
            _matrixBuilder = matrixBuilder;
            _progressData = progressData;
        }

        public async Task<List<ClusterNode>> MaybeExtendAsync(
            List<ClusterNode> nodes,
            int maxIndex,
            IReadOnlyCollection<IClusterableMatch> clusterableMatches, 
            IReadOnlyCollection<Node> primaryClusters,
            double minCentimorgansToCluster,
            IDistanceMetric distanceMetric,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            ConcurrentDictionary<int, float[]> matrix)
        {
            if (nodes.Count > 0 && maxIndex < clusterableMatches.Max(match => match.Index))
            {
                var leafNodes = nodes.First().GetOrderedLeafNodes().ToList();

                var extendedClusters = await ExtendClustersAsync(clusterableMatches, primaryClusters, leafNodes, minCentimorgansToCluster);

                await Recluster(nodes, extendedClusters, distanceMetric, matchesByIndex, matrix);
            }

            return nodes;
        }

        private async Task<Dictionary<Node, List<IClusterableMatch>>> ExtendClustersAsync(IEnumerable<IClusterableMatch> clusterableMatches, IReadOnlyCollection<Node> primaryClusters, IReadOnlyCollection<LeafNode> leafNodes, double minCentimorgansToCluster)
        {
            // Identify other matches that have not been clustered yet, which are eligible to be added to existing clusters.
            var maxClusteredIndex = leafNodes.Max(leafNode => leafNode.Index);
            var otherMatches = clusterableMatches
                .Where(match => match.Index > maxClusteredIndex && match.Match.SharedCentimorgans >= minCentimorgansToCluster && match.Coords.Count >= _minClusterSize).ToList();

            var leafNodesByMatchIndex = leafNodes.ToDictionary(leafNode => leafNode.Index);

            _progressData.Reset($"Extending clusters with {otherMatches.Count} matches...", otherMatches.Count);

            var extendedClusters = await Task.Run(() => otherMatches.Select(match =>
            {
                // Identify all clusters that contain at least one shared match to this match.
                var parentClusters = match.Coords
                    .Select(coord => leafNodesByMatchIndex.TryGetValue(coord, out var leafNode) ? leafNode : null)
                    .Where(leafNode => leafNode != null)
                    .SelectMany(leafNode => leafNode.GetParents().OfType<Node>().Concat(new[] { leafNode }))
                    .ToList();

                // This match should be added to the existing cluster that contains the largest number of shared matches
                // (as long as the existing cluster contains at least _minClusterSize of the shared matches).
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

        // Optimize the appearance of existing clusters within an existing cluster diagram,
        // after additional matches were added to extend the existing clusters.
        private async Task Recluster(
            List<ClusterNode> nodes,
            IReadOnlyDictionary<Node, List<IClusterableMatch>> extendedClusters,
            IDistanceMetric distanceMetric,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            ConcurrentDictionary<int, float[]> matrix)
        {
            _progressData.Reset($"Reclustering {extendedClusters.Count} primary clusters", extendedClusters.Count);

            // Identify input parameters for each cluster that has been extended and needs to be reclustered.
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
            if (additionalMatchesDistinct.Count < 0)
            {
                _progressData.Reset();
                return;
            }
            var maxIndex = additionalMatchesDistinct.Select(match => match.Index).Max();

            _matrixBuilder.ExtendMatrix(matrix, additionalMatchesDistinct, maxIndex);

            var primaryClustersTasks = primaryClustersTaskData
                .Select(async data =>
                {
                    var reclusteredNodes = await _clusterBuilder.BuildClustersAsync(data.ClusterableMatches, matrix, distanceMetric, new SuppressProgress()).ConfigureAwait(false);

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
    }
}