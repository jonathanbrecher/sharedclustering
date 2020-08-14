using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.Export.ColumnWriters
{
    public class CorrelatedOverlappingClustersWriter : IColumnWriter
    {
        public string Header => "Correlated Clusters";
        public bool RotateHeader => true;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 15;

        private readonly IReadOnlyCollection<LeafNode> _leafNodes;
        private readonly ISet<int> _immediateFamilyIndexes;
        private readonly IReadOnlyDictionary<int, List<int>> _indexClusterNumbers;
        private readonly ClusterNumbersWriter _clusterNumbersWriter;
        private readonly int _minClusterSize;

        public CorrelatedOverlappingClustersWriter(
            IReadOnlyCollection<LeafNode> leafNodes,
            ISet<int> immediateFamilyIndexes,
            IReadOnlyDictionary<int, List<int>> indexClusterNumbers,
            ClusterNumbersWriter clusterNumbersWriter,
            int minClusterSize)
        {
            _leafNodes = leafNodes;
            _immediateFamilyIndexes = immediateFamilyIndexes;
            _indexClusterNumbers = indexClusterNumbers;
            _clusterNumbersWriter = clusterNumbersWriter;
            _minClusterSize = minClusterSize;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            var clusterNumbers = _clusterNumbersWriter.GetClusterNumbers(match);
            if (clusterNumbers == null)
            {
                return;
            }

            var correlatedClusterNumbers = _leafNodes
                .Where(leafNode2 => !_immediateFamilyIndexes.Contains(leafNode2.Index)
                                    && leafNode.Coords.TryGetValue(leafNode2.Index, out var correlationValue) && correlationValue >= 1)
                .SelectMany(leafNode2 => _indexClusterNumbers.TryGetValue(leafNode2.Index, out var c) ? c : Enumerable.Empty<int>())
                .Where(c => !clusterNumbers.Contains(c))
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

        public void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress) { }
    }
}
