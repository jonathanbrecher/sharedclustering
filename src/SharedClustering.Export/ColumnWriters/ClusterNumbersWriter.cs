using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;
using System.Collections.Generic;

namespace SharedClustering.Export.ColumnWriters
{
    public class ClusterNumbersWriter : IColumnWriter
    {
        public string Header => "Cluster Numbers";
        public bool RotateHeader => true;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 26.0 / 7;

        public IReadOnlyDictionary<int, List<int>> _indexClusterNumbers;

        public ClusterNumbersWriter(IReadOnlyDictionary<int, List<int>> indexClusterNumbers)
        {
            _indexClusterNumbers = indexClusterNumbers;
        }

        public List<int> GetClusterNumbers(IClusterableMatch match) => _indexClusterNumbers.TryGetValue(match.Index, out var clusterNumbers) ? clusterNumbers : null;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            var clusterNumbers = GetClusterNumbers(match);
            cell.Value = clusterNumbers == null ? null : string.Join(", ", clusterNumbers);
        }

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
