using System;

namespace SharedClustering.HierarchicalClustering
{
    // Intentionally a struct rather than a class, to minimize memory usage
    public struct Neighbor : IComparable<Neighbor>
    {
        public Node Node { get; set; }
        public double DistanceSquared { get; set; }

        public Neighbor(Node node, Node parent)
        {
            Node = node;
            DistanceSquared = node.DistanceTo(parent);
        }

        public int CompareTo(Neighbor other) => DistanceSquared.CompareTo(other.DistanceSquared);
    }
}
