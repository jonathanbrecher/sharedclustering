using SharedClustering.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AncestryDnaClustering.Models
{
    internal class EndogamyProber
    {
        private readonly AncestryMatchesRetriever _matchesRetriever;

        public EndogamyProber(AncestryMatchesRetriever matchesRetriever)
        {
            _matchesRetriever = matchesRetriever;
        }

        public async Task ProbeAsync(string name, string guid, int matchIndexTarget, int numMatchesToTest, string matchCounts, Throttle throttle, IProgressData progressData)
        {
            progressData.Reset("Checking endogamy...", numMatchesToTest + 2);

            try
            {
                var shiftKeyDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                var pageNum = matchIndexTarget / _matchesRetriever.MatchesPerPage + 1;
                var maxIndex = pageNum * _matchesRetriever.MatchesPerPage + numMatchesToTest;
                var matches = await _matchesRetriever.GetMatchesPageAsync(guid, new HashSet<int>(), pageNum, false, throttle, progressData);

                var matchesToTest = matches.Take(numMatchesToTest).ToList();
                var minCentimorgans = matchesToTest.Min(match => match.SharedCentimorgans);
                var icwTasks = matchesToTest
                    .Select(async match =>
                    {
                        try
                        {
                            return await _matchesRetriever.GetRawMatchesInCommonAsync(guid, match.TestGuid, pageNum, minCentimorgans, true, throttle);
                        }
                        catch
                        {
                            return null;
                        }
                        finally
                        {
                            progressData.Increment();
                        }
                    })
                    .ToList();
                var icws = await Task.WhenAll(icwTasks);

                if (icws.Any(icw => icw == null))
                {
                    MessageBox.Show("Unable to retrieve match data. Please try again in a few minutes.", "Endogamy test", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var orderedIcwCounts = icws.Select(l => l.Count).OrderBy(c => c).ToList();
                var medianIcwCount = orderedIcwCounts.Skip(orderedIcwCounts.Count() / 2).FirstOrDefault();
                var sharedMatchRatios = icws.Select(l => l.Where(m => m.SharedCentimorgans >= minCentimorgans).Count() / (double)maxIndex).OrderBy(c => c).ToList();
                var reportableSharedMatchRatio = sharedMatchRatios
                    .Take((int)(sharedMatchRatios.Count() * 0.9)) // Discard top 10%
                    .Skip(sharedMatchRatios.Count() / 2) // Discard lowest 50%
                    .DefaultIfEmpty(0)
                    .Average();

                string primaryMessage;
                string secondaryMessage;

                if (reportableSharedMatchRatio <= 0.05)
                {
                    // Shared match ratio under 5%
                    primaryMessage = $"The test results for {name} do not show any significant endogamy.{Environment.NewLine}(Shared match frequency estimate: {reportableSharedMatchRatio:P0})";
                    secondaryMessage = "Clustering should work well.";
                }
                else if (medianIcwCount < _matchesRetriever.MatchesPerPage * 3 / 4)
                {
                    // Median icw count 20-150: Some endogamy but probably not all lines
                    primaryMessage = $"The test results for {name} may show some endogamy in some ancestral lines.{Environment.NewLine}(Shared match frequency estimate: {reportableSharedMatchRatio:P0})";
                    secondaryMessage = "Clustering should work well for the ancestral lines without endogamy, but may be difficult to interpret for others.";
                }
                else if (reportableSharedMatchRatio <= 0.20)
                {
                    // Shared match ratio 5% - 20%
                    primaryMessage = $"The test results for {name} show significant endogamy.{Environment.NewLine}(Shared match frequency estimate: {reportableSharedMatchRatio:P0})";
                    secondaryMessage = "Clustering by itself may not work well. Considering using the 'Endogamy special' downloading option combined with Similarity to find more distant matches.";
                }
                else
                {
                    // Shared match ratio over 20%
                    primaryMessage = $"The test results for {name} show extreme endogamy.{Environment.NewLine}(Shared match frequency estimate: {reportableSharedMatchRatio:P0})";
                    secondaryMessage = "Clustering and Similarity analyses will almost certainly not work well for these test results.";
                }

                if (shiftKeyDown)
                {
                    secondaryMessage = secondaryMessage + Environment.NewLine + Environment.NewLine + string.Join(", ", orderedIcwCounts);
                }

                MessageBox.Show(primaryMessage + Environment.NewLine + Environment.NewLine + secondaryMessage + Environment.NewLine + Environment.NewLine + matchCounts,
                    "Endogamy test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                progressData.Reset();
            }
        }
    }
}
