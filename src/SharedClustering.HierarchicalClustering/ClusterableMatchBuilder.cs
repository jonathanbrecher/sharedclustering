using SharedClustering.Core;
using SharedClustering.Core.Anonymizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                // This method assumes that the incoming data has already been validated for data integrity, see Serialized.Validate()

                // The caller may specify a subset of matches to load.
                var matchesToLoad = matches.Where(match => match.SharedCentimorgans >= minCentimorgansToCluster).ToList();

                // We only need to load in-common-with data for the matches that will actually be loaded.
                var maxIcwIndex = Math.Min(matchesToLoad.Count - 1, matches.Count(match => match.SharedCentimorgans >= minCentimorgansInSharedMatches) + 1);
                maxIcwIndex = Math.Min(maxIcwIndex, matches.Count - 1);
                var matchesToLoadGuids = matchesToLoad.Select(match => match.TestGuid).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var icw = rawIcw
                    .Where(kvp => matchesToLoadGuids.Contains(kvp.Key))
                    .OrderBy(kvp => matchIndexes.TryGetValue(kvp.Key, out var index) ? index : matchIndexes.Count)
                    .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Where(index => index <= maxIcwIndex).ToList()
                    );

                // Merge matches with their corresponding ICW data.
                var matchesDictionary = matchesToLoad.ToDictionary(match => match.TestGuid);
                var clusterableMatches = matchesToLoad
                    .AsParallel().AsOrdered()
                    .Select((match, index) =>
                    {
                        match = GetAnonymizedMatch(match, anonymizer);
                        var matchIcw = icw.TryGetValue(match.TestGuid, out var result) ? result : new List<int>();
                        return (IClusterableMatch)new ClusterableMatch(index, match, matchIcw);
                    })
                    .ToList();

                return MaybeFilterMassivelySharedMatches(clusterableMatches, askYesNoFunc);
            });
        }

        // Optionally exclude matche with massive numbers of shared matches, as sometimes seen in the presence of endogamy.
        // Matches with massive numbers of shared matches will overwhelm the clustering process and won't produce useful clusters anyway.
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

        // Optionally anonymize match data, according to anonymization behavior of the anonymizer.
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
                TestGuid = anonymizer.GetObfuscatedGuid(match.TestGuid),
                SharedCentimorgans = match.SharedCentimorgans,
                SharedSegments = match.SharedSegments,
                LongestBlock = match.LongestBlock,
                TreeType = match.TreeType,
                TreeUrl = match.TreeUrl == null ? null : "https://invalid", // Tree URLs are opaque data, so they cannot be anonymized in place. They have to be excluded completely.
                TreeSize = match.TreeSize,
                HasCommonAncestors = match.HasCommonAncestors,
                CommonAncestors = match.CommonAncestors?.Select(commonAncestor => anonymizer.GetAnonymizedName(commonAncestor)).ToList(),
                Starred = match.Starred,
                HasHint = match.HasHint,
                Note = null, // Notes are free-entry data, so they cannot be anonymized in place. They have to be excluded completely.
                TagIds = match.TagIds,
                IsFather = match.IsFather,
                IsMother = match.IsMother,
            };
        }
    }
}
