using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using SharedClustering.Core.Anonymizers;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// Main ViewModel for the application.
    /// </summary>
    internal class AncestryDnaToolsViewModel : ObservableObject
    {
        public AncestryDnaToolsViewModel()
        {
            // Ancestry's security works by setting some cookies in the browser when someone signs in.
            // The CookieContainer captures those cookies when they are set, and adds them to subsequent requests.
            var cookies = new CookieContainer { PerDomainCapacity = 100 };
            var handler = new HttpClientHandler 
            { 
                CookieContainer = cookies,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            var ancestryClients = new[] { "https://www.ancestry.com", "https://www.ancestry.com.au", "https://www.ancestry.co.uk", "https://www.ancestry.it" }
                .ToDictionary(url => url, url =>
                {
                    var httpClient = new HttpClient(handler)
                    {
                        BaseAddress = new Uri(url),
                        Timeout = TimeSpan.FromSeconds(15),
                    };
                    //httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    //httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                    //httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                    httpClient.DefaultRequestHeaders.Add("User-Agent", WindowTitle);
                    //httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
                    //httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                    //httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    //httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    //httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    //httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    return httpClient;
                });

            var loginHelper = new AncestryLoginHelper(ancestryClients, cookies, this);
            var testsRetriever = new AncestryTestsRetriever(loginHelper);
            var matchesRetriever = new AncestryMatchesRetriever(loginHelper);
            var endogamyProber = new EndogamyProber(matchesRetriever);

            var serializedMatchesReaders = new List<ISerializedMatchesReader>
            {
                new DnaGedcomAncestryMatchesReader(),
                new DnaGedcomFtdnaMatchesReader(),
                new DnaGedcomMyHeritageMatchesReader(),
                new SharedClusteringExcelMatchesReader(),
                new SharedClusteringMatchesReader(),
                new AutoClusterCsvMatchesReader(),
                new AutoClusterExcelMatchesReader(),
            };

            var matchesLoader = new MatchesLoader(serializedMatchesReaders);

            var signInViewModel = new AncestryDnaSignInViewModel(loginHelper, testsRetriever);

            // Extendable list of tabs to display.
            Tabs = new List<object>
            {
                new IntroductionViewModel(),
                new AncestryDnaDownloadingViewModel(signInViewModel, matchesRetriever, endogamyProber, OpenInClusterTab),
                new AncestryDnaHierarchicalClusteringViewModel(matchesLoader, new Anonymizer()),
                new AncestryDnaSimilarityViewModel(matchesLoader),
                new AncestryDnaExportViewModel(matchesLoader),
                new AncestryDnaUploadNotesViewModel(signInViewModel, new AncestryNotesUpdater(matchesRetriever)),
            };
            SelectedTabIndex = Settings.Default.SelectedTabIndex;
        }

        private void OpenInClusterTab(string fileToCluster)
        {
            var clusteringTab = Tabs.OfType<AncestryDnaHierarchicalClusteringViewModel>().FirstOrDefault();
            if (clusteringTab != null)
            {
                clusteringTab.Filename = fileToCluster;
                clusteringTab.SetDefaultFileName(fileToCluster);
                SelectedTabIndex = Tabs.IndexOf(clusteringTab);
            }
        }

        public ICommand WindowClosingCommand { get; } = new RelayCommand(() => Settings.Default.Save());

        public string WindowTitle => ApplicationDeployment.IsNetworkDeployed
            ? $"Shared Clustering {ApplicationDeployment.CurrentDeployment.CurrentVersion}"
            : "Shared Clustering";

        public List<object> Tabs { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetFieldValue(ref _selectedTabIndex, value, nameof(SelectedTabIndex)))
                {
                    // Save the current value so that it can be restored when the application relaunched.
                    Settings.Default.SelectedTabIndex = SelectedTabIndex;
                    Settings.Default.Save();
                }
            }
        }

        private int _width = 850;
        public int Width
        {
            get => _width;
            set => SetFieldValue(ref _width, value, nameof(Width));
        }

        private int _height = 650;
        public int Height
        {
            get => _height;
            set => SetFieldValue(ref _height, value, nameof(Height));
        }
    }
}
