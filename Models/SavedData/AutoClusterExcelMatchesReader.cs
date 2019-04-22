using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.ViewModels;
using CsvHelper;
using CsvHelper.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AncestryDnaClustering.Models.SavedData
{
    /// <summary>
    /// Read files saved by AutoCluster.
    /// </summary>
    public class AutoClusterExcelMatchesReader : ISerializedMatchesReader
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

        public async Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName, ProgressData progressData)
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
                return (null, $"Unexpected error while reading AutoCluster match file: {ex.Message}");
            }

            return (serialized, null);
        }

        private static void ReadMatchFile(Serialized serialized, string matchFile, ProgressData progressData)
        {
            using (var fileStream = new FileStream(matchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(fileStream))
            using (var ws = package.Workbook.Worksheets[1])
            {
                var hyperlinkColumn = 0;
                var totalSharedCmColumn = 0;
                var notesColumn = 0;
                var treeColumn = 0;
                var firstMatchFieldIndex = 0;
                var lastMatchFieldIndex = 0;

                // Find the columns that have interesting data (don't assume specific column numbers)
                for (var col = 1; col < 1000; ++col)
                {
                    var cell = ws.Cells[1, col];
                    var cellValue = cell.GetValue<string>();
                    if (cellValue.Equals("name", StringComparison.OrdinalIgnoreCase))
                    {
                        hyperlinkColumn = col;
                    }
                    else if (cellValue.Equals("total shared cM", StringComparison.OrdinalIgnoreCase))
                    {
                        totalSharedCmColumn = col;
                    }
                    else if (cellValue.Equals("notes", StringComparison.OrdinalIgnoreCase))
                    {
                        notesColumn = col;
                    }
                    else if (cellValue.Equals("tree", StringComparison.OrdinalIgnoreCase))
                    {
                        treeColumn = col;
                    }

                    var row2Cell = ws.Cells[2, col];
                    if (row2Cell.Style.Fill.BackgroundColor.Rgb != null)
                    {
                        firstMatchFieldIndex = col;
                        break;
                    }
                }

                if (totalSharedCmColumn == 0)
                {
                    throw new Exception("Total Shared cM column not found.");
                }

                lastMatchFieldIndex = firstMatchFieldIndex;
                while (ws.Cells[1, lastMatchFieldIndex + 1].Value != null)
                {
                    lastMatchFieldIndex++;
                }

                var maxRow = 1;
                while (ws.Cells[maxRow + 1, totalSharedCmColumn].Value != null)
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

                    var resultMatch = new Match();

                    if (hyperlinkColumn != 0)
                    {
                        try
                        {
                            // new format
                            var url = ws.Cells[row, hyperlinkColumn].Hyperlink.ToString();
                            var name = ws.Cells[row, hyperlinkColumn].GetValue<string>();
                            var path = url.Split('/');
                            resultMatch.MatchTestDisplayName = name;
                            serialized.TestTakerTestId = path[4];
                            resultMatch.TestGuid = path[6];
                        }
                        catch
                        {
                            try
                            {
                                // old format
                                var hyperlink = ws.Cells[row, hyperlinkColumn].GetValue<string>();
                                var fields = hyperlink.Split('"');
                                var url = fields[1];
                                var name = fields[3];
                                var path = url.Split('/');
                                resultMatch.MatchTestDisplayName = name;
                                serialized.TestTakerTestId = path[4];
                                resultMatch.TestGuid = path[6];
                            }
                            catch
                            {
                            }
                        }
                    }
                    if (totalSharedCmColumn != 0)
                    {
                        resultMatch.SharedCentimorgans = ws.Cells[row, totalSharedCmColumn].GetValue<double>();
                    }
                    if (notesColumn != 0)
                    {
                        resultMatch.Note = ws.Cells[row, notesColumn].GetValue<string>();
                    }
                    if (treeColumn != 0)
                    {
                        try
                        {
                            resultMatch.TreeUrl = ws.Cells[row, treeColumn].Hyperlink?.ToString();
                            if (!string.IsNullOrEmpty(resultMatch.TreeUrl))
                            {
                                var fields = ws.Cells[row, treeColumn].GetValue<string>().Split(' ');
                                if (fields.Last() == "persons")
                                {
                                    resultMatch.TreeSize = Convert.ToInt32(fields.First());
                                }
                            }
                        }
                        catch { }
                    }

                    // Do not assume that the AutoCluster data is free of duplicates.
                    if (resultMatch.TestGuid == null || serialized.MatchIndexes.ContainsKey(resultMatch.TestGuid))
                    {
                        continue;
                    }

                    var icw = Enumerable.Range(firstMatchFieldIndex, lastMatchFieldIndex - firstMatchFieldIndex + 1)
                        .Where(col => ws.Cells[row, col].Style.Fill.BackgroundColor.Rgb != null)
                        .Select(col => col - firstMatchFieldIndex)
                        .ToList();

                    // AutoCluster sometimes writes invalid CSV files, not properly quoting a line break in the notes field.
                    // When that happens the ICW data cannot be read
                    if (icw.Count == 0)
                    {
                        continue;
                    }

                    serialized.Matches.Add(resultMatch);
                    serialized.MatchIndexes[resultMatch.TestGuid] = serialized.MatchIndexes.Count;
                    serialized.Icw[resultMatch.TestGuid] = icw;
                }
            }

            if (serialized.Matches.Count == 0)
            {
                throw new Exception("No rows read.");
            }

            // Do not assume that the AutoCluster data is already ordered by descending Shared Centimorgans.
            serialized.SortMatchesDescending();
        }
    }
}
