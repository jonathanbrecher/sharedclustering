using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AncestryDnaClustering.Properties;
using AncestryDnaClustering.SavedData;
using Microsoft.Win32;
using SharedClustering.Core;
using SharedClustering.Core.Anonymizers;
using SharedClustering.HierarchicalClustering;

namespace AncestryDnaClustering.Models
{
    public class MatchesLoader : IMatchesLoader
    {
        private readonly List<ISerializedMatchesReader> _serializedMatchesReaders;

        public MatchesLoader(List<ISerializedMatchesReader> serializedMatchesReaders)
        {
            _serializedMatchesReaders = serializedMatchesReaders;
        }

        // Present an Open File dialog to allow selecting the saved DNA data from disk
        public string SelectFile(string fileName)
        {
            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = DirectoryUtils.GetDefaultDirectory(fileName),
                FileName = fileName,
                Filter = "Shared Clustering downloaded data (*.txt)|*.txt;*.json|DNAGedcom icw_ or AutoCluster files (*.csv)|*.csv|Existing cluster diagrams (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                FilterIndex = Settings.Default.MatchesLoaderFilterIndex,
            };
            if (openFileDialog.ShowDialog() == true)
            {
                Settings.Default.MatchesLoaderFilterIndex = openFileDialog.FilterIndex;
                Settings.Default.LastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();

                return openFileDialog.FileName;
            }
            return null;
        }

        public string GetTrimmedFileName(string fileName)
        {
            return _serializedMatchesReaders.Select(reader => reader.GetTrimmedFileName(fileName)).FirstOrDefault(f => f != null);
        }

        public async Task<(string, List<IClusterableMatch>, List<Tag>)> LoadClusterableMatchesAsync(string savedData, double minCentimorgansToCluster, double minCentimorgansInSharedMatches, IAnonymizer anonymizer, IProgressData progressData)
        {
            progressData.Description = "Loading data...";

            var serializedMatchesReaders = _serializedMatchesReaders.Where(reader => reader.IsSupportedFileType(savedData)).ToList();
            if (serializedMatchesReaders.Count == 0)
            {
                MessageBox.Show("Unsupported file type.");
                return (null, null, null);
            }

            Serialized input = null;
            string errorMessage = null;
            foreach (var serializedMatchesReader in serializedMatchesReaders)
            {
                string thisErrorMessage;
                (input, thisErrorMessage) = await serializedMatchesReader.ReadFileAsync(savedData, progressData);
                if (input != null)
                {
                    break;
                }
                if (errorMessage == null)
                {
                    errorMessage = thisErrorMessage;
                }
            }

            var validationMessage = input?.Validate();

            if (input == null || validationMessage != null)
            {
                MessageBox.Show(validationMessage ?? errorMessage);
                return (null, null, null);
            }

            var clusterableMatches = await ClusterableMatchBuilder.LoadClusterableMatchesAsync(
                input.Matches,
                input.Icw,
                input.MatchIndexes,
                minCentimorgansToCluster,
                minCentimorgansInSharedMatches,
                anonymizer,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes,
                progressData);

            var testTakerTestId = anonymizer?.GetObfuscatedGuid(input.TestTakerTestId) ?? input.TestTakerTestId;
            var tags = anonymizer == null ? input.Tags : input.Tags?.Select((tag, index) => new Tag { TagId = tag.TagId, Color = tag.Color, Label = $"Group{index}" }).ToList(); 
            return (testTakerTestId, clusterableMatches, tags);
        }
    }
}
