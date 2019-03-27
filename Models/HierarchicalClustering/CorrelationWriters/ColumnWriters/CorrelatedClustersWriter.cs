using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters.ColumnWriters
{
    public class CorrelatedClustersWriter : IColumnWriter
    {
        public string Header => "Correlated Clusters";
        public bool RotateHeader => true;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 15;

        private List<LeafNode> _leafNodes;
        private HashSet<int> _immediateFamilyIndexes;
        private Dictionary<int, int> _indexClusterNumbers;
        private ClusterNumberWriter _clusterNumberWriter;

        public CorrelatedClustersWriter(List<LeafNode> leafNodes, HashSet<int> immediateFamilyIndexes, Dictionary<int, int> indexClusterNumbers, ClusterNumberWriter clusterNumberWriter)
        {
            _leafNodes = leafNodes;
            _immediateFamilyIndexes = immediateFamilyIndexes;
            _indexClusterNumbers = indexClusterNumbers;
            _clusterNumberWriter = clusterNumberWriter;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            var clusterNumber = _clusterNumberWriter.GetClusterNumber(match);
            var correlatedClusterNumbers = _leafNodes
                .Where(leafNode2 => !_immediateFamilyIndexes.Contains(leafNode2.Index)
                                    && leafNode.Coords.TryGetValue(leafNode2.Index, out var correlationValue) && correlationValue >= 1)
                .Select(leafNode2 => _indexClusterNumbers.TryGetValue(leafNode2.Index, out var correlatedClusterNumber) ? correlatedClusterNumber : 0)
                .Where(correlatedClusterNumber => correlatedClusterNumber != 0 && correlatedClusterNumber != clusterNumber)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            if (correlatedClusterNumbers.Count > 0)
            {
                cell.Value = string.Join(", ", correlatedClusterNumbers);
            }
        }
    }
}
