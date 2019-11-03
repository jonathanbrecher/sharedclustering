using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters;
using AncestryDnaClustering.ViewModels;
using OfficeOpenXml;
using OfficeOpenXml.ConditionalFormatting;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters
{
    /// <summary>
    /// Write a correlation matrix to an Excel file.
    /// </summary>
    public class ExcelOutputWriter
    {
        private readonly string _testTakerTestId;
        private readonly string _ancestryHostName;
        private readonly ProgressData _progressData;

        public ExcelOutputWriter(string testTakerTestId, string ancestryHostName, ProgressData progressData)
        {
            _testTakerTestId = testTakerTestId;
            _ancestryHostName = ancestryHostName;
            _progressData = progressData;
        }

        public async Task ExportAsync(List<IClusterableMatch> matches, string exportFileName)
        {
            if (string.IsNullOrEmpty(exportFileName) || matches.Count == 0)
            {
                return;
            }

            _progressData.Reset("Exporting matches", matches.Count);

            using (var p = new ExcelPackage())
            {
                await Task.Run(() =>
                {
                    var ws = p.Workbook.Worksheets.Add("matches");

                    // Start at the top left of the sheet
                    var row = 1;
                    var col = 1;

                    // Rotate the entire top row by 90 degrees
                    ws.Row(row).Style.TextRotation = 90;

                    // Fixed columns
                    var writers = new IColumnWriter[]
                    {
                        new NameWriter(false),
                        matches.Any(match => !string.IsNullOrEmpty(match.Match.TestGuid)) ? new TestIdWriter() : null,
                        !string.IsNullOrEmpty(_testTakerTestId) ? new LinkWriter(_testTakerTestId, _ancestryHostName) : null,
                        new SharedCentimorgansWriter(),
                        matches.Any(match => match.Match.SharedSegments > 0) ? new SharedSegmentsWriter() : null,
                        matches.Any(match => match.Match.LongestBlock > 0) ? new LongestBlockWriter() : null,
                        matches.Any(match => !string.IsNullOrEmpty(match.Match.TreeUrl)) ? new TreeUrlWriter(_testTakerTestId) : null,
                        matches.Any(match => match.Match.TreeType != SavedData.TreeType.Undetermined) ? new TreeTypeWriter() : null,
                        matches.Any(match => match.Match.TreeSize > 0) ? new TreeSizeWriter() : null,
                        matches.Any(match => match.Match.Starred) ? new StarredWriter() : null,
                        matches.Any(match => match.Match.HasHint) ? new SharedAncestorHintWriter() : null,
                        new NoteWriter(),
                    }.Where(writer => writer != null).ToArray();
                    var columnWriters = new ColumnWritersCollection(p, ws, writers, _testTakerTestId);

                    col = columnWriters.WriteHeaders(row, col);

                    var firstMatrixDataRow = row + 1;
                    var firstMatrixDataColumn = col;

                    // One row for each match
                    foreach (var match in matches)
                    {
                        row++;

                        // Row headers
                        col = 1;
                        col = columnWriters.WriteColumns(row, col, match, null);

                        _progressData.Increment();
                    }

                    col = 1;
                    col = columnWriters.FormatColumns(row, col);

                    // Freeze the column and row headers
                    ws.View.FreezePanes(firstMatrixDataRow, firstMatrixDataColumn);
                });

                FileUtils.Save(p, exportFileName);
            }
        }
    }
}
