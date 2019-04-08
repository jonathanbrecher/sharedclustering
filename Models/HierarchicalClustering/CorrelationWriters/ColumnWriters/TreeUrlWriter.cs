using System;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters.ColumnWriters
{
    public class TreeUrlWriter : IColumnWriter
    {
        public string Header => "Tree";
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        public string _testTakerTestId;

        public TreeUrlWriter(string testTakerTestId)
        {
            _testTakerTestId = testTakerTestId;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            if (!string.IsNullOrWhiteSpace(match.Match.TreeUrl))
            {
                cell.StyleName = "HyperLink";
                cell.Hyperlink = new ExcelHyperLink(match.Match.TreeUrl, UriKind.Absolute) { Display = "Tree" };
            }
        }
    }
}
