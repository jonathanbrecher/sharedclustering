using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName)
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
                await Task.Run(() => ReadMatchFile(serialized, fileName));
            }
            catch (Exception ex)
            {
                return (null, $"Unexpected error while reading AutoCluster match file: {ex.Message}");
            }

            return (serialized, null);
        }

        private static void ReadMatchFile(Serialized serialized, string matchFile)
        {
            using (var fileStream = new FileStream(matchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(fileStream))
            {
                var ws = package.Workbook.Worksheets[1];

                var hyperlinkColumn = 0;
                var totalSharedCmColumn = 0;
                var notesColumn = 0;
                var treeColumn = 0;
                var firstMatchFieldIndex = 0;
                var lastMatchFieldIndex = 0;

                // Find the columns that have interestingn data (don't assume specific column numbers)
                for (var col = 1; col < 1000; ++col)
                {
                    var cell = ws.Cells[1, col];
                    var cellValue = cell.Value.ToString();
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

                for (var row = 2; row <= maxRow; ++row)
                {
                    var resultMatch = new Match();

                    if (hyperlinkColumn != 0)
                    {
                        try
                        {
                            var hyperlink = ws.Cells[row, hyperlinkColumn].Value.ToString();
                            var fields = hyperlink.Split('"');
                            var url = fields[1];
                            var name = fields[3];
                            var path = url.Split('/');
                            resultMatch.MatchTestDisplayName = name;
                            serialized.TestTakerTestId = path[4];
                            resultMatch.TestGuid = path[6];
                        }
                        catch { }
                    }
                    if (totalSharedCmColumn != 0)
                    {
                        resultMatch.SharedCentimorgans = Convert.ToDouble(ws.Cells[row, totalSharedCmColumn].Value ?? 0);
                    }
                    if (notesColumn != 0)
                    {
                        resultMatch.Note = ws.Cells[row, notesColumn].Value?.ToString();
                    }
                    if (treeColumn != 0)
                    {
                        resultMatch.TreeUrl = ws.Cells[row, treeColumn].Value?.ToString();
                    }

                    // Do not assume that the AutoCluster data is free of duplicates.
                    if (serialized.MatchIndexes.ContainsKey(resultMatch.TestGuid))
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

            // Do not assume that the AutoCluster data is already ordered by descending Shared Centimorgans.
            var unsortedIndexes = serialized.MatchIndexes;

            serialized.Matches = serialized.Matches.OrderByDescending(match => match.SharedCentimorgans).ToList();

            serialized.MatchIndexes = serialized.Matches
                .Select((match, index) => new { match.TestGuid, index })
                .ToDictionary(pair => pair.TestGuid, pair => pair.index);

            var indexUpdates = unsortedIndexes.ToDictionary(
                kvp => kvp.Value,
                kvp => serialized.MatchIndexes[kvp.Key]);

            serialized.Icw = serialized.Icw.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(index => indexUpdates[index]).ToList());
        }

        private static readonly IFormatProvider[] _cultures = { CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo("en-US"), CultureInfo.InvariantCulture };

        public static double GetDouble(string value)
        {
            foreach (var culture in _cultures)
            {
                if (double.TryParse(value, NumberStyles.Any, culture, out var result))
                {
                    return result;
                }
            }

            return 0.0;
        }

        public static int GetInt(string value)
        {
            foreach (var culture in _cultures)
            {
                if (int.TryParse(value, NumberStyles.Any, culture, out var result))
                {
                    return result;
                }
            }

            return 0;
        }

        // Load all fields as strings and parse manually, to protect against parse failures
        private class AutoClusterMatch
        {
            public string TestGuid { get; set; }
            public string Name { get; set; }
            public string SharedCm { get; set; }
            public string Tree { get; set; }
            public string Notes { get; set; }
        }
    }
}
