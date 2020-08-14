using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering.PrimaryClusterFinders
{
    /// <summary>
    /// Identify clusters in a pseudo-visual approach, allowing overlap between clusters.
    /// </summary>
    public class GrowthBasedPrimaryClusterFinder
    {
        private readonly int _minClusterSize;

        public GrowthBasedPrimaryClusterFinder(int minClusterSize)
        {
            _minClusterSize = Math.Max(2, minClusterSize);
        }

        public IEnumerable<(int Start, int End)> GetClusters(
            List<LeafNode> leafNodes,
            List<IClusterableMatch> nonDistantMatches,
            HashSet<int> immediateFamilyIndexes)
        {
            if (leafNodes.Count == 0 || nonDistantMatches.Count == 0)
            {
                return Enumerable.Empty<(int, int)>();
            }

            var nonDistantMatchIndexes = nonDistantMatches.Select(match => match.Index).ToHashSet();

            var matrix = leafNodes
                .Where(leafNode => nonDistantMatchIndexes.Contains(leafNode.Index))
                .AsParallel().AsOrdered()
                .Select(leafNode => nonDistantMatches.Select(match => leafNode.Coords.TryGetValue(match.Index, out var val) && val >= 1).ToArray())
                .ToArray();

            var matchMatrix = new MatchMatrix(matrix, _minClusterSize);

            matchMatrix.ImmediateFamilyMatches.AddRange(leafNodes.Select((leafNode, index) => (leafNode, index)).Where(pair => immediateFamilyIndexes.Contains(pair.leafNode.Index)).Select(pair => pair.index));

            return matchMatrix.GetClusters();
        }
    }
}
