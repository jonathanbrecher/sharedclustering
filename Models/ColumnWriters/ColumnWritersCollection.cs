using System.Drawing;
using System.Linq;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters
{
    public class ColumnWritersCollection
    {
        private readonly ExcelWorksheet _ws;
        private readonly IColumnWriter[] _writers;

        public ColumnWritersCollection(ExcelPackage p, ExcelWorksheet ws, IColumnWriter[] writers, string testTakerTestId)
        {
            _ws = ws;
            _writers = writers;

            const string hyperlinkStyleName = "HyperLink";  // Language-dependent
            if (!string.IsNullOrEmpty(testTakerTestId) && !p.Workbook.Styles.NamedStyles.Any(style => style.Name == hyperlinkStyleName))
            {
                // Google Sheets does not support HyperlinkBase
                // p.Workbook.Properties.HyperlinkBase = new Uri($"https://www.ancestry.com/dna/tests/{_testTakerTestId}/match/");
                var namedStyle = p.Workbook.Styles.CreateNamedStyle(hyperlinkStyleName);
                namedStyle.Style.Font.UnderLine = true;
                namedStyle.Style.Font.Color.SetColor(Color.Blue);
            }
        }

        public int WriteHeaders(int row, int col)
        {
            foreach (var writer in _writers)
            {
                if (!writer.IsAutofit)
                {
                    _ws.Column(col).Width = writer.Width;
                }
                var cell = _ws.Cells[row, col];
                if (!writer.RotateHeader)
                {
                    cell.Style.TextRotation = 0;
                }
                cell.Value = writer.Header;

                ++col;
            }
            return col;
        }

        public int WriteColumns(int row, int col, IClusterableMatch match, LeafNode leafNode)
        {
            foreach (var writer in _writers)
            {
                writer.WriteValue(_ws.Cells[row, col++], match, leafNode);
            }
            return col;
        }

        public int FormatColumns(int row, int col, int numRows)
        {
            _ws.DefaultColWidth = 19.0 / 7; // 2

            foreach (var writer in _writers)
            {
                if (writer.IsDecimal)
                {
                    _ws.Column(col).Style.Numberformat.Format = "0.0";
                }
                if (writer.IsAutofit)
                {
                    _ws.Column(col).AutoFit();
                }

                var column = new ExcelAddress(1, col, numRows, col);
                writer.ApplyConditionalFormatting(_ws, column);

                col++;
            }
            return col;
        }
    }
}
