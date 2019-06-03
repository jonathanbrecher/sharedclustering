using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using AncestryDnaClustering.Models.SimilarityFinding;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using Microsoft.Win32;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// A ViewModel that manages configuration for generating clusters from already-downloaded DNA data.
    /// </summary>
    internal class AncestryDnaSimilarityViewModel : ObservableObject
    {
        public string Header { get; } = "Similarity";

        public ProgressData ProgressData { get; } = new ProgressData();

        private readonly IMatchesLoader _matchesLoader;

        public AncestryDnaSimilarityViewModel(IMatchesLoader matchesLoader)
        {
            _matchesLoader = matchesLoader;

            SelectFileCommand = new RelayCommand(SelectFile);

            SelectSimilarityFileCommand = new RelayCommand(SelectSimilarityFile);

            ProcessSavedDataCommand = new RelayCommand(async () => await ProcessSavedDataAsync());

            MinClusterSizeSimilarity = Settings.Default.MinClusterSizeSimilarity;
            FilenameSimilarity = Settings.Default.FilenameSimilarity;
            MinCentimorgansToCompareSimilarity = Settings.Default.MinCentimorgansToCompareSimilarity;
            MinCentimorgansInSharedMatchesSimilarity = Settings.Default.MinCentimorgansInSharedMatchesSimilarity;
            SimilarityBasisIds = Settings.Default.SimilarityBasisIds;
            SimilarityFilename = Settings.Default.SimilarityFilename;
            ShowAdvancedSimilarityOptions = Settings.Default.ShowAdvancedSimilarityOptions;
        }

        public ICommand SelectFileCommand { get; set; }

        public ICommand SelectSimilarityFileCommand { get; set; }

        public ICommand ProcessSavedDataCommand { get; set; }

        // The name of the file (full path name) that contains the saved match data.
        private string _filenameSimilarity;
        public string FilenameSimilarity
        {
            get => _filenameSimilarity;
            set
            {
                if (SetFieldValue(ref _filenameSimilarity, value, nameof(FilenameSimilarity)))
                {
                    Settings.Default.FilenameSimilarity = FilenameSimilarity;
                    CanProcessSavedData = File.Exists(FilenameSimilarity) && MinCentimorgansToCompareSimilarity > 0;
                }
            }
        }

        // Present an Open File dialog to allow selecting the saved DNA data from disk
        private void SelectFile()
        {
            var (fileName, trimmedFileName) = _matchesLoader.SelectFile(FilenameSimilarity);
            if (fileName != null)
            {
                FilenameSimilarity = fileName;

                if (trimmedFileName != null)
                {
                    SimilarityFilename = Path.Combine(Path.GetDirectoryName(FilenameSimilarity), trimmedFileName + "-Similarity.xlsx");
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
        private int _minClusterSizeSimilarity;
        public int MinClusterSizeSimilarity
        {
            get => _minClusterSizeSimilarity;
            set
            {
                if (SetFieldValue(ref _minClusterSizeSimilarity, value, nameof(MinClusterSizeSimilarity)))
                {
                    Settings.Default.MinClusterSizeSimilarity = MinClusterSizeSimilarity;
                }
            }
        }

        // The centimorgans value of the lowest match that will appear in the cluster diagram.
        // Typical values are 20 (the lowest value shown on the Ancestry website)
        // and 6 (the lowest value returned in any fashion by Ancestry).
        // This is independent of the MinCentimorgansInSharedMatches value.
        private double _minCentimorgansToCompareSimilarity;
        public double MinCentimorgansToCompareSimilarity
        {
            get => _minCentimorgansToCompareSimilarity;
            set
            {
                if (SetFieldValue(ref _minCentimorgansToCompareSimilarity, value, nameof(MinCentimorgansToCompareSimilarity)))
                {
                    Settings.Default.MinCentimorgansToCompareSimilarity = MinCentimorgansToCompareSimilarity;
                    CanProcessSavedData = File.Exists(FilenameSimilarity) && MinCentimorgansToCompareSimilarity > 0;
                }
            }
        }

        // The centimorgans value of the lowest match that will be considered when evaluating shared matches.
        // Typical values are 20 (the lowest value shown on the Ancestry website)
        // and 6 (the lowest value returned in any fashion by Ancestry).
        // This is independent of the MinCentimorgansToCluster value.
        private double _minCentimorgansInSharedMatchesSimilarity;
        public double MinCentimorgansInSharedMatchesSimilarity
        {
            get => _minCentimorgansInSharedMatchesSimilarity;
            set
            {
                if (SetFieldValue(ref _minCentimorgansInSharedMatchesSimilarity, value, nameof(MinCentimorgansInSharedMatchesSimilarity)))
                {
                    Settings.Default.MinCentimorgansInSharedMatchesSimilarity = MinCentimorgansInSharedMatchesSimilarity;
                }
            }
        }

        // If this value is specified, only those GUIDs specified (one per line) in this list will be included in the cluster diagram.
        private string _SimilarityBasisIds;
        public string SimilarityBasisIds
        {
            get => _SimilarityBasisIds;
            set
            {
                if (SetFieldValue(ref _SimilarityBasisIds, value, nameof(SimilarityBasisIds)))
                {
                    Settings.Default.SimilarityBasisIds = SimilarityBasisIds;
                }
            }
        }

        // The file name (full path name) where the final cluster diagram should be saved.
        private string _SimilarityFilename;
        public string SimilarityFilename
        {
            get => _SimilarityFilename;
            set
            {
                if (SetFieldValue(ref _SimilarityFilename, value, nameof(SimilarityFilename)))
                {
                    Settings.Default.SimilarityFilename = SimilarityFilename;
                }
            }
        }

        // Display a Save File dialog to specify where the final Similarity lists should be saved.
        private void SelectSimilarityFile()
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = string.IsNullOrEmpty(SimilarityFilename) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(SimilarityFilename),
                FileName = SimilarityFilename,
                DefaultExt = ".xlsx",
                Filter = "Excel Workbook|*.xlsx",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                SimilarityFilename = saveFileDialog.FileName;
            }
        }

        // Whether the advanced options should be displayed.
        private bool _showAdvancedSimilarityOptions;
        public bool ShowAdvancedSimilarityOptions
        {
            get => _showAdvancedSimilarityOptions;
            set
            {
                if (SetFieldValue(ref _showAdvancedSimilarityOptions, value, nameof(ShowAdvancedSimilarityOptions)))
                {
                    Settings.Default.ShowAdvancedSimilarityOptions = ShowAdvancedSimilarityOptions;
                }
            }
        }

        private async Task ProcessSavedDataAsync()
        {
            Settings.Default.Save();

            var startTime = DateTime.Now;

            var (testTakerTestId, clusterableMatches) = await _matchesLoader.LoadClusterableMatchesAsync(FilenameSimilarity, MinCentimorgansToCompareSimilarity, MinCentimorgansInSharedMatchesSimilarity, ProgressData);
            if (clusterableMatches == null)
            {
                return;
            }
            clusterableMatches = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= MinCentimorgansToCompareSimilarity).ToList();

            var SimilarityFinder = new SimilarityFinder(MinClusterSizeSimilarity, ProgressData);
            Func<ISimilarityWriter> getSimilarityWriter = () => new ExcelSimilarityWriter(testTakerTestId, clusterableMatches, SimilarityFilename);

            if (!string.IsNullOrEmpty(SimilarityBasisIds))
            {
                var testIdsAsBasis = new HashSet<string>(Regex.Split(SimilarityBasisIds, @"[^a-zA-Z0-9-]+").Where(guid => !string.IsNullOrEmpty(guid)), StringComparer.OrdinalIgnoreCase);
                var indexesAsBasis = new HashSet<int>(clusterableMatches.Where(match => testIdsAsBasis.Contains(match.Match.TestGuid)).Select(match => match.Index));
                await SimilarityFinder.FindClosestBySimilarityAsync(clusterableMatches, indexesAsBasis, getSimilarityWriter);
            }
            else
            {
                await SimilarityFinder.FindClosestBySimilarityAsync(clusterableMatches, getSimilarityWriter);
            }

            ProgressData.Reset(DateTime.Now - startTime, "Done");
        }
    }
}
