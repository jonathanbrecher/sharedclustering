using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AncestryDnaClustering.Models.Anonymizers;
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
                InitialDirectory = FileUtils.GetDefaultDirectory(fileName),
                FileName = fileName,
                Filter = "DNAGedcom icw_ or AutoCluster files (*.csv)|*.csv|AutoCluster files (*.xlsx)|*.xlsx|Shared Clustering downloaded data (*.txt)|*.txt;*.json|All files (*.*)|*.*",
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

        public async Task<(string, List<IClusterableMatch>, List<Tag>)> LoadClusterableMatchesAsync(string savedData, double minCentimorgansToCluster, double minCentimorgansInSharedMatches, IAnonymizer anonymizer, ProgressData progressData)
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

            if (input == null)
            {
                MessageBox.Show(errorMessage);
                return (null, null, null);
            }

            return await Task.Run(() =>
            {
                var strongMatches = input.Matches.Where(match => match.SharedCentimorgans >= minCentimorgansToCluster).ToList();
                var maxMatchIndex = strongMatches.Count - 1;
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
                        match = GetAnonymizedMatch(match, anonymizer);
                        return (IClusterableMatch)new ClusterableMatch(index, match, kvp.Value);
                    }
                    )
                    .ToList();

                clusterableMatches = MaybeFilterMassivelySharedMatches(clusterableMatches);
                
                var testTakerTestId = anonymizer?.GetAnonymizedGuid(input.TestTakerTestId) ?? input.TestTakerTestId;
                var tags = anonymizer == null ? input.Tags : input.Tags?.Select((tag, index) => new Tag { TagId = tag.TagId, Color = tag.Color, Label = $"Group{index}" }).ToList(); 
                return (testTakerTestId, clusterableMatches, tags);
            });
        }

        private List<IClusterableMatch> MaybeFilterMassivelySharedMatches(List<IClusterableMatch> clusterableMatches)
        {
            var clusterableMatchesOver20cM = clusterableMatches.Where(match => match.Match.SharedCentimorgans > 20).ToList();
            if (clusterableMatchesOver20cM.Count > 0)
            {
                var lowestClusterableSharedCentimorgans = clusterableMatchesOver20cM.Last().Match.SharedCentimorgans;
                var filteringCutoff = clusterableMatchesOver20cM.Count / 3;

                // Consider which matches are left if excluding all matches who have shared matches with at least 1/3 of the total matches
                var clusterableMatchesFiltered = clusterableMatches
                    .Where(match =>
                        /*match.Match.SharedCentimorgans >= 1200
                        || (match.Match.SharedCentimorgans >= 50
                            && match.Match.SharedSegments > 1
                            && match.Match.SharedCentimorgans / match.Match.SharedSegments >= 13) // Large minimum sesgment length
                        ||*/ (match.Match.SharedCentimorgans >= lowestClusterableSharedCentimorgans
                            && match.Count < filteringCutoff)
                    ).ToList();

                // Don't do anything unless filtering will remove at least 100 matches (arbitrary cutoff)
                var numExcludedMatches = clusterableMatchesOver20cM.Count - clusterableMatchesFiltered.Count;
                if (numExcludedMatches >= 100
                    && MessageBox.Show(
                        "Do you want to exclude matches with huge numbers of shared matches?"
                        + Environment.NewLine + Environment.NewLine
                        + $"This will exclude {numExcludedMatches} matches (out of {clusterableMatches.Count}) with at least {filteringCutoff} shared matches.",
                        "Many shared matches",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var coordsFiltered = new HashSet<int>(clusterableMatchesFiltered.Select(match => match.Index));
                    clusterableMatches = clusterableMatchesFiltered
                        .Select(match => (IClusterableMatch)new ClusterableMatch(match.Index, match.Match, match.Coords.Where(coord => coordsFiltered.Contains(coord)).ToList()))
                        .ToList();
                }
            }
            return clusterableMatches;
        }

        private Match GetAnonymizedMatch(Match match, IAnonymizer anonymizer)
        {
            if (anonymizer == null)
            {
                return match;
            }

            return new Match
            {        
                MatchTestAdminDisplayName = anonymizer.GetAnonymizedName(match.MatchTestAdminDisplayName),
                MatchTestDisplayName = anonymizer.GetAnonymizedName(match.MatchTestDisplayName),
                TestGuid = anonymizer.GetAnonymizedGuid(match.TestGuid),
                SharedCentimorgans = match.SharedCentimorgans,
                SharedSegments = match.SharedSegments,
                LongestBlock = match.LongestBlock,
                TreeType = match.TreeType,
                TreeUrl = match.TreeUrl == null ? null : "https://invalid",
                TreeSize = match.TreeSize,
                HasCommonAncestors = match.HasCommonAncestors,
                CommonAncestors = match.CommonAncestors?.Select(commonAncestor => anonymizer.GetAnonymizedName(commonAncestor)).ToList(),
                Starred = match.Starred,
                HasHint = match.HasHint,
                Note = null,
                TagIds = match.TagIds,
                IsFather = match.IsFather,
                IsMother = match.IsMother,
            };
        }
    }
}
