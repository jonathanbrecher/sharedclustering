using System;

namespace AncestryDnaClustering.Models.HierarchicalClustering
{
    // Intentionally a struct rather than a class, to minimize memory usage
    public struct Neighbor : IComparable<Neighbor>
    {
        public Node Node { get; set; }
        public float DistanceSquared { get; set; }

        public Neighbor(Node node, Node parent)
        {
            Node = node;
            DistanceSquared = (float)node.DistanceTo(parent);
        }

        public int CompareTo(Neighbor other)
        {
            return DistanceSquared.CompareTo(other.DistanceSquared);
        }
    }
}
