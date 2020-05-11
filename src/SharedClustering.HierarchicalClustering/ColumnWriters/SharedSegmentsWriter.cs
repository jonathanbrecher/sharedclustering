using OfficeOpenXml;

namespace SharedClustering.HierarchicalClustering.ColumnWriters
{
    public class SharedSegmentsWriter : IColumnWriter
    {
        public string Header => "Shared Segments";
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.SharedSegments;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
