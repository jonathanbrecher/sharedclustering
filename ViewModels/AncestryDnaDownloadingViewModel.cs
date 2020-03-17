using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using Microsoft.Win32;

namespace AncestryDnaClustering.ViewModels
{
    internal class AncestryDnaDownloadingViewModel : ObservableObject
    {
        private readonly AncestryMatchesRetriever _matchesRetriever;
        private readonly EndogamyProber _endogamyProber;

        public AncestryDnaSignInViewModel SignInViewModel { get; }
        public string Header { get; } = "Download";

        public ProgressData ProgressData { get; } = new ProgressData();

        public AncestryDnaDownloadingViewModel(
            AncestryDnaSignInViewModel signInViewModel,
            AncestryMatchesRetriever matchesRetriever,
            EndogamyProber endogamyProber,
            Action<string> continueInClusterTab)
        {
            SignInViewModel = signInViewModel;
            _matchesRetriever = matchesRetriever;
            _endogamyProber = endogamyProber;

            SignInViewModel.OnSelectedTestChanged += SelectedTestChanged;

            CheckEndogamyCommand = new RelayCommand(async () => await CheckEndogamyAsync());
            GetDnaMatchesCommand = new RelayCommand(async () => await GetDnaMatchesAsync());
            ContinueInClusterTabCommand = new RelayCommand(() => continueInClusterTab(LastFileDownloaded));

            MinCentimorgansToRetrieve = Settings.Default.MinCentimorgansToRetrieve;
            MinSharedMatchesCentimorgansToRetrieve = Settings.Default.MinSharedMatchesCentimorgansToRetrieve;
            ShowAdvancedDownloadOptions = Settings.Default.ShowAdvancedDownloadOptions;
            DownloadTypeFast = Settings.Default.DownloadTypeFast;
            DownloadTypeComplete = Settings.Default.DownloadTypeComplete;
            DownloadTypeEndogamy = Settings.Default.DownloadTypeEndogamy;
            LastFileDownloaded = Settings.Default.LastFileDownloaded;
        }

        public ICommand GetDnaMatchesCommand { get; }
        public ICommand CheckEndogamyCommand { get; }
        public ICommand ContinueInClusterTabCommand { get; }

        private void SelectedTestChanged(object sender, EventArgs e)
        {
            _matchCountsData = null;
            CheckCanGetDnaMatches();
            CheckCanCheckEndogamy();

            // When the selected test is changed, for convenience report the number of matches in that test.
            // Stop any previous task that was downloading match counts from a previous test.
            _matchCountsCancellationTokenSource.Cancel();
            _matchCountsCancellationTokenSource = new CancellationTokenSource();
            GetMatchCounts(SignInViewModel.SelectedTest?.TestGuid, _matchCountsCancellationTokenSource.Token);
        }

        private MatchCounts _matchCountsData;

        private CancellationTokenSource _matchCountsCancellationTokenSource = new CancellationTokenSource();
        private async void GetMatchCounts(string guid, CancellationToken cancellationToken)
        {
            try
            {
                MatchCounts = null;
                if (guid == null)
                {
                    return;
                }

                _matchCountsData = await _matchesRetriever.GetMatchCounts(guid);
                if (DownloadTypeEndogamy)
                {
                    MinSharedMatchesCentimorgansToRetrieve = _matchCountsData?.FourHundredthCentimorgans ?? 50;
                }
                CheckCanGetDnaMatches();
                CheckCanCheckEndogamy();
                CheckNoSharedMatches();
                if (!cancellationToken.IsCancellationRequested)
                {
                    MatchCounts = $"{_matchCountsData.ThirdCousins} third cousins, {_matchCountsData.FourthCousins} fourth cousins, {_matchCountsData.TotalMatches} total matches";
                }
            }
            catch (Exception)
            {
                // Ignore any exceptions.
                // This data is provided only for convenience, so there is no harm if an error occurs and it cannot be downloaded.
            }
        }

        private bool CheckCanGetDnaMatches() => CanGetDnaMatches = SignInViewModel.Tests?.Count > 0 && MinCentimorgansToRetrieve > 0 && _matchCountsData?.TotalMatches > 0;

