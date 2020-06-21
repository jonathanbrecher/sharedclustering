using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedClustering.Core;
using SharedClustering.Core.Anonymizers;

namespace SharedClustering.HierarchicalClustering
{
    public class ClusterableMatchBuilder
    {
        public static async Task<List<IClusterableMatch>> LoadClusterableMatchesAsync(
            List<Match> matches,
            Dictionary<string, List<int>> rawIcw,
            Dictionary<string, int> matchIndexes,
            double minCentimorgansToCluster,
            double minCentimorgansInSharedMatches,
            IAnonymizer anonymizer,
            Func<string, string, bool> askYesNoFunc,
            IProgressData progressData)
        {
            progressData.Description = "Loading data...";

            return await Task.Run(() =>
            {
                var strongMatches = matches.Where(match => match.SharedCentimorgans >= minCentimorgansToCluster).ToList();
                var maxMatchIndex = strongMatches.Count - 1;
                var maxIcwIndex = Math.Min(maxMatchIndex, matches.Count(match => match.SharedCentimorgans >= minCentimorgansInSharedMatches) + 1);
                maxIcwIndex = Math.Min(maxIcwIndex, matches.Count - 1);
                var strongMatchesGuids = strongMatches.Select(match => match.TestGuid).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var icw = rawIcw
                    .Where(kvp => strongMatchesGuids.Contains(kvp.Key))
                    .OrderBy(kvp => matchIndexes.TryGetValue(kvp.Key, out var index) ? index : matchIndexes.Count)
                    .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Where(index => index <= maxIcwIndex).ToList()
                    );
                var matchesDictionary = strongMatches.ToDictionary(match => match.TestGuid);
                var clusterableMatches = icw
                    .AsParallel().AsOrdered()
                    .Select((kvp, index) =>
                    {
                        var match = matchesDictionary[kvp.Key];
                        match = GetAnonymizedMatch(match, anonymizer);
                        return (IClusterableMatch)new ClusterableMatch(index, match, kvp.Value);
                    }
                    )
                    .ToList();

                return MaybeFilterMassivelySharedMatches(clusterableMatches, askYesNoFunc);
            });
        }

        private static List<IClusterableMatch> MaybeFilterMassivelySharedMatches(List<IClusterableMatch> clusterableMatches, Func<string, string, bool> askYesNoFunc)
        {
            var clusterableMatchesOver20cM = clusterableMatches.Where(match => match.Match.SharedCentimorgans > 20).ToList();
            if (clusterableMatchesOver20cM.Count > 0)
            {
                var lowestClusterableSharedCentimorgans = clusterableMatchesOver20cM.Last().Match.SharedCentimorgans;
                var filteringCutoff = clusterableMatchesOver20cM.Count / 3;

                // Consider which matches are left if excluding all matches who have shared matches with at least 1/3 of the total matches
                var clusterableMatchesFiltered = clusterableMatches
                    .Where(match =>
                        /*match.Match.SharedCentimorgans >= 1200
                        || (match.Match.SharedCentimorgans >= 50
                            && match.Match.SharedSegments > 1
                            && match.Match.SharedCentimorgans / match.Match.SharedSegments >= 13) // Large minimum sesgment length
                        ||*/ (match.Match.SharedCentimorgans >= lowestClusterableSharedCentimorgans
                            && match.Count < filteringCutoff)
                    ).ToList();

                // Don't do anything unless filtering will remove at least 100 matches (arbitrary cutoff)
                var numExcludedMatches = clusterableMatchesOver20cM.Count - clusterableMatchesFiltered.Count;
                if (numExcludedMatches >= 100
                    && askYesNoFunc(
                        "Do you want to exclude matches with huge numbers of shared matches?"
                        + Environment.NewLine + Environment.NewLine
                        + $"This will exclude {numExcludedMatches} matches (out of {clusterableMatches.Count}) with at least {filteringCutoff} shared matches.",
                        "Many shared matches"))
                {
                    var coordsFiltered = clusterableMatchesFiltered.Select(match => match.Index).ToHashSet();
                    clusterableMatches = clusterableMatchesFiltered
                        .Select(match => (IClusterableMatch)new ClusterableMatch(match.Index, match.Match, match.Coords.Where(coord => coordsFiltered.Contains(coord)).ToList()))
                        .ToList();
                }
            }
            return clusterableMatches;
        }

        private static Match GetAnonymizedMatch(Match match, IAnonymizer anonymizer)
        {
            if (anonymizer == null)
            {
                return match;
            }

            return new Match
            {        
                MatchTestAdminDisplayName = anonymizer.GetAnonymizedName(match.MatchTestAdminDisplayName),
                MatchTestDisplayName = anonymizer.GetAnonymizedName(match.MatchTestDisplayName),
                TestGuid = anonymizer.GetAnonymizedGuid(match.TestGuid),
                SharedCentimorgans = match.SharedCentimorgans,
                SharedSegments = match.SharedSegments,
                LongestBlock = match.LongestBlock,
                TreeType = match.TreeType,
                TreeUrl = match.TreeUrl == null ? null : "https://invalid",
                TreeSize = match.TreeSize,
                HasCommonAncestors = match.HasCommonAncestors,
                CommonAncestors = match.CommonAncestors?.Select(commonAncestor => anonymizer.GetAnonymizedName(commonAncestor)).ToList(),
                Starred = match.Starred,
                HasHint = match.HasHint,
                Note = null,
                TagIds = match.TagIds,
                IsFather = match.IsFather,
                IsMother = match.IsMother,
            };
        }
    }
}
