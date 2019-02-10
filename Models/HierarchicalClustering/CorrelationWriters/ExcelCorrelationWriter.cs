using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AncestryDnaClustering.Models.HierarchicalCustering;
using AncestryDnaClustering.ViewModels;
using OfficeOpenXml;
using OfficeOpenXml.ConditionalFormatting;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters
{
    /// <summary>
    /// Write a correlation matrix to an Excel file.
    /// </summary>
    public class ExcelCorrelationWriter : ICorrelationWriter
    {
        private readonly string _correlationFilename;
        private readonly string _testTakerTestGuid;
        private readonly ProgressData _progressData;

        public ExcelCorrelationWriter(string correlationFilename, string testTakerTestGuid, ProgressData progressData)
        {
            _correlationFilename = correlationFilename;
            _testTakerTestGuid = testTakerTestGuid;
            _progressData = progressData;
        }

        public async Task OutputCorrelationAsync(List<ClusterNode> nodes, Dictionary<int, IClusterableMatch> matchesByIndex, Dictionary<int, int> indexClusterNumbers)
        {
            if (string.IsNullOrEmpty(_correlationFilename))
            {
                return;
            }

            if (nodes.Count == 0)
            {
                return;
            }

            // All nodes, in order. These will become rows/columns in the Excel file.
            var leafNodes = nodes.First().GetOrderedLeafNodes().ToList();

            _progressData.Reset($"Saving clusters", leafNodes.Count);

            // Ancestry never shows matches lower than 20 cM as shared matches.
            // The distant matches will be included as rows in the Excel file, but not as columns.
            // That means that correlation diagrams that include distant matches will be rectangular (tall and narrow)
            // rather than square.
            var matches = leafNodes
                .Where(leafNode => matchesByIndex.ContainsKey(leafNode.Index))
                .Select(leafNode => matchesByIndex[leafNode.Index])
                .ToList();
            var lowestClusterableCentimorgans = matches
                .SelectMany(match => match.Coords.Where(coord => coord != match.Index && matchesByIndex.ContainsKey(coord)))
                .Distinct()
                .Min(coord => matchesByIndex[coord].Match.SharedCentimorgans);
            var nonDistantMatches = matches
                .Where(match => match.Match.SharedCentimorgans >= lowestClusterableCentimorgans)
                .ToList();

            var orderedIndexes = nonDistantMatches
                .Select(match => match.Index)
                .ToList();

            // Because very strong matches are included in so many clusters,
            // excluding the strong matches makes it easier to identify edges of the clusters. 
            var immediateFamilyIndexes = new HashSet<int>(
                matchesByIndex.Values
                .Where(match => match.Match.SharedCentimorgans > 200)
                .Select(match => match.Index)
                );

            using (var p = new ExcelPackage())
            {
                await Task.Run(() =>
                {
                    var ws = p.Workbook.Worksheets.Add("heatmap");

                    if (!string.IsNullOrEmpty(_testTakerTestGuid))
                    {
                        // Google Sheets does not support HyperlinkBase
                        // p.Workbook.Properties.HyperlinkBase = new Uri($"https://www.ancestry.com/dna/tests/{_testTakerTestGuid}/match/");
                        var namedStyle = p.Workbook.Styles.CreateNamedStyle("HyperLink");   // Language-dependent
                        namedStyle.Style.Font.UnderLine = true;
                        namedStyle.Style.Font.Color.SetColor(Color.Blue);
                    }

                    var hasSharedSegments = matches.Any(match => match.Match.SharedSegments > 0);
                    var hasTreeType = matches.Any(match => match.Match.TreeType != SavedData.TreeType.Undetermined);
                    var hasStarredMatches = matches.Any(match => match.Match.Starred);
                    var hasHintsForMatches = matches.Any(match => match.Match.HasHint);

                    // Keep track of columns that will be auto-fit.
                    // The auto-fit cannot be calculated until the rows are fully populated.
                    var autofitColumns = new List<int>();

                    // Start at the top left of the sheet
                    var row = 1;
                    var col = 1;

                    // Rotate the entire top row by 90 degrees
                    ws.Row(row).Style.TextRotation = 90;

                    // Column headers for the fixed columns

                    ws.Column(col).Width = 26.0 / 7;
                    ws.Cells[row, col++].Value = "Cluster Number";

                    ws.Column(col).Width = 15;
                    ws.Cells[row, col].Style.TextRotation = 0;
                    ws.Cells[row, col++].Value = "Name";

                    ws.Column(col).Width = 10;
                    ws.Cells[row, col].Style.TextRotation = 0;
                    ws.Cells[row, col++].Value = "Test Guid";

                    if (!string.IsNullOrEmpty(_testTakerTestGuid))
                    {
                        autofitColumns.Add(col);
                        ws.Cells[row, col].Style.TextRotation = 0;
                        ws.Cells[row, col++].Value = "Link";
                    }

                    autofitColumns.Add(col);
                    ws.Column(col).Style.Numberformat.Format = "0.0";
                    ws.Cells[row, col++].Value = "Shared Centimorgans";

                    if (hasSharedSegments)
                    {
                        autofitColumns.Add(col);
                        ws.Cells[row, col++].Value = "Shared Segments";
                    }

                    if (hasTreeType)
                    {
                        autofitColumns.Add(col);
                        ws.Cells[row, col++].Value = "Tree Type";
                    }

                    autofitColumns.Add(col);
                    ws.Cells[row, col++].Value = "Tree Size";

                    if (hasStarredMatches)
                    {
                        autofitColumns.Add(col);
                        ws.Cells[row, col++].Value = "Starred";
                    }

                    if (hasHintsForMatches)
                    {
                        autofitColumns.Add(col);
                        ws.Cells[row, col++].Value = "Shared Ancestor Hint";
                    }

                    ws.Column(col).Width = 15;
                    ws.Cells[row, col++].Value = "Correlated Clusters";

                    ws.Column(col).Width = 15;
                    ws.Cells[row, col].Style.TextRotation = 0;
                    ws.Cells[row, col++].Value = "Note";

                    var firstMatrixDataRow = row + 1;
                    var firstMatrixDataColumn = col;

                    // Column headers for each match
                    foreach (var nonDistantMatch in nonDistantMatches)
                    {
                        ws.Cells[row, col++].Value = nonDistantMatch.Match.Name;
                    }

                    // One row for each match
                    foreach (var leafNode in leafNodes)
                    {
                        var match = matchesByIndex[leafNode.Index];
                        row++;

                        // Row headers
                        col = 1;
                        if (indexClusterNumbers.TryGetValue(leafNode.Index, out var clusterNumber))
                        {
                            ws.Cells[row, col++].Value = clusterNumber;
                        }
                        else
                        {
                            col++;
                        }
                        ws.Cells[row, col++].Value = match.Match.Name;
                        ws.Cells[row, col++].Value = match.Match.TestGuid;
                        if (!string.IsNullOrEmpty(_testTakerTestGuid))
                        {
                            ws.Cells[row, col].StyleName = "HyperLink";
                            ws.Cells[row, col++].Hyperlink = new ExcelHyperLink($"https://www.ancestry.com/dna/tests/{_testTakerTestGuid}/match/{match.Match.TestGuid}", UriKind.Absolute) { Display = "Link" };
                        }
                        ws.Cells[row, col++].Value = match.Match.SharedCentimorgans;
                        if (hasSharedSegments)
                        {
                            ws.Cells[row, col++].Value = match.Match.SharedSegments;
                        }
                        if (hasTreeType)
                        {
                            ws.Cells[row, col++].Value = match.Match.TreeType;
                        }
                        ws.Cells[row, col++].Value = match.Match.TreeSize;

                        if (hasStarredMatches)
                        {
                            ws.Cells[row, col++].Value = match.Match.Starred ? "*" : null;
                        }

                        if (hasHintsForMatches)
                        {
                            ws.Cells[row, col++].Value = match.Match.HasHint ? "*" : null;
                        }

                        // Correlated clusters
                        var correlatedClusterNumbers = leafNodes
                            .Where(leafNode2 => !immediateFamilyIndexes.Contains(leafNode2.Index)
                                                && leafNode.Coords.TryGetValue(leafNode2.Index, out var correlationValue) ? correlationValue >= 1 : false)
                            .Select(leafNode2 => indexClusterNumbers.TryGetValue(leafNode2.Index, out var correlatedClusterNumber) ? correlatedClusterNumber : 0)
                            .Where(correlatedClusterNumber => correlatedClusterNumber != 0 && correlatedClusterNumber != clusterNumber)
                            .Distinct()
                            .OrderBy(n => n)
                            .ToList();
                        if (correlatedClusterNumbers.Count > 0)
                        {
                            ws.Cells[row, col++].Value = string.Join(", ", correlatedClusterNumbers);
                        }
                        else
                        {
                            col++;
                        }

                        // Note
                        if (!string.IsNullOrEmpty(match.Match.Note))
                        {
                            ws.Cells[row, col++].Value = match.Match.Note;
                        }
                        else
                        {
                            col++;
                        }

                        // Correlation data
                        foreach (var coordAndIndex in leafNode.GetCoordsArray(orderedIndexes).Zip(orderedIndexes, (c, i) => new { Coord = c, Index = i }))
                        {
                            if (coordAndIndex.Coord != 0)
                            {
                                ws.Cells[row, col].Value = coordAndIndex.Coord;
                            }
                            col++;
                        }

                        _progressData.Increment();
                    }

                    // Heatmap color scale
                    var correlationData = new ExcelAddress(firstMatrixDataRow, firstMatrixDataColumn, firstMatrixDataRow - 1 + leafNodes.Count, firstMatrixDataColumn - 1 + leafNodes.Count);
                    var threeColorScale = ws.ConditionalFormatting.AddThreeColorScale(correlationData);
                    threeColorScale.LowValue.Type = eExcelConditionalFormattingValueObjectType.Num;
                    threeColorScale.LowValue.Value = 0;
                    threeColorScale.LowValue.Color = Color.Gainsboro;
                    threeColorScale.MiddleValue.Type = eExcelConditionalFormattingValueObjectType.Num;
                    threeColorScale.MiddleValue.Value = 1;
                    threeColorScale.MiddleValue.Color = Color.Cornsilk;
                    threeColorScale.HighValue.Type = eExcelConditionalFormattingValueObjectType.Num;
                    threeColorScale.HighValue.Value = 2;
                    threeColorScale.HighValue.Color = Color.DarkRed;

                    // Heapmap number format
                    ws.Cells[$"1:{leafNodes.Count}"].Style.Numberformat.Format = "General";

                    // Column widths
                    ws.DefaultColWidth = 19.0 / 7; // 2
                    foreach (var column in autofitColumns)
                    {
                        ws.Column(column).AutoFit();
                    }

                    // Freeze the column and row headers
                    ws.View.FreezePanes(firstMatrixDataRow, firstMatrixDataColumn);
                });

                while (true)
                {
                    try
                    {
                        p.SaveAs(new FileInfo(_correlationFilename));
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (MessageBox.Show(
                            $"An error occurred while saving {_correlationFilename}:{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Try again?",
                            "File error",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        {
                            throw;
                        }
                    }
                }
            }
        }
    }
}