        // A user-visible string that describes how many matches are available in the currently-selected test.
        private string _matchCounts;
        public string MatchCounts
        {
            get => _matchCounts;
            set => SetFieldValue(ref _matchCounts, value, nameof(MatchCounts));
        }

        // Whether the advanced options are visible.
        private bool _showAdvancedDownloadOptions;
        public bool ShowAdvancedDownloadOptions
        {
            get => _showAdvancedDownloadOptions;
            set
            {
                if (SetFieldValue(ref _showAdvancedDownloadOptions, value, nameof(ShowAdvancedDownloadOptions)))
                {
                    Settings.Default.ShowAdvancedDownloadOptions = ShowAdvancedDownloadOptions;
                }
            }
        }

        private bool _downloadTypeFast;
        public bool DownloadTypeFast
        {
            get => _downloadTypeFast;
            set
            {
                if (SetFieldValue(ref _downloadTypeFast, value, nameof(DownloadTypeFast)))
                {
                    Settings.Default.DownloadTypeFast = DownloadTypeFast;
                    if (DownloadTypeFast)
                    {
                        MinCentimorgansToRetrieve = MinSharedMatchesCentimorgansToRetrieve = 20;
                    }
                }
            }
        }

        private bool _downloadTypeComplete;
        public bool DownloadTypeComplete
        {
            get => _downloadTypeComplete;
            set
            {
                if (SetFieldValue(ref _downloadTypeComplete, value, nameof(DownloadTypeComplete)))
                {
                    Settings.Default.DownloadTypeComplete = DownloadTypeComplete;
                    if (DownloadTypeComplete)
                    {
                        MinCentimorgansToRetrieve = MinSharedMatchesCentimorgansToRetrieve = 6;
                    }
                }
            }
        }

        private bool _downloadTypeEndogamy;
        public bool DownloadTypeEndogamy
        {
            get => _downloadTypeEndogamy;
            set
            {
                if (SetFieldValue(ref _downloadTypeEndogamy, value, nameof(DownloadTypeEndogamy)))
                {
                    Settings.Default.DownloadTypeEndogamy = DownloadTypeEndogamy;
                    if (DownloadTypeEndogamy)
                    {
                        MinCentimorgansToRetrieve = 6;
                        MinSharedMatchesCentimorgansToRetrieve = _matchCountsData?.FourHundredthCentimorgans ?? 50;
                    }
                }
            }
        }

        private bool _canGetDnaMatches;
        public bool CanGetDnaMatches
        {
            get => _canGetDnaMatches;
            set => SetFieldValue(ref _canGetDnaMatches, value, nameof(CanGetDnaMatches));
        }

        // The total number of matches to retrieve.
        // Typical values are the number of matches with 20 or higher (the lowest values shown on the Ancestry website)
        // and the total count of all matches.
        private double _minCentimorgansToRetrieve;
        public double MinCentimorgansToRetrieve
        {
            get => _minCentimorgansToRetrieve;
            set
            {
                if (SetFieldValue(ref _minCentimorgansToRetrieve, value, nameof(MinCentimorgansToRetrieve)))
                {
                    Settings.Default.MinCentimorgansToRetrieve = MinCentimorgansToRetrieve;
                    CheckCanGetDnaMatches();

                    if (DownloadTypeFast && MinCentimorgansToRetrieve != 20)
                    {
                        DownloadTypeFast = false;
                    }
                    if (DownloadTypeComplete && MinCentimorgansToRetrieve != 6)
                    {
                        DownloadTypeComplete = false;
                    }
                    if (DownloadTypeEndogamy && MinCentimorgansToRetrieve != 6)
                    {
                        DownloadTypeEndogamy = false;
                    }
                }
            }
        }

