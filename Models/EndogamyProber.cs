using AncestryDnaClustering.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AncestryDnaClustering.Models
{
    internal class EndogamyProber
    {
        private readonly AncestryMatchesRetriever _matchesRetriever;

        public EndogamyProber(AncestryMatchesRetriever matchesRetriever)
        {
            _matchesRetriever = matchesRetriever;
        }

        public async Task ProbeAsync(string name, string guid, int matchIndexTarget, int numMatchesToTest, Throttle throttle, ProgressData progressData)
        {
            var pageNum = matchIndexTarget / _matchesRetriever.MatchesPerPage + 1;
            var matches = await _matchesRetriever.GetMatchesPageAsync(guid, pageNum, false, throttle, progressData);
            var icwTasks = matches
                .Take(numMatchesToTest)
                .Select(match => _matchesRetriever.GetRawMatchesInCommonAsync(guid, match.TestGuid, 1, 0, throttle))
                .ToList();
            var icws = await Task.WhenAll(icwTasks);
            var medianIcwCount = icws.Select(l => l.Count).Skip(icwTasks.Count() / 2).FirstOrDefault();

            if (medianIcwCount <= 20)
            {
                // Median 0-20: No endogamy
                MessageBox.Show($"The test results for {name} do not show any significant endogamy. " +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Clustering should work well.",
                    "Endogamy test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (medianIcwCount < _matchesRetriever.MatchesPerPage * 3 / 4)
            {
                // Median 20-150: Some endogamy but probably not all lines
                MessageBox.Show($"The test results for {name} may show some endogamy in some ancestral lines. " +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Clustering should work well for the ancestral lines without endogamy, but may be difficult to interpret for others.",
                    "Endogamy test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Median 150-200: Heavy endogamy
                MessageBox.Show($"The test results for {name} show significant endogamy. " +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Clustering by itself may not work well. Considering using the 'Endogamy special' downloading option combined with Similarity to find more distant matches.",
                    "Endogamy test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
