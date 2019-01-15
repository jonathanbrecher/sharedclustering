using System;

namespace AncestryDnaClustering.Models.HierarchicalClustering
{
    public class Neighbor : IComparable<Neighbor>
    {
        public Node Node { get; set; }
        public Node Parent { get; set; }
        public double DistanceSquared { get; set; }

        public Neighbor(Node node, Node parent)
        {
            Node = node;
            Parent = parent;
            DistanceSquared = node.DistanceTo(parent);
        }

        public int CompareTo(Neighbor other)
        {
            return DistanceSquared.CompareTo(other.DistanceSquared);
        }
    }
}