        // The index of the highest shared match to retrieve.
        // Typical values are the number of matches with 20 or higher (the lowest values shown on the Ancestry website)
        // and the total count of all matches.
        // This might need to be set to an artificially low number in the presence of endogamy,
        // to avoid ridiculously long download times when each match might have thousands of shared matches.
        private double _minSharedMatchesCentimorgansToRetrieve;
        public double MinSharedMatchesCentimorgansToRetrieve
        {
            get => _minSharedMatchesCentimorgansToRetrieve;
            set
            {
                if (SetFieldValue(ref _minSharedMatchesCentimorgansToRetrieve, value, nameof(MinSharedMatchesCentimorgansToRetrieve)))
                {
                    Settings.Default.MinSharedMatchesCentimorgansToRetrieve = MinSharedMatchesCentimorgansToRetrieve;
                    CheckNoSharedMatches();

                    if (DownloadTypeFast && MinSharedMatchesCentimorgansToRetrieve != 20)
                    {
                        DownloadTypeFast = false;
                    }
                    if (DownloadTypeComplete && MinSharedMatchesCentimorgansToRetrieve != 6)
                    {
                        DownloadTypeComplete = false;
                    }
                    if (DownloadTypeEndogamy && MinSharedMatchesCentimorgansToRetrieve != (_matchCountsData?.FourHundredthCentimorgans ?? 50))
                    {
                        DownloadTypeEndogamy = false;
                    }
                }
            }
        }

        private bool _noSharedMatches;
        public bool NoSharedMatches
        {
            get => _noSharedMatches;
            set => SetFieldValue(ref _noSharedMatches, value, nameof(NoSharedMatches));
        }

        private void CheckNoSharedMatches()
            => NoSharedMatches = MinSharedMatchesCentimorgansToRetrieve > (_matchCountsData?.HighestCentimorgans ?? 4000);

        private bool _canCheckEndogamy;
        public bool CanCheckEndogamy
        {
            get => _canCheckEndogamy;
            set => SetFieldValue(ref _canCheckEndogamy, value, nameof(CanCheckEndogamy));
        }

        private bool CheckCanCheckEndogamy() => CanCheckEndogamy = SignInViewModel.Tests?.Count > 0 && _matchCountsData?.TotalMatches > 0;

        private async Task CheckEndogamyAsync()
        {
            try
            {
                CanCheckEndogamy = false;
                Mouse.OverrideCursor = Cursors.Wait;
                var throttle = new Throttle(50);
                var numMatchesToTest = 10;
                await _endogamyProber.ProbeAsync(SignInViewModel.SelectedTest.DisplayName, SignInViewModel.SelectedTest.TestGuid, _matchCountsData.FourthCousins, numMatchesToTest, MatchCounts, throttle, ProgressData);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                CheckCanCheckEndogamy();
            }
        }

        private async Task GetDnaMatchesAsync()
        {
            Settings.Default.Save();

            var startTime = DateTime.Now;

            var fileName = $"{SignInViewModel.SelectedTest.DisplayName} Ancestry Shared Clustering.txt";
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = FileUtils.GetDefaultDirectory(null),
                FileName = fileName,
                DefaultExt = ".txt",
                Filter = "Text|*.txt",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                Settings.Default.LastUsedDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                Settings.Default.Save();
                fileName = saveFileDialog.FileName;
            }
            else
            {
                return;
            }

