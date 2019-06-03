using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters
{
    public class TestIdWriter : IColumnWriter
    {
        public string Header => "Test ID";
        public bool RotateHeader => false;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 10;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.TestGuid;
    }
}
