using System;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters.ColumnWriters
{
    public class LinkWriter : IColumnWriter
    {
        public string Header => "Link";
        public bool RotateHeader => false;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        public string _testTakerTestId;

        public LinkWriter(string testTakerTestId)
        {
            _testTakerTestId = testTakerTestId;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            cell.StyleName = "HyperLink";
            cell .Hyperlink = new ExcelHyperLink($"https://www.ancestry.com/dna/tests/{_testTakerTestId}/match/{match.Match.TestGuid}", UriKind.Absolute) { Display = "Link" };
        }
    }
}
