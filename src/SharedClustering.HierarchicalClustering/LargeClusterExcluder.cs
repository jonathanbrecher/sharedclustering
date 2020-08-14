using SharedClustering.Core;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering
{
    public class LargeClusterExcluder
    {
        public static List<IClusterableMatch> MaybeExcludeLargeClusters(List<IClusterableMatch> clusterableMatches, int? maxClusterSize, IProgressData progressData)
            => maxClusterSize != null ? ExcludeLargeClusters(clusterableMatches, maxClusterSize.Value, progressData) : clusterableMatches;

        // Exclude clusters larger than a given size, typically as a result of endogamy.
        // This is mainly useful for testers who have half or a quarter of their ancestry from a very endogamous group,
        // which would normally overwhelm the cluster diagram.
        // After removing clusters associated with the endogamic matches, the smaller number of remaining matches may be easier to interpret.
        // Accordingly, this is best used when maxClusterSize is greater than a number that would not normally be seen in non-endogamic matches,
        // such as cluster sizes of 200 or even 500.
        private static List<IClusterableMatch> ExcludeLargeClusters(List<IClusterableMatch> clusterableMatches, int maxClusterSize, IProgressData progressData)
        {
            progressData.Reset($"Excluding clusters greater than {maxClusterSize} members");

            var clusterableMatchesOver20cM = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= 20).ToList();
            
            // Tentatively exclude matches who have more shared matches than maxClusterSize.
            // This typically excludes too many matches. For example, it will almost always exclude very close matches such as parents/children
            var matchesToExclude = clusterableMatchesOver20cM.Where(match => match.Count > maxClusterSize).ToList();

            // Also include matches where at least half of their shared matches are excluded
            var matchIndexesToExclude = matchesToExclude.Select(match => match.Index).ToHashSet();
            var partiallyExcludedMatches = clusterableMatchesOver20cM
                .Except(matchesToExclude)
                .AsParallel()
                .Where(match => match.Match.SharedCentimorgans >= 20 && match.Coords.Intersect(matchIndexesToExclude).Count() > match.Count / 2);

            matchesToExclude = matchesToExclude.Concat(partiallyExcludedMatches).ToList();

            // Restrict the excluded matches to those matches that have more than maxClusterSize shared matches that will also be excluded.
            while (true)
            {
                matchIndexesToExclude = matchesToExclude.Select(match => match.Index).ToHashSet();

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