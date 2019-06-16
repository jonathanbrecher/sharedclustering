using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.Properties;
using AncestryDnaClustering.ViewModels;
using Microsoft.Win32;

namespace AncestryDnaClustering.Models.SavedData
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
                InitialDirectory = string.IsNullOrEmpty(fileName) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(fileName),
                FileName = fileName,
                Filter = "DNAGedcom icw_ or AutoCluster files (*.csv)|*.csv|AutoCluster files (*.xlsx)|*.xlsx|Shared Clustering downloaded data (*.txt)|*.txt;*.json|All files (*.*)|*.*",
                FilterIndex = Settings.Default.MatchesLoaderFilterIndex,
            };
            if (openFileDialog.ShowDialog() == true)
            {
                Settings.Default.MatchesLoaderFilterIndex = openFileDialog.FilterIndex;
                Settings.Default.Save();

                return openFileDialog.FileName;
            }
            return null;
        }

        public string GetTrimmedFileName(string fileName)
        {
            return _serializedMatchesReaders.Select(reader => reader.GetTrimmedFileName(fileName)).FirstOrDefault(f => f != null);
        }

        public async Task<(string, List<IClusterableMatch>)> LoadClusterableMatchesAsync(string savedData, double minCentimorgansToCluster, double minCentimorgansInSharedMatches, ProgressData progressData)
        {
            progressData.Description = "Loading data...";

            var serializedMatchesReaders = _serializedMatchesReaders.Where(reader => reader.IsSupportedFileType(savedData)).ToList();
            if (serializedMatchesReaders.Count == 0)
            {
                MessageBox.Show("Unsupported file type.");
                return (null, null);
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

            if (input == null)
            {
                MessageBox.Show(errorMessage);
                return (null, null);
            }

            return await Task.Run(() =>
            {
                var strongMatches = input.Matches.Where(match => match.SharedCentimorgans >= minCentimorgansToCluster).ToList();
                var maxMatchIndex = strongMatches.Count + 1;
                var maxIcwIndex = Math.Min(maxMatchIndex, input.Matches.Count(match => match.SharedCentimorgans >= minCentimorgansInSharedMatches) + 1);
                maxIcwIndex = Math.Min(maxIcwIndex, input.Matches.Count - 1);
                var strongMatchesGuids = new HashSet<string>(strongMatches.Select(match => match.TestGuid), StringComparer.OrdinalIgnoreCase);
                var icw = input.Icw
                    .Where(kvp => strongMatchesGuids.Contains(kvp.Key))
                    .OrderBy(kvp => input.MatchIndexes.TryGetValue(kvp.Key, out var index) ? index : input.MatchIndexes.Count)
                    .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Where(index => index <= maxIcwIndex).ToList()
                    );
                var matchesDictionary = strongMatches.ToDictionary(match => match.TestGuid);
                var clusterableMatches = icw
                    .AsParallel().AsOrdered()
                    .Select((kvp, index) =>
                    {
                        var match = matchesDictionary[kvp.Key];
                        return (IClusterableMatch)new ClusterableMatch(index, match, kvp.Value);
                    }
                    )
                    .ToList();
                return (input.TestTakerTestId, clusterableMatches);
            });
        }
    }
}
