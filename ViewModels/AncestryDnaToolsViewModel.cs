using System.Collections.Generic;
using System.Deployment.Application;
using System.Linq;
using System.Windows.Input;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// Main ViewModel for the application.
    /// </summary>
    internal class AncestryDnaToolsViewModel : ObservableObject
    {
        public AncestryDnaToolsViewModel()
        {
            var serializedMatchesReaders = new List<ISerializedMatchesReader>
            {
                new DnaGedcomAncestryMatchesReader(),
                new DnaGedcomFtdnaMatchesReader(),
                new SharedClusteringMatchesReader(),
                new AutoClusterCsvMatchesReader(),
                new AutoClusterExcelMatchesReader(),
            };

            var matchesLoader = new MatchesLoader(serializedMatchesReaders);

            // Extendable list of tabs to display.
            var clusteringTab = new AncestryDnaHierarchicalClusteringViewModel(matchesLoader);
            Tabs = new List<object>
            {
                new IntroductionViewModel(),
                new AncestryDnaDownloadingViewModel(OpenInClusterTab),
                new AncestryDnaHierarchicalClusteringViewModel(matchesLoader),
                new AncestryDnaSimilarityViewModel(matchesLoader),
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
    }
}
