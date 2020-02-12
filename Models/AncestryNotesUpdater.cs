using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using AncestryDnaClustering.ViewModels;
using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AncestryDnaClustering.Models
{
    internal class AncestryNotesUpdater
    {
        private readonly AncestryMatchesRetriever _matchesRetriever;

        public AncestryNotesUpdater(AncestryMatchesRetriever matchesRetriever)
        {
            _matchesRetriever = matchesRetriever;
        }

        // Present an Open File dialog to allow selecting the saved DNA data from disk
        public string SelectFile(string fileName)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select file with Ancestry notes to upload",
                InitialDirectory = FileUtils.GetDefaultDirectory(fileName),
                FileName = fileName,
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            };
            if (openFileDialog.ShowDialog() == true)
            {
                Settings.Default.LastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();
                return openFileDialog.FileName;
            }
            return null;
        }

        public async Task UpdateNotesAsync(string guid, string matchFile, Throttle throttle, ProgressData progressData)
        {
            var originalTags = await _matchesRetriever.GetTagsAsync(guid, throttle);
            var originalTagIds = new HashSet<int>(originalTags.Select(tag => tag.TagId));

            var notes = ReadMatchFile(matchFile, originalTags, progressData).ToList();
            var originalNumNotes = notes.Count;

            await MaybeUpdateFilesAsync(notes, originalTags);

            notes = (await FilterModifiedNodesAsync(guid, notes, originalTagIds, throttle, progressData)).ToList();

            if (notes.Count == 0)
            {
                MessageBox.Show("No changed notes were found.", "No changed notes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var duplicateTags = originalTags
                .GroupBy(tag => tag.Label)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g).ToList();
            if (duplicateTags.Count > 1 
                && duplicateTags.Select(tag => tag.TagId).Intersect(notes.SelectMany(note => note.NewTags.Concat(note.NewTagsRemoved))).Any())
            {
                MessageBox.Show($"Duplicate group names found: '{duplicateTags.First().Label}'. Cannot update groups when more than one group has a matching name.", "Duplicate groups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var toChangeCount = notes.Count(
                note => (note.NewNotes != null && note.NewNotes != "") 
                || (note.NewStarred != null && note.NewStarred != note.OldStarred)
                || note.NewTags.Except(note.OldTags).Any()
                || note.NewTagsRemoved.Intersect(note.OldTags).Any());
            var toRemoveCount = notes.Count(note => note.NewNotes == "");
            var message = "Found "
                + (toChangeCount > 0 && toRemoveCount > 0
                ? $"{toChangeCount} matches to change and {toRemoveCount} notes to remove."
                : toChangeCount > 0
                ? $"{toChangeCount} matches to change."
                : $"{toRemoveCount} notes to remove.")
                + " Continue?";
            if (MessageBox.Show(
                message,
                "Notes to change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            progressData.Reset("Updating notes", notes.Count);

            var updateTasks = notes.Select(async note =>
            {
                try
                {
                    if (note.NewNotes != null)
                    {
                        await _matchesRetriever.UpdateNotesAsync(guid, note.TestId, note.NewNotes, throttle);
                    }
                    if (note.NewStarred != null)
                    {
                        await _matchesRetriever.UpdateStarredAsync(guid, note.TestId, note.NewStarred.Value, throttle);
                    }
                    foreach (var tagId in note.NewTags.Except(note.OldTags))
                    {
                        await _matchesRetriever.AddTagAsync(guid, note.TestId, tagId, throttle);
                    }
                    foreach (var tagId in note.NewTagsRemoved.Intersect(note.OldTags))
                    {
                        await _matchesRetriever.DeleteTagAsync(guid, note.TestId, tagId, throttle);
                    }
                    return note;
                }
                catch (Exception ex)
                {
                    FileUtils.LogException(new Exception($"Failed to update notes for {note.Name} ({note.TestId})", ex), false);
                    return null;
                }
                finally
                {
                    progressData.Increment();
                }
            });

            var updatedNotes = await Task.WhenAll(updateTasks);

            await SaveUpdatedNotesToFileAsync(updatedNotes.Where(note => note != null).ToList(), originalTags, progressData);

            if (originalNumNotes > notes.Count * 1000)
            {
                MessageBox.Show($"Only {originalNumNotes} out of {notes.Count} were updated. The uploading process will be much quicker if you trim down your file to just the changed matches, before uploading the changes.", "Few notes updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private class NotesData
        {
            public string Name { get; set; }
            public string TestId { get; set; }
            public string OldNotes { get; set; }
            public string NewNotes { get; set; }
            public bool OldStarred { get; set; }
            public bool? NewStarred { get; set; }
            public List<int> OldTags { get; set; }
            public List<int> NewTags { get; set; }
            public List<int> NewTagsRemoved { get; set; }
        }

        private static readonly HashSet<string> _deletionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "delete", "remove", "clear" }; 

        private static IEnumerable<NotesData> ReadMatchFile(string matchFile, List<Tag> originalTags, ProgressData progressData)
        {
            var tagIdsByLabel = originalTags.GroupBy(tag => tag.Label).ToDictionary(g => g.Key, g => g.First().TagId);

            using (var fileStream = new FileStream(matchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(fileStream))
            using (var ws = package.Workbook.Worksheets[1])
            {
                var nameColumn = 0;
                var testIdColumn = 0;
                var notesColumn = 0;
                var starredColumn = 0;
                var tagsColumns = new Dictionary<int, int>();

                // Find the columns that have interesting data (don't assume specific column numbers)
                foreach (var cell in ws.Cells.Where(c => c.Start.Row == 1))
                {
                    var cellValue = cell.GetValue<string>();
                    if (cellValue == null)
                    {
                        break;
                    }
                    if (cellValue.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        nameColumn = cell.End.Column;
                    }
                    else if (cellValue.Equals("Test ID", StringComparison.OrdinalIgnoreCase))
                    {
                        testIdColumn = cell.End.Column;
                    }
                    else if (cellValue.Equals("Notes", StringComparison.OrdinalIgnoreCase) || cellValue.Equals("Note", StringComparison.OrdinalIgnoreCase))
                    {
                        notesColumn = cell.End.Column;
                    }
                    else if (cellValue.Equals("Starred", StringComparison.OrdinalIgnoreCase))
                    {
                        starredColumn = cell.End.Column;
                    }
                    else if (tagIdsByLabel.TryGetValue(cellValue, out var tagId))
                    {
                        tagsColumns[tagId] = cell.End.Column;
                    }
                }

                if (nameColumn == 0 || testIdColumn == 0 || (notesColumn == 0 && starredColumn == 0 && !tagsColumns.Any()))
                {
                    throw new Exception($"Could not identify column headers from first sheet ({ws.Name}) in {matchFile}.");
                }

                var maxRow = 1;
                while (ws.Cells[maxRow + 1, testIdColumn].Value != null)
                {
                    maxRow++;
                }

                if (maxRow == 1)
                {
                    throw new Exception("No rows found.");
                }

                progressData.Reset("Loading data.", maxRow - 1);

                for (var row = 2; row <= maxRow; ++row)
                {
                    progressData.Increment();

                    var notes = notesColumn > 0 ? ws.Cells[row, notesColumn].GetValue<string>() : null;
                    var starred = starredColumn > 0 ? ws.Cells[row, starredColumn].GetValue<string>() : null;
                    var tags = tagsColumns
                        .Select(kvp =>
                        {
                            var tagLabel = ws.Cells[row, kvp.Value].GetValue<string>();
                            return new
                            {
                                TagId = kvp.Key,
                                HasTag = _deletionKeywords.Contains(tagLabel) ? false : string.IsNullOrEmpty(tagLabel) ? (bool?)null : true,
                            };
                        })
                        .Where(pair => pair.HasTag != null)
                        .ToLookup(pair => pair.HasTag.Value, pair => pair.TagId);

                    if (string.IsNullOrEmpty(notes) && string.IsNullOrEmpty(starred) && !tags.Any())
                    {
                        continue;
                    }

                    yield return new NotesData
                    {
                        Name = ws.Cells[row, nameColumn].GetValue<string>(),
                        TestId = ws.Cells[row, testIdColumn].GetValue<string>(),
                        NewNotes = _deletionKeywords.Contains(notes) ? "" : string.IsNullOrEmpty(notes) ? null : notes,
                        NewStarred = _deletionKeywords.Contains(starred) ? false : string.IsNullOrEmpty(starred) ? (bool?)null : true,
                        NewTags = tags[true].ToList(),
                        NewTagsRemoved = tags[false].ToList(),
                    };
                }
            }
        }

        private async Task MaybeUpdateFilesAsync(List<NotesData> notes, List<Tag> originalTags)
        {
            if (MessageBox.Show(
                "Do you also want to update data files that you already downloaded from Ancestry and have saved locally?",
                "Also update local saved data files",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            MessageBox.Show(
                "Select each file to update, Cancel to continue to updating the Ancestry web site.",
                "Also update local saved data files",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            while (true)
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select local file with Ancestry data to update",
                    InitialDirectory = FileUtils.GetDefaultDirectory(null),
                    Filter = "Shared Clustering downloaded data (*.txt)|*.txt",
                };
                if (openFileDialog.ShowDialog() != true || string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    return;
                }

                Settings.Default.LastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);
                Settings.Default.Save();

                var notesByIdGroups = notes.GroupBy(note => note.TestId).ToList();

                var duplicatedIds = notesByIdGroups.Where(g => g.Count() > 1).ToList();
                if (duplicatedIds.Count > 0)
                {
                    MessageBox.Show(
                        $"Duplicate IDs found  for names: {string.Join(", ", duplicatedIds.SelectMany(g => g).Select(notesData => notesData.Name))}. Please remove duplicates and try again",
                        "Duplicate IDs found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var notesById = notesByIdGroups.ToDictionary(g => g.Key, g => g.First());
                var serialized = await Task.Run(() => FileUtils.ReadAsJson<Serialized>(openFileDialog.FileName, false, false));

                foreach (var match in serialized.Matches)
                {
                    if (notesById.TryGetValue(match.TestGuid, out var note))
                    {
                        match.Note = note.NewNotes;
                        if (note.NewStarred != null)
                        {
                            match.Starred = note.NewStarred.Value;
                        }
                        if (note.NewTags.Any())
                        {
                            match.TagIds = (match.TagIds ?? new List<int>())
                                .Except(note.NewTagsRemoved)
                                .Concat(note.NewTags)
                                .Distinct()
                                .OrderBy(t => t)
                                .ToList();
                        }
                    }
                }

                FileUtils.WriteAsJson(openFileDialog.FileName, serialized, false);
            }
        }

        private async Task<IEnumerable<NotesData>> FilterModifiedNodesAsync(string guid, List<NotesData> notes, HashSet<int> tagIds, Throttle throttle, ProgressData progressData)
        {
            progressData.Reset("Filtering notes", notes.Count);
            var tasks = notes.Select(async note =>
            {
                var match = await _matchesRetriever.GetMatchAsync(guid, note.TestId, tagIds, throttle, progressData);
                if (match?.TestGuid == null)
                {
                    return null;
                }

                note.OldNotes = match.Note;
                note.OldStarred = match.Starred;
                note.OldTags = match.TagIds ?? new List<int>();
                return (
                    note.OldNotes != note.NewNotes 
                    || (note.NewStarred != null && note.OldStarred != note.NewStarred)
                    || note.NewTags.Except(note.OldTags).Any()
                    || note.NewTagsRemoved.Intersect(note.OldTags).Any()
                ) ? note : null;
            });
            var notesToUpdate = await Task.WhenAll(tasks);
            return notesToUpdate.Where(note => note != null);
        }

        private async Task SaveUpdatedNotesToFileAsync(List<NotesData> notes, List<Tag> originalTags, ProgressData progressData)
        {
            progressData.Reset("Saving local copy of changes.", notes.Count);

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save a record of updated notes",
                InitialDirectory = FileUtils.GetDefaultDirectory(null),
                FileName = $"Ancestry Notes Changes {DateTime.Now.ToString("yyyy-MM-dd")}.xlsx",
                DefaultExt = ".xlsx",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            };
            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }
            Settings.Default.LastUsedDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
            Settings.Default.Save();
            var fileName = saveFileDialog.FileName;

            var affectedNotes = notes.Any(note => note.NewNotes != null);
            var affectedStarred = notes.Any(note => note.NewStarred != null);
            var affectedTagIds = new HashSet<int>(notes.SelectMany(note => note.NewTags.Concat(note.NewTagsRemoved)));
            var affectedTags = originalTags.Where(tag => affectedTagIds.Contains(tag.TagId)).ToList();

            using (var p = new ExcelPackage())
            {
                await Task.Run(() =>
                {
                    var ws = p.Workbook.Worksheets.Add("Updated Notes");

                    var row = 1;
                    var col = 1;
                    ws.Cells[row, col++].Value = "Name";
                    ws.Cells[row, col++].Value = "Test ID";
                    if (affectedNotes)
                    {
                        ws.Cells[row, col++].Value = "Old Notes";
                        ws.Cells[row, col++].Value = "New Notes";
                    }
                    if (affectedStarred)
                    {
                        ws.Cells[row, col++].Value = "Old Starred";
                        ws.Cells[row, col++].Value = "New Starred";
                    }
                    foreach (var tag in affectedTags)
                    {
                        ws.Cells[row, col++].Value = $"Old {tag.Label}";
                        ws.Cells[row, col++].Value = $"New {tag.Label}";
                    }

                    foreach (var note in notes)
                    {
                        row++;
                        col = 1;

                        ws.Cells[row, col++].Value = note.Name;
                        ws.Cells[row, col++].Value = note.TestId;
                        if (affectedNotes)
                        {
                            ws.Cells[row, col++].Value = note.OldNotes;
                            ws.Cells[row, col++].Value = note.NewNotes;
                        }
                        if (affectedStarred)
                        {
                            ws.Cells[row, col++].Value = note.OldStarred ? "*" : null;
                            ws.Cells[row, col++].Value = (note.NewStarred ?? note.OldStarred) ? "*" : null;
                        }
                        foreach (var tag in affectedTags)
                        {
                            ws.Cells[row, col++].Value = note.OldTags.Contains(tag.TagId) ? "." : null;
                            ws.Cells[row, col++].Value = note.NewTagsRemoved.Contains(tag.TagId) ? null : note.NewTags.Contains(tag.TagId) || note.OldTags.Contains(tag.TagId) ? "." : null;
                        }

                        progressData.Increment();
                    }
                });

                FileUtils.Save(p, fileName);
                FileUtils.LaunchFile(fileName);
            }
        }
    }
}
