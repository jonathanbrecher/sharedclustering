using OfficeOpenXml;
using SharedClustering.Core;
using SharedClustering.HierarchicalClustering;
using System.Drawing;

namespace SharedClustering.Export.ColumnWriters
{
    public class TagWriter : IColumnWriter
    {
        public string Header => _tag.Label;
        public bool RotateHeader => true;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => .75;

        private Tag _tag;
        private static ColorConverter _colorConverter = new ColorConverter();
        private Color _color;

        public TagWriter(Tag tag)
        {
            _tag = tag;
            _color = (Color)_colorConverter.ConvertFromString(_tag.Color);
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = match.Match.TagIds?.Contains(_tag.TagId) == true ? "." : null;

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress)
        {
            var notBlanksRule = ws.ConditionalFormatting.AddNotContainsBlanks(excelAddress);
            notBlanksRule.Style.Font.Color.Color = _color;
            notBlanksRule.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            notBlanksRule.Style.Fill.BackgroundColor.Color = _color;
        }
    }
}
