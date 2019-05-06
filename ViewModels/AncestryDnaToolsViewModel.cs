using System.Collections.Generic;
using System.Deployment.Application;
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
            Tabs = new List<object>
            {
                new IntroductionViewModel(),
                new AncestryDnaDownloadingViewModel(),
                new AncestryDnaHierarchicalClusteringViewModel(matchesLoader),
                new AncestryDnaDistanceViewModel(matchesLoader),
            };
            SelectedTabIndex = Settings.Default.SelectedTabIndex;
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
