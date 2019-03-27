using System.Collections.Generic;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters.ColumnWriters
{
    public class ClusterNumberWriter : IColumnWriter
    {
        public string Header => "Cluster Number";
        public bool RotateHeader => true;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 26.0 / 7;

        public Dictionary<int, int> _indexClusterNumbers;

        public ClusterNumberWriter(Dictionary<int, int> indexClusterNumbers)
        {
            _indexClusterNumbers = indexClusterNumbers;
        }

        public int? GetClusterNumber(IClusterableMatch match) => _indexClusterNumbers.TryGetValue(match.Index, out var clusterNumber) ? clusterNumber : (int?)null;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            cell.Value = GetClusterNumber(match);
        }
    }
}
