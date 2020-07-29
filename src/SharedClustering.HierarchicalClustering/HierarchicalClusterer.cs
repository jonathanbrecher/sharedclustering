using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using SharedClustering.Core;
using SharedClustering.HierarchicalClustering.CorrelationWriters;
using SharedClustering.HierarchicalClustering.Distance;
using SharedClustering.HierarchicalClustering.MatrixBuilders;
using SharedClustering.HierarchicalClustering.PrimaryClusterFinders;

namespace SharedClustering.HierarchicalClustering
{
    public class HierarchicalClusterer
    {
        private readonly IClusterBuilder _clusterBuilder;
        private readonly IClusterExtender _clusterExtender;
        private readonly int _minClusterSize;
        private readonly Func<List<IClusterableMatch>, IDistanceMetric> _distanceMetricFactory;
        private readonly IMatrixBuilder _matrixBuilder;
        private readonly IPrimaryClusterFinder _primaryClusterFinder;
        private readonly ICorrelationWriter _correlationWriter;
        private readonly Func<string, string, bool> _askYesNo;
        private readonly IProgressData _progressData;

        public HierarchicalClusterer(
            IClusterBuilder clusterBuilder,
            IClusterExtender clusterExtender,
            int minClusterSize,
            Func<List<IClusterableMatch>, IDistanceMetric> distanceMetricFactory,
            IMatrixBuilder matrixBuilder,
            IPrimaryClusterFinder primaryClusterFinder,
            ICorrelationWriter correlationWriter,
            Func<string, string, bool> askYesNo,
            IProgressData progressData)
        {
            _clusterBuilder = clusterBuilder;
            _clusterExtender = clusterExtender;
            _minClusterSize = minClusterSize;
            _distanceMetricFactory = distanceMetricFactory;
            _matrixBuilder = matrixBuilder;
            _primaryClusterFinder = primaryClusterFinder;
            _correlationWriter = correlationWriter;
            _askYesNo = askYesNo;
            _progressData = progressData;
        }

        public static double GetLowestClusterableCentimorgans(
            IList<int> clusterableCoords,
            IList<IClusterableMatch> matches,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            ISet<string> testIdsToFilter)
        {
            // Find the absolute lowest shared centimorgans value.
            var lowestCentimorgans = clusterableCoords.Min(coord => matchesByIndex[coord].Match.SharedCentimorgans);

            // Shared centimorgan values down to 20 cM are clusterable on all sites.
            if (lowestCentimorgans >= 20)
            {
                return lowestCentimorgans;
            }

            // Find the lowest shared centimorgans that is a shared match to some other match.
            var lowestSharedCentimorgans = matches
                .Where(match => testIdsToFilter.Count == 0 || testIdsToFilter.Contains(match.Match.TestGuid))
                .SelectMany(match => match.Coords.Where(coord => coord != match.Index))
                .Distinct()
                .Select(coord => matchesByIndex.TryGetValue(coord, out var match) ? match.Match.SharedCentimorgans : lowestCentimorgans)
                .DefaultIfEmpty(lowestCentimorgans)
                .Min();

            return lowestSharedCentimorgans;
        }

        // Clusterable matches are normally symmetric: If A is a shared match to B, then B should be a shared match to A.
        // Matches under 20 cM at Ancestry are an exception, and can be excluded via the lowestClusterableCentimorgans parameter.
        public static List<(IClusterableMatch, IClusterableMatch)> FindAsymmetricData(List<IClusterableMatch> matches, double lowestClusterableCentimorgans)
        {
            // Find all matches above lowestClusterableCentimorgans.
            // This test intentionally ignores matches with exactly lowestClusterableCentimorgans. 
            // Ancestry rounds their centimorgan values, so some matches reported as 20 cM are clusterable, while some are not.
            var clusterableMatchesByIndex = matches
                .Where(match => match.Match.SharedCentimorgans > lowestClusterableCentimorgans)
                .ToDictionary(match => match.Index);

            // Find the highest index (the index of the match with the lowest clusterable centimorgans).
            var lowestClusterableMatchIndex = clusterableMatchesByIndex.Values
                .Select(match => match.Index)
                .DefaultIfEmpty(-1)
                .Max();

            // Generate pairs of each match and shared match.
            var allMatchPairs = clusterableMatchesByIndex.Values
                .SelectMany(match => match.Coords.Where(coord => coord <= lowestClusterableMatchIndex), (match, coord) => (match.Index, Coord: coord))
                .ToHashSet();

            // An asymmetric pair is a pair where (match, sharedMatch) is included in the data, but (sharedMatch, match) is not.
            return allMatchPairs
                .Where(pair => !allMatchPairs.Contains((pair.Coord, pair.Index)) && clusterableMatchesByIndex.ContainsKey(pair.Index) && clusterableMatchesByIndex.ContainsKey(pair.Coord))
                .Select(pair => (clusterableMatchesByIndex[pair.Index], clusterableMatchesByIndex[pair.Coord]))
                .ToList();
        }

