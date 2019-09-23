using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using Microsoft.Win32;

namespace AncestryDnaClustering.ViewModels
{
    internal class AncestryDnaDownloadingViewModel : ObservableObject
    {
        private readonly AncestryLoginHelper _loginHelper;
        private readonly AncestryTestsRetriever _testsRetriever;
        private readonly AncestryMatchesRetriever _matchesRetriever;
        private readonly EndogamyProber _endogamyProber;

        public string Header { get; } = "Download";

        public ProgressData ProgressData { get; } = new ProgressData();

        public AncestryDnaDownloadingViewModel(
            AncestryLoginHelper loginHelper,
            AncestryTestsRetriever testsRetriever,
            AncestryMatchesRetriever matchesRetriever,
            EndogamyProber endogamyProber,
            Action<string> continueInClusterTab)
        {
            _loginHelper = loginHelper;
            _testsRetriever = testsRetriever;
            _matchesRetriever = matchesRetriever;
            _endogamyProber = endogamyProber;

            SignInCommand = new RelayCommand<PasswordBox>(async password => await SignInAsync(password));
            CheckEndogamyCommand = new RelayCommand(async () => await CheckEndogamyAsync());
            GetDnaMatchesCommand = new RelayCommand(async () => await GetDnaMatchesAsync());
            ContinueInClusterTabCommand = new RelayCommand(() => continueInClusterTab(LastFileDownloaded));

            AncestryUserName = Settings.Default.AncestryUserName;
            MinCentimorgansToRetrieve = Settings.Default.MinCentimorgansToRetrieve;
            MinSharedMatchesCentimorgansToRetrieve = Settings.Default.MinSharedMatchesCentimorgansToRetrieve;
            ShowAdvancedDownloadOptions = Settings.Default.ShowAdvancedDownloadOptions;
            DownloadTypeFast = Settings.Default.DownloadTypeFast;
            DownloadTypeComplete = Settings.Default.DownloadTypeComplete;
            DownloadTypeEndogamy = Settings.Default.DownloadTypeEndogamy;
            LastFileDownloaded = Settings.Default.LastFileDownloaded;
        }

        public ICommand SignInCommand { get; }
        public ICommand GetDnaMatchesCommand { get; }
        public ICommand CheckEndogamyCommand { get; }
        public ICommand ContinueInClusterTabCommand { get; }

        // The user name for the account to use. This value is saved and will be restored when the application is relaunched.
        // For security, the password is not saved.
        private string _ancestryUserName;
        public string AncestryUserName
        {
            get => _ancestryUserName;
            set
            {
                if (SetFieldValue(ref _ancestryUserName, value, nameof(AncestryUserName)))
                {
                    Settings.Default.AncestryUserName = AncestryUserName;
                    CanSignIn = !string.IsNullOrWhiteSpace(AncestryUserName);
                }
            }
        }

        // A non-empty username is needed.
        private bool _canSignIn;
        public bool CanSignIn
        {
            get => _canSignIn;
            set => SetFieldValue(ref _canSignIn, value, nameof(CanSignIn));
        }

        // All of the tests (test ID and test taker name) available to the signed-in account.
        private Dictionary<string, string> _tests;
        public Dictionary<string, string> Tests
        {
            get => _tests;
            set
            {
                if (SetFieldValue(ref _tests, value, nameof(Tests)))
                {
                    if (Tests?.Count > 0)
                    {
                        // The selected test is the first one that matches the last-used value, otherwise the first one.
                        // The tests are ordered in the same order as in the Ancestry web site, with test taker's own test listed first.
                        SelectedTest = Tests?.Any(test => test.Key == Settings.Default.SelectedTestId) == true
                               ? Tests.FirstOrDefault(test => test.Key == Settings.Default.SelectedTestId)
                               : Tests.First();
                    }
                    else
                    {
                        SelectedTest = new KeyValuePair<string, string>();
                    }
                }
            }
        }

        // The test whose results will be downloaded.
        private KeyValuePair<string, string> _selectedTest;
        public KeyValuePair<string, string> SelectedTest
        {
            get => _selectedTest;
            set
            {
                if (SetFieldValue(ref _selectedTest, value, nameof(SelectedTest)))
                {
                    Settings.Default.SelectedTestId = SelectedTest.Key;
                    CheckCanGetDnaMatches();
                    CheckCanCheckEndogamy();

                    // When the selected test is changed, for convenience report the number of matches in that test.
                    // Stop any previous task that was downloading match counts from a previous test.
                    _matchCountsCancellationTokenSource.Cancel();
                    _matchCountsCancellationTokenSource = new CancellationTokenSource();
                    GetMatchCounts(SelectedTest.Value, _matchCountsCancellationTokenSource.Token);
                }
            }
        }

        private MatchCounts _matchCountsData;

