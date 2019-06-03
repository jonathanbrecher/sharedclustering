using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters
{
    public class CorrelatedClustersWriter : IColumnWriter
    {
        public string Header => "Correlated Clusters";
        public bool RotateHeader => true;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 15;

        private readonly List<LeafNode> _leafNodes;
        private readonly HashSet<int> _immediateFamilyIndexes;
        private readonly Dictionary<int, int> _indexClusterNumbers;
        private readonly ClusterNumberWriter _clusterNumberWriter;
        private readonly int _minClusterSize;

        public CorrelatedClustersWriter(List<LeafNode> leafNodes, HashSet<int> immediateFamilyIndexes, Dictionary<int, int> indexClusterNumbers, ClusterNumberWriter clusterNumberWriter, int minClusterSize)
        {
            _leafNodes = leafNodes;
            _immediateFamilyIndexes = immediateFamilyIndexes;
            _indexClusterNumbers = indexClusterNumbers;
            _clusterNumberWriter = clusterNumberWriter;
            _minClusterSize = minClusterSize;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            var clusterNumber = _clusterNumberWriter.GetClusterNumber(match);
            var correlatedClusterNumbers = _leafNodes
                .Where(leafNode2 => !_immediateFamilyIndexes.Contains(leafNode2.Index)
                                    && leafNode.Coords.TryGetValue(leafNode2.Index, out var correlationValue) && correlationValue >= 1)
                .Select(leafNode2 => _indexClusterNumbers.TryGetValue(leafNode2.Index, out var correlatedClusterNumber) ? correlatedClusterNumber : 0)
                .Where(correlatedClusterNumber => correlatedClusterNumber != 0 && correlatedClusterNumber != clusterNumber)
                .GroupBy(n => n)
                .Where(g => g.Count() >= _minClusterSize)
                .Select(g => g.Key)
                .OrderBy(n => n)
                .ToList();
            if (correlatedClusterNumbers.Count > 0)
            {
                cell.Value = string.Join(", ", correlatedClusterNumbers);
            }
        }
    }
}
