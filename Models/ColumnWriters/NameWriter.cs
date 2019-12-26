using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters
{
    public class NameWriter : IColumnWriter
    {
        public string Header => "Name";
        public bool RotateHeader => false;
        public bool IsAutofit { get; }
        public bool IsDecimal => false;
        public double Width => 15;

        public NameWriter(bool isAutofit)
        {
            IsAutofit = isAutofit;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.Name;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
