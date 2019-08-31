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
using System.Windows;
using AncestryDnaClustering.Models;

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
            AncestryHostName = Settings.Default.AncestryHostName;
            OpenSimilarityFileWhenComplete = Settings.Default.OpenSimilarityFileWhenComplete;
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
            var fileName = _matchesLoader.SelectFile(FilenameSimilarity);
            if (fileName != null)
            {
                FilenameSimilarity = fileName;

                var trimmedFileName = _matchesLoader.GetTrimmedFileName(FilenameSimilarity);
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

        // The Ancestry host name to be used in links within the cluster diagram.
        private string _ancestryHostName;
        public string AncestryHostName
        {
            get => _ancestryHostName;
            set
            {
                if (SetFieldValue(ref _ancestryHostName, value, nameof(AncestryHostName)))
                {
                    Settings.Default.AncestryHostName = AncestryHostName;
                }
            }
        }

        private bool _openSimilarityFileWhenComplete;
        public bool OpenSimilarityFileWhenComplete
        {
            get => _openSimilarityFileWhenComplete;
            set
            {
                if (SetFieldValue(ref _openSimilarityFileWhenComplete, value, nameof(OpenSimilarityFileWhenComplete)))
                {
                    Settings.Default.OpenSimilarityFileWhenComplete = OpenSimilarityFileWhenComplete;
                }
            }
        }

        private async Task ProcessSavedDataAsync()
        {
            Settings.Default.Save();

            var startTime = DateTime.Now;

            try
            {
                var (testTakerTestId, clusterableMatches) = await _matchesLoader.LoadClusterableMatchesAsync(FilenameSimilarity, MinCentimorgansToCompareSimilarity, MinCentimorgansInSharedMatchesSimilarity, ProgressData);
                if (clusterableMatches == null)
                {
                    return;
                }
                clusterableMatches = clusterableMatches.Where(match => match.Match.SharedCentimorgans >= MinCentimorgansToCompareSimilarity).ToList();

                var SimilarityFinder = new SimilarityFinder(MinClusterSizeSimilarity, ProgressData);
                Func<string, ISimilarityWriter> getSimilarityWriter = fileNameSuffix => new ExcelSimilarityWriter(testTakerTestId, AncestryHostName, clusterableMatches, SimilarityFilename, fileNameSuffix);

                if (!string.IsNullOrEmpty(SimilarityBasisIds))
                {
                    var testIdsAsBasis = new HashSet<string>(Regex.Split(SimilarityBasisIds, @"[^a-zA-Z0-9-]+").Where(guid => !string.IsNullOrEmpty(guid)), StringComparer.OrdinalIgnoreCase);
                    var foo = clusterableMatches.FirstOrDefault(match => match.Match.TestGuid == testIdsAsBasis.First());
                    var matchesAsBasis = testIdsAsBasis.Count() == 1
                        ? clusterableMatches.Where(match => string.Equals(match.Match.TestGuid, testIdsAsBasis.First(), StringComparison.OrdinalIgnoreCase)).ToList()
                        : clusterableMatches.Where(match => testIdsAsBasis.Contains(match.Match.TestGuid)).ToList();
                    var indexesAsBasis = new HashSet<int>(
                        matchesAsBasis.Any(match => match.Match.SharedCentimorgans < MinCentimorgansInSharedMatchesSimilarity)
                        ? matchesAsBasis
                            .SelectMany(match => match.Coords)
                            .GroupBy(coord => coord)
                            .Where(g => g.Count() >= matchesAsBasis.Count / 2 && clusterableMatches.First(clusterableMatch => clusterableMatch.Index == g.Key).Match.SharedCentimorgans < 600)
                            .Select(g => g.Key)
                        : matchesAsBasis.Count() == 1  && testIdsAsBasis.Count() == 1
                        ? matchesAsBasis.First().Coords 
                        : matchesAsBasis.Select(match => match.Index));
                    await SimilarityFinder.FindClosestBySimilarityAsync(clusterableMatches, indexesAsBasis, getSimilarityWriter);
                }
                else
                {
                    if (MessageBox.Show(
                        $"No test IDs have been specified. This will report all similarity for all matches, possibly millions of lines of data. Proceed anyway?",
                        "Lots of data",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    MessageBox.Show(
                        $"Large numbers of results will be split into multiple files, with about 100,000 lines per file.",
                        "Lots of data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    var files = await SimilarityFinder.FindClosestBySimilarityAsync(clusterableMatches, getSimilarityWriter);

                    if (OpenSimilarityFileWhenComplete)
                    {
                        foreach (var file in files)
                        {
                            FileUtils.LaunchFile(file);
                        }
                    }
                }
            }
            finally
            {
                ProgressData.Reset(DateTime.Now - startTime, "Done");
            }
        }
    }
}
