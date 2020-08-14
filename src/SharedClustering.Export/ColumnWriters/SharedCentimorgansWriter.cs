using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;

namespace SharedClustering.Export.ColumnWriters
{
    public class SharedCentimorgansWriter : IColumnWriter
    {
        public string Header => "Shared Centimorgans";
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => true;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.SharedCentimorgans;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
