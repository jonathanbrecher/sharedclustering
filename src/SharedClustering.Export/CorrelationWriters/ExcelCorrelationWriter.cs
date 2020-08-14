using OfficeOpenXml;
using OfficeOpenXml.ConditionalFormatting;
using OfficeOpenXml.Style;
using SharedClustering.Core;
using SharedClustering.Export.ColumnWriters;
using SharedClustering.HierarchicalClustering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace SharedClustering.Export.CorrelationWriters
{
    /// <summary>
    /// Write a correlation matrix to an Excel file.
    /// </summary>
    public class ExcelCorrelationWriter : ICorrelationWriter
    {
        private readonly string _correlationFilename;
        private readonly IReadOnlyCollection<Tag> _tags;
        private readonly string _worksheetName;
        private readonly string _testTakerTestId;
        private readonly string _ancestryHostName;
        private readonly int _minClusterSize;
        private readonly ICoreFileUtils _coreFileUtils;
        private readonly IProgressData _progressData;
        private ExcelPackage _p = null;

        public ExcelCorrelationWriter(
            string correlationFilename,
            IReadOnlyCollection<Tag> tags,
            string worksheetName,
            string testTakerTestId,
            string ancestryHostName,
            int minClusterSize,
            int maxMatchesPerClusterFile,
            ICoreFileUtils coreFileUtils,
            IProgressData progressData)
        {
            _correlationFilename = correlationFilename;
            _tags = tags;
            _worksheetName = worksheetName;
            _testTakerTestId = testTakerTestId;
            _ancestryHostName = ancestryHostName;
            _minClusterSize = minClusterSize;
            MaxMatchesPerClusterFile = Math.Min(MaxColumns, maxMatchesPerClusterFile);
            _coreFileUtils = coreFileUtils;
            _progressData = progressData;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _p?.Dispose();
                _p = null;
            }
        }

        public IDisposable BeginWriting()
        {
            _p = new ExcelPackage();
            return _p;
        }

        private bool FileIsOpen() => _p != null;

        public int MaxColumns => 16000;
        public int MaxMatchesPerClusterFile { get; }

        public async Task<List<string>> OutputCorrelationAsync(ClusterAnalyzer clusterAnalyzer)
        {
            if (string.IsNullOrEmpty(_correlationFilename) || clusterAnalyzer.Nodes.Count == 0)
            {
                return new List<string>();
            }

            // Excel has a limit of 16,384 columns.
            // If there are more than 16,000 matches, split into files containing at most 10,000 columns.
            var numOutputFiles = 1;
            if (clusterAnalyzer.NonDistantMatches.Count > MaxMatchesPerClusterFile)
            {
                numOutputFiles = (clusterAnalyzer.NonDistantMatches.Count - 1) / MaxMatchesPerClusterFile + 1;
            }

            _progressData.Reset("Saving clusters", clusterAnalyzer.LeafNodes.Count * numOutputFiles);

            // Fixed columns
            var clusterNumberWriter = new ClusterNumbersWriter(clusterAnalyzer.IndexClusterNumbers);
            var writers = new List<IColumnWriter>
            {
                clusterNumberWriter,
                new NameWriter(false),
                clusterAnalyzer.Matches.Any(match => !string.IsNullOrEmpty(match.Match.TestGuid)) ? new TestIdWriter() : null,
                !string.IsNullOrEmpty(_testTakerTestId) ? new LinkWriter(_testTakerTestId, _ancestryHostName) : null,
                new SharedCentimorgansWriter(),
                clusterAnalyzer.Matches.Any(match => match.Match.SharedSegments > 0) ? new SharedSegmentsWriter() : null,
                clusterAnalyzer.Matches.Any(match => match.Match.LongestBlock > 0) ? new LongestBlockWriter() : null,
                clusterAnalyzer.Matches.Any(match => !string.IsNullOrEmpty(match.Match.TreeUrl)) ? new TreeUrlWriter(_testTakerTestId) : null,
                clusterAnalyzer.Matches.Any(match => match.Match.TreeType != TreeType.Undetermined) ? new TreeTypeWriter() : null,
                clusterAnalyzer.Matches.Any(match => match.Match.TreeSize > 0) ? new TreeSizeWriter() : null,
                clusterAnalyzer.Matches.Any(match => match.Match.CommonAncestors?.Count > 0) ? new CommonAncestorsWriter() : null,
                clusterAnalyzer.Matches.Any(match => match.Match.Starred) ? new StarredWriter() : null,
                clusterAnalyzer.Matches.Any(match => match.Match.HasHint) ? new SharedAncestorHintWriter() : null,
                new CorrelatedOverlappingClustersWriter(clusterAnalyzer.LeafNodes, clusterAnalyzer.ImmediateFamilyIndexes, clusterAnalyzer.IndexClusterNumbers, clusterNumberWriter, _minClusterSize),
            }.Where(writer => writer != null).ToList();
            if (_tags != null)
            {
                writers.AddRange(_tags.OrderBy(tag => tag.Label).Select(tag => new TagWriter(tag)));
            }
            writers.Add(new NoteWriter());

            if (!FileIsOpen())
            {
                return await OutputFiles(_worksheetName, clusterAnalyzer, writers.ToArray(), numOutputFiles);
            }
            else
            {
                await OutputWorksheet(_worksheetName, clusterAnalyzer, writers.ToArray(), 0);
                return new List<string>{ _correlationFilename };
            }
        }

        private async Task<List<string>> OutputFiles(
            string worksheetName,
            ClusterAnalyzer clusterAnalyzer,
            IColumnWriter[] writers,
            int numOutputFiles)
        {
            var files = new List<string>();

            for (var fileNum = 0; fileNum < numOutputFiles; ++fileNum)
            {
                using (var p = BeginWriting())
                {
                    await OutputWorksheet(worksheetName, clusterAnalyzer, writers, fileNum);
                    files.Add(SaveFile(fileNum));
                }
                _p = null;
            }
            return files;
        }

        public string SaveFile(int fileNum)
        {
            var fileName = _correlationFilename;
            if (fileNum > 0)
            {
                fileName = _coreFileUtils.AddSuffixToFilename(fileName, (fileNum + 1).ToString());
            }

            _p.SaveWithRetry(fileName, _coreFileUtils.ShouldRetry, _coreFileUtils.LogException);

            return fileName;
        }

        private Task OutputWorksheet(
            string worksheetName,
            ClusterAnalyzer clusterAnalyzer,
            IColumnWriter[] writers,
            int fileNum)
        {
            return Task.Run(() =>
            {
                var ws = _p.Workbook.Worksheets.Add(worksheetName);

                // Start at the top left of the sheet
                var row = 1;
                var col = 1;

                // Rotate the entire top row by 90 degrees
                ws.Row(row).Style.TextRotation = 90;

                var columnWriters = new ColumnWritersCollection(_p, ws, writers, _testTakerTestId);

                col = columnWriters.WriteHeaders(row, col);

                var firstMatrixDataRow = row + 1;
                var firstMatrixDataColumn = col;

                // Column headers for each match
                var matchColumns = clusterAnalyzer.NonDistantMatches.Skip(fileNum * MaxMatchesPerClusterFile).Take(MaxMatchesPerClusterFile).ToList();
                foreach (var nonDistantMatch in matchColumns)
                {
                    ws.Cells[row, col++].Value = nonDistantMatch.Match.Name;
                }

                // One row for each match
                foreach (var leafNode in clusterAnalyzer.LeafNodes)
                {
                    var match = clusterAnalyzer.MatchesByIndex[leafNode.Index];
                    row++;

                    // Row headers
                    col = 1;
                    col = columnWriters.WriteColumns(row, col, match, leafNode);

                    // Correlation data
                    foreach (var coordAndIndex in leafNode.GetCoordsArray(clusterAnalyzer.OrderedIndexes)
                        .Zip(clusterAnalyzer.OrderedIndexes, (c, i) => new { Coord = c, Index = i })
                        .Skip(fileNum * MaxMatchesPerClusterFile).Take(MaxMatchesPerClusterFile))
                    {
                        if (coordAndIndex.Coord != 0)
                        {
                            ws.Cells[row, col].Value = coordAndIndex.Coord;
                        }
                        col++;
                    }

                    _progressData.Increment();
                }

                if (clusterAnalyzer.LeafNodes.Count > 0 && matchColumns.Count > 0)
                {
                    // Heatmap color scale
                    var correlationData = new ExcelAddress(firstMatrixDataRow, firstMatrixDataColumn, firstMatrixDataRow - 1 + clusterAnalyzer.LeafNodes.Count, firstMatrixDataColumn - 1 + matchColumns.Count);
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

                    // Add borders around clusters
                    var leafNodeIndexes = clusterAnalyzer.LeafNodes.Select((leafNode, index) => (leafNode, index)).ToDictionary(pair => pair.leafNode.Index, pair => pair.index);
                    foreach (var (start, end) in clusterAnalyzer.Clusters)
                    {
                        AddBorder(ws,
                            left: firstMatrixDataColumn + start,
                            top: firstMatrixDataRow + leafNodeIndexes[clusterAnalyzer.NonDistantMatches[start].Index],
                            right: firstMatrixDataColumn + end,
                            bottom: firstMatrixDataRow + leafNodeIndexes[clusterAnalyzer.NonDistantMatches[end].Index],
                            Color.MidnightBlue
                        );
                    }
                }

                // Heatmap number format
                ws.Cells[$"1:{matchColumns.Count}"].Style.Numberformat.Format = "General";

                col = 1;
                col = columnWriters.FormatColumns(row, col, firstMatrixDataRow + clusterAnalyzer.LeafNodes.Count);

                // Freeze the column and row headers
                ws.View.FreezePanes(firstMatrixDataRow, firstMatrixDataColumn);
            });
        }

        private static void AddBorder(ExcelWorksheet ws, int left, int top, int right, int bottom, Color color)
        {
            AddBorder(ws.Cells[top, left, top, right].Style.Border.Top, color);
            AddBorder(ws.Cells[top, right, bottom, right].Style.Border.Right, color);
            AddBorder(ws.Cells[bottom, left, bottom, right].Style.Border.Bottom, color);
            AddBorder(ws.Cells[top, left, bottom, left].Style.Border.Left, color);
        }

        private static void AddBorder(ExcelBorderItem border, Color color)
        {
            border.Style = ExcelBorderStyle.Thick;
            border.Color.SetColor(color);
        }
    }
}
