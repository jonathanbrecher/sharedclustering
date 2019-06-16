using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

        public string Header { get; } = "Download";

        public ProgressData ProgressData { get; } = new ProgressData();

        public AncestryDnaDownloadingViewModel(Action<string> continueInClusterTab)
        {
            // Ancestry's security works by setting some cookies in the browser when someone signs in.
            // The CookieContainer captures those cookies when they are set, and adds them to subsequent requests.
            var cookies = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = cookies };
            var ancestryClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.ancestry.com"), Timeout = TimeSpan.FromMinutes(5) };

            _loginHelper = new AncestryLoginHelper(ancestryClient, cookies);
            _testsRetriever = new AncestryTestsRetriever(ancestryClient);
            _matchesRetriever = new AncestryMatchesRetriever(ancestryClient);

            SignInCommand = new RelayCommand<PasswordBox>(async password => await SignInAsync(password));
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

            try
            {
                Tests = (await _loginHelper.LoginAsync(AncestryUserName.Trim(), password.Password)) 
                    ? await _testsRetriever.GetTestsAsync()
                    : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to sign in to Ancestry {Environment.NewLine}{Environment.NewLine}{ex.Message}", "Sign in failure", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var counter = 0;

                var icwDictionary = matches.ToDictionary(
                    match => match.TestGuid,
                    match =>
                    {
                        var index = Interlocked.Increment(ref counter);
                        var result = _matchesRetriever.GetMatchesInCommonAsync(guid, match, MinSharedMatchesCentimorgansToRetrieve, throttle, index, ProgressData);
                        return result;
                    });
                await Task.WhenAll(icwDictionary.Values);

                // Save the downloaded data to disk.
                ProgressData.Reset("Saving data...");

                var icw = icwDictionary.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Result.Keys
                        .Select(matchName =>
                            matchIndexes.TryGetValue(matchName, out var matchIndex) ? matchIndex : (int?)null).Where(index => index != null)
                                .Select(index => index.Value)
                                .Concat(new[] { matchIndexes[kvp.Key] })
                                .ToList()
                            );

                var output = new Serialized { TestTakerTestId = guid, Matches = matches, MatchIndexes = matchIndexes, Icw = icw };
                FileUtils.WriteAsJson(fileName, output, false);
                LastFileDownloaded = fileName;

                var matchesWithSharedMatches = output.Icw.Where(match => match.Value.Count > 1).ToList();
                var averageSharedMatches = matchesWithSharedMatches.Sum(match => match.Value.Count - 1) / (double)matchesWithSharedMatches.Count;
                ProgressData.Reset(DateTime.Now - startTime, $"Done. Downloaded {matches.Count} matches ({matchesWithSharedMatches.Count} with shared matches, averaging {averageSharedMatches:0.#} shared matches)");
            }
            catch (Exception)
            {
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
