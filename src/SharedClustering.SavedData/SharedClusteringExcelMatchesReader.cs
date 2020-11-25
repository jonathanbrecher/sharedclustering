using OfficeOpenXml;
using SharedClustering.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Match = SharedClustering.Core.Match;

namespace SharedClustering.SavedData
{
    /// <summary>
    /// Read cluster diagrams saved by Shared Clustering.
    /// </summary>
    public class SharedClusteringExcelMatchesReader : ISerializedMatchesReader
    {
        public bool IsSupportedFileType(string fileName) => fileName != null && Path.GetExtension(fileName).ToLower() == ".xlsx";

        public string GetTrimmedFileName(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return null;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            return fileNameWithoutExtension;
        }

        public async Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName, IProgressData progressData)
        {
            if (!IsSupportedFileType(fileName))
            {
                return (null, $"{fileName} is not a *.xlsx file");
            }

            var serialized = new Serialized
            {
                MatchIndexes = new Dictionary<string, int>(),
                Matches = new List<Match>(),
                Icw = new Dictionary<string, List<int>>(),
            };

            try
            {
                await Task.Run(() => ReadMatchFile(serialized, fileName, progressData));
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, false);
                return (null, $"Unexpected error while reading Shared Clustering cluster diagram: {ex.Message}");
            }

