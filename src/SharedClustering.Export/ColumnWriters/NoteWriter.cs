using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;

namespace SharedClustering.Export.ColumnWriters
{
    public class NoteWriter : IColumnWriter
    {
        public string Header => "Note";
        public bool RotateHeader => false;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = !string.IsNullOrEmpty(match.Match.Note) ? match.Match.Note : null;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
