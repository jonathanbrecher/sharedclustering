using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using AncestryDnaClustering.Models.DistanceFinding;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using Microsoft.Win32;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// A ViewModel that manages configuration for generating clusters from already-downloaded DNA data.
    /// </summary>
    internal class AncestryDnaDistanceViewModel : ObservableObject
    {
        public string Header { get; } = "Distance";

        public ProgressData ProgressData { get; } = new ProgressData();

        private readonly IMatchesLoader _matchesLoader;

        public AncestryDnaDistanceViewModel(IMatchesLoader matchesLoader)
        {
            _matchesLoader = matchesLoader;

            SelectFileCommand = new RelayCommand(SelectFile);

            SelectDistanceFileCommand = new RelayCommand(SelectDistanceFile);

            ProcessSavedDataCommand = new RelayCommand(async () => await ProcessSavedDataAsync());

            MinClusterSizeDistance = Settings.Default.MinClusterSizeDistance;
            FilenameDistance = Settings.Default.FilenameDistance;
            MinCentimorgansToCompareDistance = Settings.Default.MinCentimorgansToCompareDistance;
            MinCentimorgansInSharedMatchesDistance = Settings.Default.MinCentimorgansInSharedMatchesDistance;
            DistanceBasisIds = Settings.Default.DistanceBasisIds;
            DistanceFilename = Settings.Default.DistanceFilename;
            ShowAdvancedDistanceOptions = Settings.Default.ShowAdvancedDistanceOptions;
        }

        public ICommand SelectFileCommand { get; set; }

        public ICommand SelectDistanceFileCommand { get; set; }

        public ICommand ProcessSavedDataCommand { get; set; }

        // The name of the file (full path name) that contains the saved match data.
        private string _filenameDistance;
        public string FilenameDistance
        {
            get => _filenameDistance;
            set
            {
                if (SetFieldValue(ref _filenameDistance, value, nameof(FilenameDistance)))
                {
                    Settings.Default.FilenameDistance = FilenameDistance;
                    CanProcessSavedData = File.Exists(FilenameDistance) && MinCentimorgansToCompareDistance > 0;
                }
            }
        }

        // Present an Open File dialog to allow selecting the saved DNA data from disk
        private void SelectFile()
        {
            var (fileName, trimmedFileName) = _matchesLoader.SelectFile(FilenameDistance);
            if (fileName != null)
            {
                FilenameDistance = fileName;

                if (trimmedFileName != null)
                {
                    DistanceFilename = Path.Combine(Path.GetDirectoryName(FilenameDistance), trimmedFileName + "-distance.txt");
                }
            }
        }

        private bool _canProcessSavedData;
        public bool CanProcessSavedData
        {
            get => _canProcessSavedData;
            set => SetFieldValue(ref _canProcessSavedData, value, nameof(CanProcessSavedData));
        }

        // The size of the smallest valid cluster. This defaults to 3, which probably shouldn't be changed.
        private int _minClusterSizeDistance;
        public int MinClusterSizeDistance
        {
            get => _minClusterSizeDistance;
            set
            {
                if (SetFieldValue(ref _minClusterSizeDistance, value, nameof(MinClusterSizeDistance)))
                {
                    Settings.Default.MinClusterSizeDistance = MinClusterSizeDistance;
                }
            }
        }

        // The centimorgans value of the lowest match that will appear in the cluster diagram.
        // Typical values are 20 (the lowest value shown on the Ancestry website)
        // and 6 (the lowest value returned in any fashion by Ancestry).
        // This is independent of the MinCentimorgansInSharedMatches value.
        private double _minCentimorgansToCompareDistance;
        public double MinCentimorgansToCompareDistance
        {
            get => _minCentimorgansToCompareDistance;
            set
            {
                if (SetFieldValue(ref _minCentimorgansToCompareDistance, value, nameof(MinCentimorgansToCompareDistance)))
                {
                    Settings.Default.MinCentimorgansToCompareDistance = MinCentimorgansToCompareDistance;
                    CanProcessSavedData = File.Exists(FilenameDistance) && MinCentimorgansToCompareDistance > 0;
                }
            }
        }

        // The centimorgans value of the lowest match that will be considered when evaluating shared matches.
        // Typical values are 20 (the lowest value shown on the Ancestry website)
        // and 6 (the lowest value returned in any fashion by Ancestry).
        // This is independent of the MinCentimorgansToCluster value.
        private double _minCentimorgansInSharedMatchesDistance;
        public double MinCentimorgansInSharedMatchesDistance
        {
            get => _minCentimorgansInSharedMatchesDistance;
            set
            {
                if (SetFieldValue(ref _minCentimorgansInSharedMatchesDistance, value, nameof(MinCentimorgansInSharedMatchesDistance)))
                {
                    Settings.Default.MinCentimorgansInSharedMatchesDistance = MinCentimorgansInSharedMatchesDistance;
                }
            }
        }

        // If this value is specified, only those GUIDs specified (one per line) in this list will be included in the cluster diagram.
        private string _distanceBasisIds;
        public string DistanceBasisIds
        {
            get => _distanceBasisIds;
            set
            {
                if (SetFieldValue(ref _distanceBasisIds, value, nameof(DistanceBasisIds)))
                {
                    Settings.Default.DistanceBasisIds = DistanceBasisIds;
                }
            }
        }

        // The file name (full path name) where the final cluster diagram should be saved.
        private string _distanceFilename;
        public string DistanceFilename
        {
            get => _distanceFilename;
            set
            {
                if (SetFieldValue(ref _distanceFilename, value, nameof(DistanceFilename)))
                {
                    Settings.Default.DistanceFilename = DistanceFilename;
                }
            }
        }

        // Display a Save File dialog to specify where the final distance lists should be saved.
        private void SelectDistanceFile()
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = string.IsNullOrEmpty(DistanceFilename) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(DistanceFilename),
                FileName = DistanceFilename,
                DefaultExt = ".txt",
                Filter = "Text|*.txt",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                DistanceFilename = saveFileDialog.FileName;
            }
        }

        // Whether the advanced options should be displayed.
        private bool _showAdvancedDistanceOptions;
        public bool ShowAdvancedDistanceOptions
        {
            get => _showAdvancedDistanceOptions;
            set
            {
                if (SetFieldValue(ref _showAdvancedDistanceOptions, value, nameof(ShowAdvancedDistanceOptions)))
                {
                    Settings.Default.ShowAdvancedDistanceOptions = ShowAdvancedDistanceOptions;
                }
            }
        }

        private async Task ProcessSavedDataAsync()
        {
            Settings.Default.Save();

            var startTime = DateTime.Now;

            var (testTakerTestId, clusterableMatches) = await _matchesLoader.LoadClusterableMatchesAsync(FilenameDistance, MinCentimorgansToCompareDistance, MinCentimorgansInSharedMatchesDistance, ProgressData);
            if (clusterableMatches == null)
            {
                return;
            }
            clusterableMatches = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= MinCentimorgansToCompareDistance).ToList();

            var distanceFinder = new DistanceFinder(MinClusterSizeDistance, ProgressData);

            if (!string.IsNullOrEmpty(DistanceBasisIds))
            {
                var testIdsAsBasis = new HashSet<string>(Regex.Split(DistanceBasisIds, @"\s+").Where(guid => !string.IsNullOrEmpty(guid)), StringComparer.OrdinalIgnoreCase);
                var indexesAsBasis = new HashSet<int>(clusterableMatches.Where(match => testIdsAsBasis.Contains(match.Match.TestGuid)).Select(match => match.Index));
                await distanceFinder.FindClosestByDistanceAsync(clusterableMatches, indexesAsBasis, DistanceFilename);
            }
            else
            {
                await distanceFinder.FindClosestByDistanceAsync(clusterableMatches, DistanceFilename);
            }

            ProgressData.Reset(DateTime.Now - startTime, "Done");
        }
    }
}
