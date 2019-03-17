using System.Collections.Generic;
using System.Linq;
using AncestryDnaClustering.Models.HierarchicalClustering.Distance;

namespace AncestryDnaClustering.Models.HierarchicalClustering
{
    /// <summary>
    /// A type of Node that represents a single match.
    /// </summary>
    public class LeafNode : Node
    {
        // The index of the match.
        public int Index { get; set; }

        public LeafNode(int index, IEnumerable<int> coords, IDistanceMetric distanceMetric) : base(1, distanceMetric)
        {
            Index = index;
            Coords = coords.ToDictionary(coord => coord, _ => 1.0);
            FirstLeaf = this;
            SecondLeaf = this;
        }

        public LeafNode(int index, IEnumerable<double> coords, IDistanceMetric distanceMetric) : base(1, distanceMetric)
        {
            Index = index;
            Coords = coords.Select((coord, i) => new { i, coord }).Where(pair => pair.coord > 0).ToDictionary(pair => pair.i, pair => (double)pair.coord);
            FirstLeaf = this;
            SecondLeaf = this;
        }

        // There is nothing to do when reversing a single leaf node.
        public override void Reverse() { }

        // Leaf nodes never contain any other clusters.
        public override IEnumerable<ClusterNode> GetOrderedClusterNodes() => Enumerable.Empty<ClusterNode>();

        // A leaf node contains only itself.
        public override IEnumerable<LeafNode> GetOrderedLeafNodes()
        {
            yield return this;
        }

        // Get the coordinates in the specified order.
        public IEnumerable<double> GetCoordsArray(List<int> orderedIndexes)
        {
            return orderedIndexes.Select(index => Coords.TryGetValue(index, out var coord) ? coord : 0);
        }
    }
}
