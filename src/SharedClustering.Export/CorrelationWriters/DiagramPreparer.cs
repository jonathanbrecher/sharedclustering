using SharedClustering.HierarchicalClustering;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.Export.CorrelationWriters
{
    public class DiagramPreparer
    {
        public double LowestClusterableCentimorgans { get; }

        public DiagramPreparer(
            IList<int> clusterableCoords,
            IList<IClusterableMatch> matches,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            ISet<string> testIdsToFilter)
        {
            LowestClusterableCentimorgans = GetLowestClusterableCentimorgans(clusterableCoords, matches, matchesByIndex, testIdsToFilter);
        }

        private static double GetLowestClusterableCentimorgans(
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
    }
}