        private CancellationTokenSource _matchCountsCancellationTokenSource = new CancellationTokenSource();
        private async void GetMatchCounts(string guid, CancellationToken cancellationToken)
        {
            try
            {
                MatchCounts = null;
                _matchCountsData = await _matchesRetriever.GetMatchCounts(guid);
                CheckCanGetDnaMatches();
                CheckCanCheckEndogamy();
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

        private bool CheckCanGetDnaMatches() => CanGetDnaMatches = Tests?.Count > 0 && MinCentimorgansToRetrieve > 0 && _matchCountsData?.TotalMatches > 0;

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
                        MinSharedMatchesCentimorgansToRetrieve = 50;
                    }
                }
            }
        }

        private async Task SignInAsync(PasswordBox password)
        {
            Settings.Default.Save();

            // Try primary site, and capture error if any.
            var errorMessage = await SignInAsync(password, "www.ancestry.com");
            if (errorMessage == null)
            {
                return;
            }

            // If not able to sign into the main Ancestry site, try some backups.
            foreach (var alternateHost in new[] { "www.ancestry.com.au", "www.ancestry.co.uk" })
            {
                if (await SignInAsync(password, alternateHost) == null)
                {
                    MessageBox.Show($"Using backup login at {alternateHost}", "Sign in success", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            // Show error message from primary login failure if none of the backups worked.
            MessageBox.Show(errorMessage, "Sign in failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task<string> SignInAsync(PasswordBox password, string hostOverride)
        {
            try
            {
                if (await _loginHelper.LoginAsync(AncestryUserName.Trim(), password.Password, hostOverride))
                {
                    Tests = await _testsRetriever.GetTestsAsync();
                    return null;
                }
                else
                {
                    return $"Unable to sign in to Ancestry";
                }
            }
            catch (Exception ex)
            {
                return $"Unable to sign in to Ancestry {Environment.NewLine}{Environment.NewLine}{ex.Message}";
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

                    if (DownloadTypeFast && MinSharedMatchesCentimorgansToRetrieve != 20)
                    {
                        DownloadTypeFast = false;
                    }
                    if (DownloadTypeComplete && MinSharedMatchesCentimorgansToRetrieve != 6)
                    {
                        DownloadTypeComplete = false;
                    }
                    if (DownloadTypeEndogamy && MinSharedMatchesCentimorgansToRetrieve != 50)
                    {
                        DownloadTypeEndogamy = false;
                    }
                }
            }
        }

        private bool _canCheckEndogamy;
        public bool CanCheckEndogamy
        {
            get => _canCheckEndogamy;
            set => SetFieldValue(ref _canCheckEndogamy, value, nameof(CanCheckEndogamy));
        }

        private bool CheckCanCheckEndogamy() => CanCheckEndogamy = Tests?.Count > 0 && _matchCountsData?.TotalMatches > 0;

        private async Task CheckEndogamyAsync()
        {
            try
            {
                CanCheckEndogamy = false;
                Mouse.OverrideCursor = Cursors.Wait;
                var throttle = new Throttle(50);
                var numMatchesToTest = 10;
                await _endogamyProber.ProbeAsync(SelectedTest.Key, SelectedTest.Value, _matchCountsData.FourthCousins, numMatchesToTest, throttle, ProgressData.SuppressProgress);
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

            var fileName = $"{SelectedTest.Key} Ancestry Shared Clustering.txt";
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                FileName = fileName,
                DefaultExt = ".txt",
                Filter = "Text|*.txt",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
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

                var guid = SelectedTest.Value;

                // Make sure there are no more than 50 concurrent HTTP requests, to avoid overwhelming the Ancestry web site.
                var throttle = new Throttle(50);

                // First download a list of all matches available in the test.
                // This is the data shown 50-at-a-time in list view on the Ancestry web site.
                var numMatchesToRetrieve =
                    MinCentimorgansToRetrieve >= 90 ? _matchCountsData.ThirdCousins
                    : MinCentimorgansToRetrieve >= 20 ? _matchCountsData.FourthCousins
                    : _matchCountsData.TotalMatches;
                ProgressData.Reset("Downloading matches...", numMatchesToRetrieve);
                var matches = await _matchesRetriever.GetMatchesAsync(guid, numMatchesToRetrieve, true, throttle, ProgressData);

                // Make sure there are no duplicates among the matches
                matches = matches
                    .Where(match => match.SharedCentimorgans >= MinCentimorgansToRetrieve)
                    .GroupBy(match => match.TestGuid)
                    .Select(g => g.First())
                    .ToList();

                var matchIndexes = matches
                    .Select((match, index) => new { match.TestGuid, Index = index })
                    .ToDictionary(pair => pair.TestGuid, pair => pair.Index);

                // Now download the shared matches for each match.
                // This takes much longer than downloading the list of matches themselves..
                ProgressData.Reset($"Downloading shared matches for {matches.Count} matches...", matches.Count);

                // Don't process more than 50 matches at once. This lets the matches finish processing completely
                // rather than opening requests for all of the matches at onces.
                var matchThrottle = new Throttle(50);

                var counter = 0;

                var icwTasksDictionary = matches.ToDictionary(
                    match => match.TestGuid,
                    async match =>
                    {
                        await matchThrottle.WaitAsync();

                        try
                        {
                            var index = Interlocked.Increment(ref counter);
                            return await _matchesRetriever.GetMatchesInCommonAsync(guid, match, MinSharedMatchesCentimorgansToRetrieve, throttle, matchIndexes, ProgressData);
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

                var output = new Serialized { TestTakerTestId = guid, Matches = matches, MatchIndexes = matchIndexes, Icw = icw };
                FileUtils.WriteAsJson(fileName, output, false);
                LastFileDownloaded = fileName;

                var matchesWithSharedMatches = output.Icw.Where(match => match.Value.Count > 1).ToList();
                var averageSharedMatches = matchesWithSharedMatches.Sum(match => match.Value.Count - 1) / (double)matchesWithSharedMatches.Count;
                ProgressData.Reset(DateTime.Now - startTime, $"Done. Downloaded {matches.Count} matches ({matchesWithSharedMatches.Count} with shared matches, averaging {averageSharedMatches:0.#} shared matches)");
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
