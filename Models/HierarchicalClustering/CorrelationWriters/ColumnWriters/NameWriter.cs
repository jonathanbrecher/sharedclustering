using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters.ColumnWriters
{
    public class NameWriter : IColumnWriter
    {
        public string Header => "Name";
        public bool RotateHeader => false;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.Name;
    }
}
