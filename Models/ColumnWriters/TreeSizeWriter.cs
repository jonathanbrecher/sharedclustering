using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters
{
    public class TreeSizeWriter : IColumnWriter
    {
        public string Header => "Tree Size";
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.TreeSize;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
