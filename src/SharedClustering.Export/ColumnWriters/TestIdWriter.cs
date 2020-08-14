using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;

namespace SharedClustering.Export.ColumnWriters
{
    public class TestIdWriter : IColumnWriter
    {
        public string Header => "Test ID";
        public bool RotateHeader => false;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 10;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.TestGuid;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
