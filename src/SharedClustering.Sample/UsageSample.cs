using SharedClustering.Core;
using SharedClustering.Core.Anonymizers;
using SharedClustering.Export;
using SharedClustering.Export.CorrelationWriters;
using SharedClustering.HierarchicalClustering;
using SharedClustering.HierarchicalClustering.Distance;
using SharedClustering.HierarchicalClustering.MatrixBuilders;
using SharedClustering.HierarchicalClustering.PrimaryClusterFinders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedClustering.Sample
{
    class UsageSample
    {
        public async Task DoClusteringAsync()
        {
            // Some reasonable defaults. These would normally be under user control.
            var minCentimorgansToCluster = 20;
            var minCentimorgansInSharedMatches = 20;
            var minClusterSize = 3;
            var maxGrayPercentage = 0.05; // At most 5% of cells will be gray
            var maxMatchesPerClusterFile = 10000;   // Excel has a maximum of about 16,000 columns; using a slightly smaller number to leave some margin.
            var ancestryHostName = "www.ancestry.com"; // might be localized if desired
            var testTakerTestId = "zzzzz";  // This needs to be specified for the test taker
            var outputFileName = @"C:\Users\jonat\Desktop\New folder\clustered-output.xlsx";  // This should be specified by the user

            // This would be a way to filter matches to a subset of the total. For this example, we'll use everything (no filtering except for the centimorgans range).
            var testIdsToFilter = new HashSet<string>();

            // A list of tags that describe the "colored dots" groups in Ancestry. For this sample, we'll use no tags.
            var tags = new List<Tag>();

            // An arbitrary name for the worksheet created in the saved Excel files.
            var worksheetName = "heatmap";

            // Not going to display a dynamic progress bar in this example.
            var suppressProgress = new SuppressProgress();

            // Define some file utilities, with no retries or error reporting.
            var fileUtils = new CoreFileUtils((a, b) => false, (a, b) => false, (message, title) => { });



            var matches = GetMatches();

            ValidateMatches(matches);

            // Assign zero-based indexes to the matches sorted by shared centimorgans descending.
            var matchIndexes = matches
                .Select(match => match.TestGuid)
                .Select((id, index) => new { Id = id, Index = index })
                .ToDictionary(pair => pair.Id, pair => pair.Index);

            var rawIcw = GetRawIcw(matchIndexes);

            // Convert matches and ICWs to ClusterableMatches,
            var clusterableMatches = await ClusterableMatchBuilder.LoadClusterableMatchesAsync(
                matches,
                rawIcw,
                matchIndexes,
                minCentimorgansToCluster,
                minCentimorgansInSharedMatches,
                new NonAnonymizer(), // not anonymizing the output
                (message, title) => false, // not filtering massively shared matches
                suppressProgress);

            // Optionally filter the matches by testIdsToFilter.
            var filteredMatches = clusterableMatches
                .Where(match => match.Match.SharedCentimorgans >= minCentimorgansToCluster
                    && (testIdsToFilter.Count == 0 || testIdsToFilter.Contains(match.Match.TestGuid)))
                .ToList();

            // Return early if nothing to do
            if (filteredMatches.Count == 0)
            {
                var errorMessage = testIdsToFilter.Count > 0
                    ? $"No matches found over {minCentimorgansToCluster} cM that match any of {testIdsToFilter.Count} filtered IDs. Clusters could not be generated."
                    : $"No matches found over {minCentimorgansToCluster} cM. Clusters could not be generated.";

                throw new Exception(errorMessage);
            }

            var matchesByIndex = clusterableMatches.ToDictionary(match => match.Index);

            // Only generate clusters based on shared matches that are also included in the cluster.
            var clusterableCoords = filteredMatches
                .SelectMany(match => testIdsToFilter.Count == 0
                    ? match.Coords.Where(coord => coord != match.Index)
                    : new[] { match.Index })
                .Distinct()
                .Where(coord => matchesByIndex.ContainsKey(coord))
                .ToList();

            // Return early if nothing to do
            if (clusterableCoords.Count == 0)
            {
                throw new Exception($"No shared matches found for any of {filteredMatches.Count} matches. Clusters could not be generated.");
            }

            // Ancestry doesn't report shared matches below 20 cM. This number could be lowered if generating clusters from other sites.
            var diagramPreparer = new DiagramPreparer(clusterableCoords, filteredMatches, matchesByIndex, testIdsToFilter);

            // Perform the actual clustering.
            var matrixBuilder = new AppearanceWeightedMatrixBuilder(diagramPreparer.LowestClusterableCentimorgans, maxGrayPercentage, suppressProgress);
            var clusterBuilder = new ClusterBuilder(minClusterSize);
            var clusterExtender = new ClusterExtender(clusterBuilder, minClusterSize, matrixBuilder, suppressProgress);
            var correlationWriter = new ExcelCorrelationWriter(outputFileName, tags, worksheetName, testTakerTestId, ancestryHostName, minClusterSize, maxMatchesPerClusterFile, fileUtils, suppressProgress);
            var hierarchicalClustering = new HierarchicalClusterer(
                clusterBuilder,
                clusterExtender,
                minClusterSize,
                _ => new OverlapWeightedEuclideanDistanceSquared(),
                matrixBuilder,
                new HalfMatchPrimaryClusterFinder(),
                correlationWriter.MaxColumns,
                fileUtils.AskYesNo,
                suppressProgress);

            var lowestClusterableCentimorgans = diagramPreparer.LowestClusterableCentimorgans;
            var nodes = await hierarchicalClustering.ClusterAsync(clusterableMatches, matchesByIndex, testIdsToFilter, lowestClusterableCentimorgans, minCentimorgansToCluster);
            var clusterAnalyer = new ClusterAnalyzer(nodes, matchesByIndex, new GrowthBasedPrimaryClusterFinder(minClusterSize), lowestClusterableCentimorgans);
            var files = await correlationWriter.OutputCorrelationAsync(clusterAnalyer);
        }

        /// <summary>
        /// Get in-common-with data for each match. This would normally be implemented by downloading the matches from a website or loading them from a saved database.
        /// </summary>
        private Dictionary<string, List<int>> GetRawIcw(Dictionary<string, int> matchIndexes)
        {
            // Arbitrary data that happens to give reasonable results for this sample data.
            // Icws are directional. If A matches B, then both pairs (A, B) and (B, A) should be specified.
            // An exception is for matches under 20 cM, where only one direction is reported by Ancestry.
            var rawIcw = new List<(string MatchId, string IcwId)>
            {
                ("aaaaa", "bbbbb"),
                ("aaaaa", "ccccc"),
                ("aaaaa", "ddddd"),
                ("aaaaa", "eeeee"),
                ("aaaaa", "fffff"),
                ("bbbbb", "ddddd"),
                ("bbbbb", "fffff"),
                ("ccccc", "aaaaa"),
                ("ccccc", "eeeee"),
                ("ddddd", "bbbbb"),
                ("ddddd", "fffff"),
                ("eeeee", "aaaaa"),
                ("eeeee", "eeeee"),
                ("fffff", "bbbbb"),
                ("fffff", "ddddd"),
            };

            // Make sure that the all of the self-matches are included in the dictionary
            rawIcw = rawIcw.Concat(matchIndexes.Keys.Select(id => (id, id))).Distinct().ToList();

            return rawIcw
                .GroupBy(icw => icw.MatchId, icw => icw.IcwId)
                .ToDictionary
                (
                    g => g.Key,
                    g => g.Select(id => matchIndexes.TryGetValue(id, out var index) ? index : -1)
                        .Where(i => i >= 0)
                        .OrderBy(i => i)
                        .ToList()
                );
        }

        /// <summary>
        /// TestGuid values must be unique across all matches.
        /// </summary>
        /// <param name=""></param>
        private void ValidateMatches(List<Match> matches)
        {
            if (matches.Select(match => match.TestGuid).GroupBy(id => id).Any(g => g.Count() > 1))
            {
                throw new Exception("Duplicate test IDs found!");
            }
        }

        /// <summary>
        /// Get some matches. This would normally be implemented by downloading the matches from a website or loading them from a saved database.
        /// </summary>
        private List<Match> GetMatches()
        {
            return new List<Match>
            {
                new Match{ MatchTestDisplayName = "Joe Smith", TestGuid = "aaaaa", SharedCentimorgans = 1843.1 },
                new Match{ MatchTestDisplayName = "Mary Jones", TestGuid = "bbbbb", SharedCentimorgans = 672.2 },
                new Match{ MatchTestDisplayName = "Bill Rogers", TestGuid = "ccccc", SharedCentimorgans = 400.4 },
                new Match{ MatchTestDisplayName = "Liz Washington", TestGuid = "ddddd", SharedCentimorgans = 278 },
                new Match{ MatchTestDisplayName = "Frank Lincoln", TestGuid = "eeeee", SharedCentimorgans = 149 },
                new Match{ MatchTestDisplayName = "Ruth Grant", TestGuid = "fffff", SharedCentimorgans = 122 },
            };
        }
    }
}