            return (serialized, null);
        }

        private static HashSet<string> _sharedCentimorganColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Shared Centimorgans",
            "hared Centimorgans",
            "Sared Centimorgans",
            "Shred Centimorgans",
            "Shaed Centimorgans",
            "Shard Centimorgans",
            "Share Centimorgans",
            "SharedCentimorgans",
            "Shared entimorgans",
            "Shared Cntimorgans",
            "Shared Cetimorgans",
            "Shared Cenimorgans",
            "Shared Centmorgans",
            "Shared Centiorgans",
            "Shared Centimrgans",
            "Shared Centimogans",
            "Shared Centimorans",
            "Shared Centimorgns",
            "Shared Centimorgas",
            "Shared Centimorgan",
            "Shared cM",
            "Shared cMs",
        };

        private static void ReadMatchFile(Serialized serialized, string matchFile, IProgressData progressData)
        {
            using (var fileStream = new FileStream(matchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(fileStream))
            {
                var numWorksheets = package.Workbook.Worksheets.Count;
                for (var worksheetNumber = 1; worksheetNumber <= numWorksheets && serialized.Matches.Count == 0; ++worksheetNumber)
                { 
                    using (var ws = package.Workbook.Worksheets[worksheetNumber])
                    {
                        var nameColumn = 0;
                        var identifierColumn = 0;
                        var linkColumn = 0;
                        var totalSharedCmColumn = 0;
                        var totalSharedSegmentsColumn = 0;
                        var treeTypeColumn = 0;
                        var treeSizeColumn = 0;
                        var commonAncestorsColumn = 0;
                        var starredColumn = 0;
                        var notesColumn = 0;
                        var firstMatchFieldIndex = 0;
                        var lastMatchFieldIndex = 0;

                        var tagColumns = ws.ConditionalFormatting
                            .Where(cf => cf.Address.Start.Column == cf.Address.End.Column && cf.Style.Fill.BackgroundColor.Color != null)
                            .GroupBy(cf => cf.Address.Start.Column)
                            .Select((g, index) =>
                            (
                                Column: g.Key,
                                Tag: new Tag
                                {
                                    TagId = index + 1001,
                                    Color = ColorTranslator.ToHtml(g.First().Style.Fill.BackgroundColor.Color.Value),
                                }
                            ))
                            .ToDictionary(pair => pair.Column, pair => pair.Tag);

                        var firstNameCellValue = "";

                        // Find the columns that have interesting data (don't assume specific column numbers)
                        for (var col = 1; col < 1000; ++col)
                        {
                            var cell = ws.Cells[1, col];
                            var cellValue = cell.GetValue<string>()?.Trim();
                            if (cellValue == null)
                            {
                                break;
                            }
                            if (cellValue.Equals("Name", StringComparison.OrdinalIgnoreCase))
                            {
                                nameColumn = col;
                                firstNameCellValue = ws.Cells[2, nameColumn].GetValue<string>();
                            }
                            else if (cellValue.Equals("Test ID", StringComparison.OrdinalIgnoreCase))
                            {
                                identifierColumn = col;
                            }
                            else if (cellValue.Equals("Link", StringComparison.OrdinalIgnoreCase))
                            {
                                linkColumn = col;
                            }
                            else if (_sharedCentimorganColumnNames.Contains(cellValue))
                            {
                                totalSharedCmColumn = col;
                            }
                            else if (cellValue.Equals("Shared Segments", StringComparison.OrdinalIgnoreCase))
                            {
                                totalSharedSegmentsColumn = col;
                            }
                            else if (cellValue.Equals("Tree Type", StringComparison.OrdinalIgnoreCase))
                            {
                                treeTypeColumn = col;
                            }
                            else if (cellValue.Equals("Tree Size", StringComparison.OrdinalIgnoreCase))
                            {
                                treeSizeColumn = col;
                            }
                            else if (cellValue.Equals("Common Ancestors", StringComparison.OrdinalIgnoreCase))
                            {
                                commonAncestorsColumn = col;
                            }
                            else if (cellValue.Equals("Starred", StringComparison.OrdinalIgnoreCase))
                            {
                                starredColumn = col;
                            }
                            else if (tagColumns.TryGetValue(col, out var tag))
                            {
                                tag.Label = cellValue;
                            }
                            else if (cellValue.Equals("note", StringComparison.OrdinalIgnoreCase))
                            {
                                notesColumn = col;
                            }

                            if ((notesColumn > 0 && col > notesColumn)
                                || (!string.IsNullOrEmpty(firstNameCellValue) && cellValue == firstNameCellValue))
                            {
                                firstMatchFieldIndex = col;
                                break;
                            }
                        }

                        if (nameColumn == 0 && totalSharedCmColumn == 0)
                        {
                            throw new Exception("Could not find either a Name column or a Shared Centimorgans column.");
                        }

                        if (totalSharedCmColumn == 0)
                        {
                            throw new Exception("Could not find a Shared Centimorgans column.");
                        }

                        if (firstMatchFieldIndex == 0)
                        {
                            throw new Exception("Could not find any match column.");
                        }

                        lastMatchFieldIndex = firstMatchFieldIndex;
                        do
                        {
                            var nextMatchFieldCell = ws.Cells[1, lastMatchFieldIndex + 1];
                            if (nextMatchFieldCell?.Value == null || (nextMatchFieldCell.GetValue<string>() == "0" && nextMatchFieldCell.Style?.Numberformat?.Format == "0;\\-0;;@"))
                            {
                                break;
                            }
                            else
                            {
                                lastMatchFieldIndex++;
                            }
                        } while (true);

                        var maxRow = 1;
                        while ((totalSharedCmColumn > 0 && ws.Cells[maxRow + 1, totalSharedCmColumn].Value != null) 
                            || (nameColumn > 0 && ws.Cells[maxRow + 1, nameColumn].Value != null))
                        {
                            maxRow++;
                        }

                        if (maxRow == 1)
                        {
                            throw new Exception("No rows found.");
                        }

                        var matchesAreSquare = lastMatchFieldIndex - firstMatchFieldIndex + 1 == maxRow - 1;

                        progressData.Reset("Loading data.", maxRow - 1);

                        var clusteredIndexes = new List<int>();

                        for (var row = 2; row <= maxRow; ++row)
                        {
                            progressData.Increment();

                            var resultMatch = new Match();

                            if (nameColumn != 0)
                            {
                                resultMatch.MatchTestDisplayName = ws.Cells[row, nameColumn].GetValue<string>();
                            }
                            if (identifierColumn != 0)
                            {
                                resultMatch.TestGuid = ws.Cells[row, identifierColumn].GetValue<string>();
                            }
                            if (linkColumn != 0 && serialized.TestTakerTestId == null)
                            {
                                var hyperlink = ws.Cells[row, linkColumn].Hyperlink?.ToString();
                                var regexMatch = hyperlink == null ? null : Regex.Match(hyperlink, "/(?<guid>[a-zA-z0-9-]*)/with/", RegexOptions.Compiled);
                                serialized.TestTakerTestId = regexMatch?.Success == true ? regexMatch.Groups["guid"].Value : null;
                            }
                            if (string.IsNullOrEmpty(resultMatch.TestGuid))
                            {
                                resultMatch.TestGuid = $"row_{row}";
                            }
                            if (totalSharedCmColumn != 0)
                            {
                                try
                                {
                                    resultMatch.SharedCentimorgans = ws.Cells[row, totalSharedCmColumn].GetValue<double>();
                                }
                                catch
                                {
                                    // Exceptions are non-fatal
                                }
                            }
                            if (totalSharedSegmentsColumn != 0)
                            {
                                try
                                {
                                    resultMatch.SharedSegments = ws.Cells[row, totalSharedSegmentsColumn].GetValue<int>();
                                }
                                catch
                                {
                                    // Exceptions are non-fatal
                                }
                            }
                            if (treeTypeColumn != 0)
                            {
                                resultMatch.TreeType = Enum.TryParse<TreeType>(ws.Cells[row, treeTypeColumn].GetValue<string>(), out var treeType) ? treeType : TreeType.Undetermined;
                            }
                            if (treeSizeColumn != 0)
                            {
                                try
                                {
                                    resultMatch.TreeSize = ws.Cells[row, treeSizeColumn].GetValue<int>();
                                }
                                catch
                                {
                                    // Exceptions are non-fatal
                                }
                            }
                            if (commonAncestorsColumn != 0)
                            {
                                resultMatch.CommonAncestors = ws.Cells[row, commonAncestorsColumn].GetValue<string>()?.Split(',').Select(name => name.Trim()).ToList();
                            }
                            if (starredColumn != 0)
                            {
                                resultMatch.Starred = ws.Cells[row, starredColumn].GetValue<string>() == "*";
                            }
                            if (tagColumns.Count > 0)
                            {
                                var tagIds = tagColumns
                                    .Where(tagColumn => !string.IsNullOrEmpty(ws.Cells[row, tagColumn.Key].GetValue<string>()))
                                    .Select(tagColumn => tagColumn.Value.TagId)
                                    .ToList();
                                if (tagIds.Count > 0)
                                {
                                    resultMatch.TagIds = tagIds;
                                }
                            }
                            if (notesColumn != 0)
                            {
                                resultMatch.Note = ws.Cells[row, notesColumn].GetValue<string>();
                            }

                            // Do not assume that the data is free of duplicates.
                            if (resultMatch.TestGuid == null || serialized.MatchIndexes.ContainsKey(resultMatch.TestGuid))
                            {
                                continue;
                            }

                            var columnValues = Enumerable.Range(firstMatchFieldIndex, lastMatchFieldIndex - firstMatchFieldIndex + 1)
                                .Select(col =>
                                {
                                    var index = col - firstMatchFieldIndex;
                                    var value = 0.0;
                                    try
                                    {
                                        value = ws.Cells[row, col].GetValue<double>();
                                    }
                                    catch
                                    {
                                        value = string.IsNullOrWhiteSpace(ws.Cells[row, col].GetValue<string>()) ? 0.0 : 1.0;
                                    }
                                    return (Index: index, Value: value);
                                })
                                .ToList();

                            var matchIndex = serialized.MatchIndexes.Count;

                            var icw = columnValues.Where(pair => pair.Value >= 1).Select(pair => pair.Index).ToList();

                            if (columnValues.Any(pair => pair.Value > 1.95))
                            {
                                // A self-match should be exactly 2.00 but leave a bit of room for rounding.
                                clusteredIndexes.Add(matchIndex);
                            }
                            else if(matchesAreSquare)
                            {
                                // If the matches are square -- same number of rows and columns -- assume that every match is a self-natch on the diagonal
                                clusteredIndexes.Add(matchIndex);
                            }

                            serialized.Matches.Add(resultMatch);
                            serialized.MatchIndexes[resultMatch.TestGuid] = matchIndex;
                            serialized.Icw[resultMatch.TestGuid] = icw;
                        }

                        // If there are only a few clustered indexes found, assume a data entry error with a stray '2' in the file somewhere.
                        if (clusteredIndexes.Count < serialized.Matches.Count(match => match.SharedCentimorgans > 20) / 10)
                        {
                            clusteredIndexes.Clear();
                        }

                        if (clusteredIndexes.Count > 0)
                        {
                            // Adjust ICW to ignore any under-20 cM matches that aren't fully clustered.
                            serialized.Icw = serialized.Icw.ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value.Where(value => value < clusteredIndexes.Count).Select(value => clusteredIndexes[value]).ToList());
                        }

                        // Make sure that every match is in common with itself
                        foreach (var kvp in serialized.MatchIndexes)
                        {
                            if (!serialized.Icw.TryGetValue(kvp.Key, out var icw))
                            {
                                serialized.Icw[kvp.Key] = new List<int>{ kvp.Value };
                            }
                            else if (!icw.Contains(kvp.Value))
                            {
                                icw.Add(kvp.Value);
                            }
                        }

                        if (tagColumns.Count > 0)
                        {
                            serialized.Tags = tagColumns.Values.ToList();
                        }
                    }
                }
            }

            if (serialized.Matches.Count == 0)
            {
                throw new Exception("No rows read.");
            }

            // Do not assume that the data is already ordered by descending Shared Centimorgans.
            serialized.SortMatchesDescending();
        }
    }
}
