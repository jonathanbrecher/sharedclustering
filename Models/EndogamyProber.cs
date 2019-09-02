using AncestryDnaClustering.ViewModels;
using System;
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

        public async Task ProbeAsync(string name, string guid, int matchIndexTarget, int numMatchesToTest, Throttle throttle, ProgressData progressData)
        {
            var shiftKeyDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            var pageNum = matchIndexTarget / _matchesRetriever.MatchesPerPage + 1;
            var matches = await _matchesRetriever.GetMatchesPageAsync(guid, pageNum, false, throttle, progressData);
            var icwTasks = matches
                .Take(numMatchesToTest)
                .Select(match => _matchesRetriever.GetRawMatchesInCommonAsync(guid, match.TestGuid, 1, 0, throttle))
                .ToList();
            var icws = await Task.WhenAll(icwTasks);
            var orderedIcwCounts = icws.Select(l => l.Count).OrderBy(c => c).ToList();
            var medianIcwCount = orderedIcwCounts.Skip(orderedIcwCounts.Count() / 2).FirstOrDefault();

            string primaryMessage;
            string secondaryMessage;

            if (medianIcwCount <= 20)
            {
                // Median 0-20: No endogamy
                primaryMessage = $"The test results for {name} do not show any significant endogamy. ";
                secondaryMessage = "Clustering should work well.";
            }
            else if (medianIcwCount < _matchesRetriever.MatchesPerPage * 3 / 4)
            {
                // Median 20-150: Some endogamy but probably not all lines
                primaryMessage = $"The test results for {name} may show some endogamy in some ancestral lines. ";
                secondaryMessage = "Clustering should work well for the ancestral lines without endogamy, but may be difficult to interpret for others.";
            }
            else
            {
                // Median 150-200: Heavy endogamy
                primaryMessage = $"The test results for {name} show significant endogamy. ";
                secondaryMessage = "Clustering by itself may not work well. Considering using the 'Endogamy special' downloading option combined with Similarity to find more distant matches.";
            }

            if (shiftKeyDown)
            {
                secondaryMessage = secondaryMessage + Environment.NewLine + Environment.NewLine + string.Join(", ", orderedIcwCounts);
            }

            MessageBox.Show(primaryMessage + Environment.NewLine + Environment.NewLine + secondaryMessage,
                "Endogamy test", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
