using System.Collections.Generic;
using System.Linq;
using SharedClustering.HierarchicalClustering.Distance;

namespace SharedClustering.HierarchicalClustering
{
    /// <summary>
    /// An abstract class that represents either a single match or a cluster of matches.
    /// </summary>
    public abstract class Node
    {
        public int NumChildren { get; protected set; }
        public IReadOnlyDictionary<int, double> Coords { get; set; }
        public List<Neighbor> NeighborsByDistance { get; set; }
        public ClusterNode Parent { get; set; }

        // The two leaf nodes that define the edges of this node.
        // The ordering of the two nodes is arbitrary, but the first node will appear above / left of the second node in the final chart.
        public LeafNode FirstLeaf { get; protected set; }
        public LeafNode SecondLeaf { get; protected set; }

        private readonly IDistanceMetric _distanceMetric;

        protected Node(int numChildren, IDistanceMetric distanceMetric)
        {
            NumChildren = numChildren;
            _distanceMetric = distanceMetric;
        }

        public abstract void Reverse();

        public abstract IEnumerable<ClusterNode> GetOrderedClusterNodes();

        public abstract IEnumerable<LeafNode> GetOrderedLeafNodes();

        public HashSet<int> GetOrderedLeafNodesIndexes() => GetOrderedLeafNodes().Select(leafNode => leafNode.Index).ToHashSet();

        public IEnumerable<ClusterNode> GetParents()
        {
            for (var parent = Parent; parent != null; parent = parent.Parent)
            {
                yield return parent;
            }
        }

        public Node GetHighestParent() => Parent?.GetHighestParent() ?? this;

        public double DistanceTo(Node other) => _distanceMetric.Calculate(Coords, other.Coords);

        public HashSet<int> AllSignificantLeafCoords() => GetOrderedLeafNodes().SelectMany(leafNode => _distanceMetric.SignificantCoordinates(leafNode.Coords)).ToHashSet();
        
        public int NumSharedCoords(Node other) => AllSignificantLeafCoords().Intersect(other.AllSignificantLeafCoords()).Count();
    }
}
