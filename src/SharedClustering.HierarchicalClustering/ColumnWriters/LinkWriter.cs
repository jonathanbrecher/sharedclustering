using System;
using OfficeOpenXml;

namespace SharedClustering.HierarchicalClustering.ColumnWriters
{
    public class LinkWriter : IColumnWriter
    {
        public string Header => "Link";
        public bool RotateHeader => false;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        private readonly string _testTakerTestId;
        private readonly string _ancestryHostName;

        public LinkWriter(string testTakerTestId, string ancestryHostName)
        {
            _testTakerTestId = testTakerTestId;
            _ancestryHostName = ancestryHostName;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            cell.StyleName = "HyperLink";
            cell.Hyperlink = new ExcelHyperLink($"https://{_ancestryHostName}/discoveryui-matches/compare-ng/{_testTakerTestId}/with/{match.Match.TestGuid}/trees", UriKind.Absolute) { Display = "Link" };
            //cell.Hyperlink = new ExcelHyperLink($"https://{_ancestryHostName}/discoveryui-matches/compare-ng/{_testTakerTestId}/with/{match.Match.TestGuid}/sharedmatches", UriKind.Absolute) { Display = "Link" };
        }

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
