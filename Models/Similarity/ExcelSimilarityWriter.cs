using System.Collections.Generic;
using System.Linq;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.SimilarityFinding
{
    public class ExcelSimilarityWriter : ISimilarityWriter
    {
        private readonly string _fileName;
        private ExcelPackage _p;
        private readonly ExcelWorksheet _ws;
        private readonly ColumnWritersCollection _writers;
        private int _row = 1;
        private int _col = 1;
        private readonly GenericObjectWriter _overlapWriter = new GenericObjectWriter("Shared matches with overlap");

        public ExcelSimilarityWriter(string testTakerTestId, List<IClusterableMatch> matches, string fileName, string fileNameSuffix)
        {
            _fileName = string.IsNullOrEmpty(fileNameSuffix) ? fileName : FileUtils.AddSuffixToFilename(fileName, fileNameSuffix);
            _p = new ExcelPackage();
            _ws = _p.Workbook.Worksheets.Add("similarity");
            var writers = new IColumnWriter[]
            {
                new CountWriter(),
                _overlapWriter,
                new NameWriter(true),
                matches.Any(match => !string.IsNullOrEmpty(match.Match.TestGuid)) ? new TestIdWriter() : null,
                !string.IsNullOrEmpty(testTakerTestId) ? new LinkWriter(testTakerTestId) : null,
                new SharedCentimorgansWriter(),
                matches.Any(match => match.Match.SharedSegments > 0) ? new SharedSegmentsWriter() : null,
                matches.Any(match => match.Match.LongestBlock > 0) ? new LongestBlockWriter() : null,
                matches.Any(match => !string.IsNullOrEmpty(match.Match.TreeUrl)) ? new TreeUrlWriter(testTakerTestId) : null,
                matches.Any(match => match.Match.TreeType != SavedData.TreeType.Undetermined) ? new TreeTypeWriter() : null,
                matches.Any(match => match.Match.TreeSize > 0) ? new TreeSizeWriter() : null,
                matches.Any(match => match.Match.Starred) ? new StarredWriter() : null,
                matches.Any(match => match.Match.HasHint) ? new SharedAncestorHintWriter() : null,
                new NoteWriter(),
            }.Where(writer => writer != null).ToArray();
            _writers = new ColumnWritersCollection(_p, _ws, writers, testTakerTestId);

            // Rotate the entire top row by 90 degrees
            _ws.Row(_row).Style.TextRotation = 90;

            _col = _writers.WriteHeaders(_row, _col);
            ++_row;
        }

        ~ExcelSimilarityWriter()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_p != null)
                {
                    _p.Dispose();
                    _p = null;
                }
            }
        }

        public void WriteHeader(IClusterableMatch match)
        {
            if (match == null)
            {
                return;
            }

            _overlapWriter.GenericObject = match.Count;
            _col = 1;
            _col = _writers.WriteColumns(_row, _col, match, null);
            ++_row;
        }

        public void WriteLine(IClusterableMatch match, int overlapCount)
        {
            _overlapWriter.GenericObject = overlapCount;
            _col = 1;
            _col = _writers.WriteColumns(_row, _col, match, null);
            ++_row;
        }

        public void SkipLine()
        {
            ++_row;
        }

        public bool FileLimitReached() => _row > 100000;

        public string Save()
        {
            // Freeze the column and row headers
            _ws.View.FreezePanes(2, 1);

            _writers.FormatColumns(1, 1);

            FileUtils.Save(_p, _fileName);

            return _fileName;
        }
    }
}