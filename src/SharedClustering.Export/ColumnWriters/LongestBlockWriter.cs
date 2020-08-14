using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;

namespace SharedClustering.Export.ColumnWriters
{
    public class LongestBlockWriter : IColumnWriter
    {
        public string Header => "Longest Block";
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => true;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.LongestBlock;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
