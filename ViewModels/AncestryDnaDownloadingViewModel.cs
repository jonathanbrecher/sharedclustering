using System;
using System.Collections.Generic;
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

        public AncestryDnaDownloadingViewModel()
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

            AncestryUserName = Settings.Default.AncestryUserName;
            NumMatchesToRetrieve = Settings.Default.NumTestsToRetrieve;
            HighestSharedMatchToRetrieve = Settings.Default.HighestSharedMatchToRetrieve;
            ShowAdvancedDownloadOptions = Settings.Default.ShowAdvancedDownloadOptions;
            DownloadTypeFast = Settings.Default.DownloadTypeFast;
            DownloadTypeComplete = Settings.Default.DownloadTypeComplete;
        }

        public ICommand SignInCommand { get; }
        public ICommand GetDnaMatchesCommand { get; }

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
                    // The selected test is the first one that matches the last-used value, otherwise the first one.
                    // The tests are ordered in the same order as in the Ancestry web site, with test taker's own test listed first.
                    SelectedTest = Tests.Any(test => test.Key == Settings.Default.SelectedTestId)
                           ? Tests.FirstOrDefault(test => test.Key == Settings.Default.SelectedTestId)
                           : Tests.First();
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
                    CanGetDnaMatches = Tests?.Count > 0 && NumMatchesToRetrieve > 0;

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
                if (!cancellationToken.IsCancellationRequested)
                {
                    MatchCounts = $"{_matchCountsData.ThirdCousins} third cousins, {_matchCountsData.FourthCousins} fourth cousins, {_matchCountsData.TotalMatches} total matches";
                    if (DownloadTypeFast)
                    {
                        NumMatchesToRetrieve = HighestSharedMatchToRetrieve = _matchCountsData.FourthCousins;
                    }
                    if (DownloadTypeComplete)
                    {
                        NumMatchesToRetrieve = HighestSharedMatchToRetrieve = _matchCountsData.TotalMatches;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore any exceptions.
                // This data is provided only for convenience, so there is no harm if an error occurs and it cannot be downloaded.
            }
        }

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
                    if (DownloadTypeFast && _matchCountsData != null)
                    {
                        NumMatchesToRetrieve = HighestSharedMatchToRetrieve = _matchCountsData.FourthCousins;
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
                    if (DownloadTypeComplete && _matchCountsData != null)
                    {
                        NumMatchesToRetrieve = HighestSharedMatchToRetrieve = _matchCountsData.TotalMatches;
                    }
                }
            }
        }

        private async Task SignInAsync(PasswordBox password)
        {
            Settings.Default.Save();

            try
            {
                await _loginHelper.LoginAsync(AncestryUserName.Trim(), password.Password);

                Tests = await _testsRetriever.GetTestsAsync();
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
        private int _numMatchesToRetrieve;
        public int NumMatchesToRetrieve
        {
            get => _numMatchesToRetrieve;
            set
            {
                if (SetFieldValue(ref _numMatchesToRetrieve, value, nameof(NumMatchesToRetrieve)))
                {
                    Settings.Default.NumTestsToRetrieve = NumMatchesToRetrieve;
                    CanGetDnaMatches = Tests?.Count > 0 && NumMatchesToRetrieve > 0;

                    if (DownloadTypeFast && _matchCountsData != null && NumMatchesToRetrieve != _matchCountsData.FourthCousins)
                    {
                        DownloadTypeFast = false;
                    }
                    if (DownloadTypeComplete && _matchCountsData != null && NumMatchesToRetrieve != _matchCountsData.TotalMatches)
                    {
                        DownloadTypeComplete = false;
                    }
                }
            }
        }

        // The index of the highest shared match to retrieve.
        // Typical values are the number of matches with 20 or higher (the lowest values shown on the Ancestry website)
        // and the total count of all matches.
        // This might need to be set to an artificially low number in the presence of endogamy,
        // to avoid ridiculously long download times when each match might have thousands of shared matches.
        private int _highestSharedMatchToRetrieve;
        public int HighestSharedMatchToRetrieve
        {
            get => _highestSharedMatchToRetrieve;
            set
            {
                if (SetFieldValue(ref _highestSharedMatchToRetrieve, value, nameof(HighestSharedMatchToRetrieve)))
                {
                    Settings.Default.HighestSharedMatchToRetrieve = HighestSharedMatchToRetrieve;

                    if (DownloadTypeFast && _matchCountsData != null && HighestSharedMatchToRetrieve != _matchCountsData.FourthCousins)
                    {
                        DownloadTypeFast = false;
                    }
                    if (DownloadTypeComplete && _matchCountsData != null && HighestSharedMatchToRetrieve != _matchCountsData.TotalMatches)
                    {
                        DownloadTypeComplete = false;
                    }
                }
            }
        }

        private async Task GetDnaMatchesAsync()
        {
            Settings.Default.Save();

            var startTime = DateTime.Now;

            var fileName = "icw.txt";
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

            var guid = SelectedTest.Value;

            // Make sure there are no more than 100 concurrent HTTP requests, to avoid overwhelming the Ancestry web site.
            var throttle = new Throttle(10);

            // First download a list of all matches available in the test.
            // This is the data shown 50-at-a-time in list view on the Ancestry web site.
            ProgressData.Reset("Downloading matches...", NumMatchesToRetrieve);
            var matches = await _matchesRetriever.GetMatchesAsync(guid, NumMatchesToRetrieve, true, throttle, ProgressData);

            // Make sure there are no duplicates among the matches
            matches = matches
                .GroupBy(match => match.TestGuid)
                .Select(g => g.First())
                .ToList();

            var matchIndexes = matches
                .Select((match, index) => new { match.TestGuid, Index = index })
                .ToDictionary(pair => pair.TestGuid, pair => pair.Index);

            var minSharedCentimorgans = matches.Take(HighestSharedMatchToRetrieve).Last().SharedCentimorgans;

            // Now download the shared matches for each match.
            // This takes much longer than downloading the list of matches themselves..
            ProgressData.Reset($"Downloading shared matches for {matches.Count} matches...", matches.Count);

            var counter = 0;

            var icwDictionary = matches.ToDictionary(
                match => match.TestGuid,
                match =>
                {
                    var index = Interlocked.Increment(ref counter);
                    var result = _matchesRetriever.GetMatchesInCommonAsync(guid, match, minSharedCentimorgans, throttle, index, ProgressData);
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

            var matchesWithSharedMatches = output.Icw.Where(match => match.Value.Count > 1).ToList();
            var averageSharedMatches = matchesWithSharedMatches.Sum(match => match.Value.Count - 1) / (double)matchesWithSharedMatches.Count;
            ProgressData.Reset(DateTime.Now - startTime, $"Done. Downloaded {matches.Count} matches ({matchesWithSharedMatches.Count} with shared matches, averaging {averageSharedMatches:0.#} shared matches");
        }
    }
}
