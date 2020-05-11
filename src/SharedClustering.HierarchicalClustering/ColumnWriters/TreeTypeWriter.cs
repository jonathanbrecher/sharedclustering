using OfficeOpenXml;

namespace SharedClustering.HierarchicalClustering.ColumnWriters
{
    public class TreeTypeWriter : IColumnWriter
    {
        public string Header => "Tree Type";
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.TreeType;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
