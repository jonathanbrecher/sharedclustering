using SharedClustering.HierarchicalClustering.PrimaryClusterFinders;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering
{
    public class ClusterAnalyzer
    {
        public IReadOnlyCollection<ClusterNode> Nodes { get; }
        public IReadOnlyDictionary<int, IClusterableMatch> MatchesByIndex { get; }
        public List<LeafNode> LeafNodes { get; }
        public List<IClusterableMatch> Matches { get; }
        public List<IClusterableMatch> NonDistantMatches { get; }
        public List<int> OrderedIndexes { get; }
        public HashSet<int> ImmediateFamilyIndexes { get; }
        public List<(int Start, int End)> Clusters { get; }
        public Dictionary<int, List<int>> IndexClusterNumbers { get; }

        private readonly GrowthBasedPrimaryClusterFinder _clusterFinder;

        public ClusterAnalyzer(
            IReadOnlyCollection<ClusterNode> nodes,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            GrowthBasedPrimaryClusterFinder clusterFinder,
            double lowestClusterableCentimorgans)
        {
            Nodes = nodes;
            MatchesByIndex = matchesByIndex;

            _clusterFinder = clusterFinder;

            // All nodes, in order. These will become rows/columns in the Excel file.
            LeafNodes = nodes.First().GetOrderedLeafNodes().ToList();

            (Matches, NonDistantMatches) = GetMatchesAndNonDistantMatches(LeafNodes, matchesByIndex, lowestClusterableCentimorgans);

            OrderedIndexes = NonDistantMatches
                .Select(match => match.Index)
                .ToList();

            // Because very strong matches are included in so many clusters,
            // excluding the strong matches makes it easier to identify edges of the clusters. 
            ImmediateFamilyIndexes = MatchesByIndex.Values
                .Where(match => match.Match.SharedCentimorgans >= 200)
                .Select(match => match.Index)
                .ToHashSet();

            // Identify which matches are members of which clusters.
            // Since clusters can overlap, one match can be a member of more than one cluster.
            Clusters = _clusterFinder.GetClusters(LeafNodes, NonDistantMatches, ImmediateFamilyIndexes).ToList();
            var leafNodeIndexes = LeafNodes.Select((leafNode, index) => (leafNode, index)).ToDictionary(pair => pair.leafNode.Index, pair => pair.index);
            IndexClusterNumbers = Clusters
                .SelectMany((cluster, clusterIndex) => Enumerable.Range(cluster.Start, cluster.End - cluster.Start + 1).Select(i => (LeafNodeIndex: LeafNodes[i].Index, ClusterIndex: clusterIndex + 1)))
                .GroupBy(pair => pair.LeafNodeIndex, pair => pair.ClusterIndex)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private (List<IClusterableMatch> matches, List<IClusterableMatch> nonDistantMatches) GetMatchesAndNonDistantMatches(
            List<LeafNode> leafNodes,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            double lowestClusterableCentimorgans)
        {
            // Ancestry never shows matches lower than 20 cM as shared matches.
            // The distant matches will be included as rows in the Excel file, but not as columns.
            // That means that correlation diagrams that include distant matches will be rectangular (tall and narrow)
            // rather than square.
            var matches = leafNodes
                .Where(leafNode => matchesByIndex.ContainsKey(leafNode.Index))
                .Select(leafNode => matchesByIndex[leafNode.Index])
                .ToList();
            var distantMatchCutoffCentimorgans = matches
                .SelectMany(match => match.Coords.Where(coord => coord != match.Index && matchesByIndex.ContainsKey(coord)))
                .Distinct()
                .Where(coord => matchesByIndex[coord].Match.SharedCentimorgans >= lowestClusterableCentimorgans)
                .Min(coord => matchesByIndex[coord].Match.SharedCentimorgans);
            var nonDistantMatches = matches
                .Where(match => match.Match.SharedCentimorgans >= distantMatchCutoffCentimorgans)
                .ToList();

            return (matches, nonDistantMatches);
        }
    }
}
