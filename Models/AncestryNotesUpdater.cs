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
                InitialDirectory = string.IsNullOrEmpty(fileName) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(fileName),
                FileName = fileName,
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }

        public async Task UpdateNotesAsync(string guid, string matchFile, Throttle throttle, ProgressData progressData)
        {
            var notes = ReadMatchFile(matchFile, progressData);

            notes = await FilterModifiedNodesAsync(guid, notes, throttle, progressData);

            var notesList = notes.ToList();

            if (notesList.Count == 0)
            {
                MessageBox.Show("No changed notes were found.", "No changed notes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var toChangeCount = notesList.Count(note => !string.IsNullOrEmpty(note.NewNotes));
            var toRemoveCount = notesList.Count - toChangeCount;
            var message = "Found "
                + (toChangeCount > 0 && toRemoveCount > 0
                ? $"{toChangeCount} notes to change and {toRemoveCount} notes to remove."
                : toChangeCount > 0
                ? $"{toChangeCount} notes to change."
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

            progressData.Reset("Updating notes", notesList.Count);

            var updateTasks = notes.Select(async note =>
            {
                await _matchesRetriever.UpdateNotesAsync(guid, note.TestId, note.NewNotes, throttle);
                progressData.Increment();
            });

            await Task.WhenAll(updateTasks);

            await SaveUpdatedNotesToFileAsync(notesList, progressData);
        }

        private class NotesData
        {
            public string Name { get; set; }
            public string TestId { get; set; }
            public string OldNotes { get; set; }
            public string NewNotes { get; set; }
        }

        private static readonly HashSet<string> _deletionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "delete", "remove", "clear" }; 

        private static IEnumerable<NotesData> ReadMatchFile(string matchFile, ProgressData progressData)
        {
            using (var fileStream = new FileStream(matchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(fileStream))
            using (var ws = package.Workbook.Worksheets[1])
            {
                var nameColumn = 0;
                var testIdColumn = 0;
                var notesColumn = 0;

                // Find the columns that have interesting data (don't assume specific column numbers)
                for (var col = 1; col < 1000; ++col)
                {
                    var cellValue = ws.Cells[1, col].GetValue<string>();
                    if (cellValue == null)
                    {
                        continue;
                    }
                    if (cellValue.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        nameColumn = col;
                    }
                    else if (cellValue.Equals("Test ID", StringComparison.OrdinalIgnoreCase))
                    {
                        testIdColumn = col;
                    }
                    else if (cellValue.Equals("Notes", StringComparison.OrdinalIgnoreCase))
                    {
                        notesColumn = col;
                    }
                }

                if (nameColumn + testIdColumn + notesColumn == 0)
                {
                    throw new Exception("Could not identify column headers.");
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

                    var notes = ws.Cells[row, notesColumn].GetValue<string>();
                    if (string.IsNullOrEmpty(notes))
                    {
                        continue;
                    }

                    if (_deletionKeywords.Contains(notes))
                    {
                        notes = "";
                    }

                    yield return new NotesData
                    {
                        Name = ws.Cells[row, nameColumn].GetValue<string>(),
                        TestId = ws.Cells[row, testIdColumn].GetValue<string>(),
                        NewNotes = notes,
                    };
                }
            }
        }

        private async Task<IEnumerable<NotesData>> FilterModifiedNodesAsync(string guid, IEnumerable<NotesData> notes, Throttle throttle, ProgressData progressData)
        {
            var tasks = notes.Select(async note =>
            {
                var match = await _matchesRetriever.GetMatchAsync(guid, note.TestId, throttle, progressData);
                note.OldNotes = match?.Note;
                return (match != null && match.Note != note.NewNotes) ? note : null;
            });
            var notesToUpdate = await Task.WhenAll(tasks);
            return notesToUpdate.Where(note => note != null);
        }

        private async Task SaveUpdatedNotesToFileAsync(List<NotesData> notes, ProgressData progressData)
        {
            progressData.Reset("Saving local copy of changes.", notes.Count);

            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                FileName = $"Ancestry Notes Changes {DateTime.Now.ToString("yyyy-MM-dd")}.xlsx",
                DefaultExt = ".xlsx",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            };
            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }
            var fileName = saveFileDialog.FileName;

            using (var p = new ExcelPackage())
            {
                await Task.Run(() =>
                {
                    var ws = p.Workbook.Worksheets.Add("Updated Notes");

                    var row = 1;
                    ws.Cells[row, 1].Value = "Name";
                    ws.Cells[row, 2].Value = "Test ID";
                    ws.Cells[row, 3].Value = "Old Notes";
                    ws.Cells[row, 4].Value = "New Notes";

                    foreach (var note in notes)
                    {
                        row++;

                        ws.Cells[row, 1].Value = note.Name;
                        ws.Cells[row, 2].Value = note.TestId;
                        ws.Cells[row, 3].Value = note.OldNotes;
                        ws.Cells[row, 4].Value = note.NewNotes;

                        progressData.Increment();
                    }
                });

                FileUtils.Save(p, fileName);
                FileUtils.LaunchFile(fileName);
            }
        }
    }
}