            try
            {
                CanGetDnaMatches = false;
                LastFileDownloaded = null;

                var guid = SignInViewModel.SelectedTest.TestGuid;

                // Make sure there are no more than 50 concurrent HTTP requests, to avoid overwhelming the Ancestry web site.
                var throttle = new Throttle(50);

                // First download a list of all matches available in the test.
                // This is the data shown 50-at-a-time in list view on the Ancestry web site.
                var numMatchesToRetrieve =
                    MinCentimorgansToRetrieve >= 90 ? _matchCountsData.ThirdCousins
                    : MinCentimorgansToRetrieve >= 20 ? _matchCountsData.FourthCousins
                    : _matchCountsData.TotalMatches;
                ProgressData.Reset("Downloading matches...", numMatchesToRetrieve);
                var tags = await _matchesRetriever.GetTagsAsync(guid, throttle);
                var tagIds = new HashSet<int>(tags.Select(tag => tag.TagId));
                var matches = await _matchesRetriever.GetMatchesAsync(guid, numMatchesToRetrieve, tagIds, true, throttle, ProgressData);

                // Make sure there are no duplicates among the matches
                matches = matches
                    .Where(match => match.SharedCentimorgans >= MinCentimorgansToRetrieve)
                    .GroupBy(match => match.TestGuid)
                    .Select(g => g.First())
                    .ToList();

                var matchIndexes = matches
                    .Select((match, index) => new { match.TestGuid, Index = index })
                    .ToDictionary(pair => pair.TestGuid, pair => pair.Index);

                // Some matches might not need more data downloaded
                var matchesNeedingMoreDataDownloaded = matches
                    .Where(match => _matchesRetriever.WillGetMatchesInCommon(match, NoSharedMatches))
                    .ToList();

                // Now download the shared matches for each match.
                // This takes much longer than downloading the list of matches themselves..
                ProgressData.Reset($"Downloading {(NoSharedMatches ? "per-match data" : "shared matches")} for {matches.Count} matches...", matchesNeedingMoreDataDownloaded.Count);

                // Don't process more than 50 matches at once. This lets the matches finish processing completely
                // rather than opening requests for all of the matches at onces.
                var matchThrottle = new Throttle(50);

                var counter = 0;

                var icwTasksDictionary = matchesNeedingMoreDataDownloaded.ToDictionary(
                    match => match.TestGuid,
                    async match =>
                    {
                        await matchThrottle.WaitAsync();

                        try
                        {
                            var index = Interlocked.Increment(ref counter);
                            return await _matchesRetriever.GetMatchesInCommonAsync(guid, match, NoSharedMatches, MinSharedMatchesCentimorgansToRetrieve, throttle, matchIndexes, false, ProgressData);
                        }
                        finally
                        {
                            matchThrottle.Release();
                        }
                    });
                await Task.WhenAll(icwTasksDictionary.Values);

                // Save the downloaded data to disk.
                ProgressData.Reset("Saving data...");

                var icw = icwTasksDictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result);

                // Make sure that each match at least matches itself
                foreach (var matchIndex in matchIndexes.Where(kvp => !icw.ContainsKey(kvp.Key)))
                {
                    icw[matchIndex.Key] = new List<int> { matchIndex.Value };
                }

                var output = new Serialized { TestTakerTestId = guid, Tags = tags, Matches = matches, MatchIndexes = matchIndexes, Icw = icw };
                FileUtils.WriteAsJson(fileName, output, false);
                LastFileDownloaded = fileName;

                var matchesWithSharedMatches = output.Icw.Where(match => match.Value.Count > 1).ToList();
                var averageSharedMatches = matchesWithSharedMatches.Count == 0 ? 0 : matchesWithSharedMatches.Sum(match => match.Value.Count - 1) / (double)matchesWithSharedMatches.Count;
                ProgressData.Reset(DateTime.Now - startTime, $"Done. Downloaded {matches.Count} matches over {MinCentimorgansToRetrieve} cM ({matchesWithSharedMatches.Count} with shared matches, averaging {averageSharedMatches:0.#} shared matches over {MinSharedMatchesCentimorgansToRetrieve} cM)");
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, true);
                ProgressData.Reset();
            }
            finally
            {
                CheckCanGetDnaMatches();
            }
        }

        private bool _continueInClusterTabVisible;
        public bool ContinueInClusterTabVisible
        {
            get => _continueInClusterTabVisible;
            set => SetFieldValue(ref _continueInClusterTabVisible, value, nameof(ContinueInClusterTabVisible));
        }

        private bool CanContinueInClusterTab => !string.IsNullOrWhiteSpace(LastFileDownloaded) && File.Exists(LastFileDownloaded);

        private string _lastFileDownloaded;
        public string LastFileDownloaded
        {
            get => _lastFileDownloaded;
            set
            {
                if (SetFieldValue(ref _lastFileDownloaded, value, nameof(LastFileDownloaded)))
                {
                    Settings.Default.LastFileDownloaded = LastFileDownloaded;
                    ContinueInClusterTabVisible = CanContinueInClusterTab;
                }
            }
        }
    }
}
