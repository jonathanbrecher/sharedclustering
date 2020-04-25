using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using Microsoft.Win32;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// A ViewModel that manages configuration for generating clusters from already-downloaded DNA data.
    /// </summary>
    internal class AncestryDnaExportViewModel : ObservableObject
    {
        public string Header { get; } = "Export";

        public ProgressData ProgressData { get; } = new ProgressData();

        private readonly IMatchesLoader _matchesLoader;

        public AncestryDnaExportViewModel(IMatchesLoader matchesLoader)
        {
            _matchesLoader = matchesLoader;

            ExportCommand = new RelayCommand(async () => await ExportAsync());

            AncestryHostName = Settings.Default.AncestryHostName;
        }

        public ICommand ExportCommand { get; set; }

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

        // Display a Save File dialog to specify where the final cluster diagram should be saved.
        private string SelectOutputFile()
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = FileUtils.GetDefaultDirectory(null),
                DefaultExt = ".xlsx",
                Filter = "Excel Workbook|*.xlsx",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                Settings.Default.LastUsedDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                Settings.Default.Save();
                return saveFileDialog.FileName;
            }
            return null;
        }

        private async Task ExportAsync()
        {
            Settings.Default.Save();

            var matchesFileName = _matchesLoader.SelectFile("");

            var startTime = DateTime.Now;

            try
            {
                var (testTakerTestId, clusterableMatches, tags) = await _matchesLoader.LoadClusterableMatchesAsync(matchesFileName, 0, 0, null, ProgressData);

                var outputFileName = SelectOutputFile();
                if (string.IsNullOrEmpty(outputFileName))
                {
                    return;
                }

                var outputWriter = new ExcelOutputWriter(testTakerTestId, AncestryHostName, ProgressData);

                const int maxMatchesPerFile = 50000;
                var clusterableMatchesBatches = new List<IEnumerable<IClusterableMatch>> { clusterableMatches };
                if (clusterableMatches.Count > maxMatchesPerFile)
                {
                    if (MessageBox.Show(
                        $"Hyperlinks will not work if all {clusterableMatches.Count} matches are written to a single file.{Environment.NewLine}{Environment.NewLine}" +
                        $"Do you want to split the output into {Math.Ceiling(clusterableMatches.Count / (double)maxMatchesPerFile)} files of {maxMatchesPerFile} matches each?",
                        "Too many matches",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.No)
                    {
                        clusterableMatchesBatches = clusterableMatches.Batch(maxMatchesPerFile).ToList();
                    }
                }

                for (var fileNum = 0; fileNum < clusterableMatchesBatches.Count; ++fileNum)
                {
                    await outputWriter.ExportAsync(clusterableMatchesBatches[fileNum].ToList(), tags, fileNum == 0 ? outputFileName : FileUtils.AddSuffixToFilename(outputFileName, (fileNum + 1).ToString()));
                }

                FileUtils.LaunchFile(outputFileName);
            }
            finally
            {
                ProgressData.Reset(DateTime.Now - startTime, "Done");
            }
        }
    }
}