        public async Task<List<string>> ClusterAsync(
            IReadOnlyCollection<IClusterableMatch> clusterableMatches,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            ISet<string> testIdsToFilter,
            double lowestClusterableCentimorgans,
            double minCentimorgansToCluster)
        {
            // The lowestClusterableCentimorgans is the lowest value that is possible to cluster.
            // At Ancestry, matches under 20 cM cannot be clustered. The number may be lower at other sites.
            // Clusters can be requested with a lower value of minCentimorgansToCluster,
            // but the clusters themselves cannot be generated with a lower cutoff than lowestClusterableCentimorgans.
            var minCentimorgansToClusterTruncated = Math.Max(lowestClusterableCentimorgans, minCentimorgansToCluster);

            // Find the largest index that can be clustered and the corresponding clusterable matches.
            // This assumes that matches are sorted in order of decreasing centimorgans.
            var maxIndex = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= minCentimorgansToClusterTruncated).Max(match => match.Index);
            var clusterableMatchesToCorrelate = clusterableMatches.Where(match => match.Index <= maxIndex);

            // Further restrict the matches if filtering is requested.
            if (testIdsToFilter.Any())
            {
                clusterableMatchesToCorrelate = clusterableMatchesToCorrelate.Where(match => testIdsToFilter.Contains(match.Match.TestGuid));
            }

            // Return early if nothing to do.
            var clusterableMatchesToCorrelateList = clusterableMatchesToCorrelate.ToList();
            if (clusterableMatchesToCorrelateList.Count == 0)
            {
                return new List<string>();
            }

            // The output file might allow only a limited number of columns. Return early if that isn't acceptable.
            if (clusterableMatchesToCorrelateList.Count > _correlationWriter.MaxColumns)
            {
                if (!_askYesNo(
                    $"At most {_correlationWriter.MaxColumns} matches can be written to one file.{Environment.NewLine}{Environment.NewLine}" +
                    $"{clusterableMatchesToCorrelateList.Count} matches will be split into several output files.{Environment.NewLine}{Environment.NewLine}" +
                    "Continue anyway?",
                    "Too many matches"))
                {
                    return new List<string>();
                }
            }

            // Identify the closest matches. Depending on the matrix-building options, these matches may not contribute
            // gray cells to the final cluster diagram, to avoid swamping the final diagram with too much gray.
            // The 200 cM cutoff corresponds roughly to second cousins, which is a reasonable but arbitrary cutoff.
            var immediateFamily = clusterableMatchesToCorrelateList.Where(match => match.Match.SharedCentimorgans > 200).ToList();

            // ...but if more than half of the matches are over the cutoff, just cluster everything without special considerations.
            if (immediateFamily.Count > clusterableMatchesToCorrelateList.Count / 2)
            {
                immediateFamily.Clear();
            }

            // Generate the base correlation matrix based on the requested matrix-building behaviors.
            var matrix = await _matrixBuilder.CorrelateAsync(clusterableMatchesToCorrelateList, immediateFamily);

            // Determine how to calculate the distance between matches.
            var distanceMetric = _distanceMetricFactory(immediateFamily);

            // Generate clusters.
            var nodes = await _clusterBuilder.BuildClustersAsync(clusterableMatches, matrix, distanceMetric, _progressData).ConfigureAwait(false);

            // Identify the main clusters within the diagram.
            var primaryClusters = _primaryClusterFinder.GetPrimaryClusters(nodes.FirstOrDefault())
                .Where(cluster => cluster.NumChildren >= _minClusterSize)
                .ToList();
            
            // Identify which cluster (by cluster number) contains each leaf node.
            // This assumes that each leaf node is a member of only one cluster, which may not be true in fact.
            // This calculation happens before possibly extending the clusters, so that any extended matches are _not_ reported as part of the primary cluster.
            var indexClusterNumbers = primaryClusters
                .SelectMany((cluster, clusterNum) => cluster.GetOrderedLeafNodes().Select(leafNode => new { LeafNode = leafNode, ClusterNum = clusterNum + 1 }))
                .ToDictionary(pair => pair.LeafNode.Index, pair => pair.ClusterNum);

            // If there are additional matches outside of the clusterable range, extend the clusters by adding the additional matches to their best-fit cluster.
            // This allows Ancestry's matches under 20 cM to be included in a cluster diagram even though those matches cannot participate in clustering themselves.
            nodes = await _clusterExtender.MaybeExtendAsync(nodes, maxIndex, clusterableMatches, primaryClusters, minCentimorgansToCluster, distanceMetric, matchesByIndex, matrix);

            // Save the final cluster diagram to the desired output file.
            return await _correlationWriter.OutputCorrelationAsync(nodes, matchesByIndex, indexClusterNumbers);
        }
    }
}