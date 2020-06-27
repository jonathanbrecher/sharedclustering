using System;
using System.Collections.Generic;
using System.Linq;
using SharedClustering.HierarchicalClustering.Distance;

namespace SharedClustering.HierarchicalClustering
{
    /// <summary>
    /// A type of Node that represents a hierarchical cluster of matches.
    /// </summary>
    public class ClusterNode : Node
    {
        // The two nodes that define the hierarchical cluster.
        // The ordering of the two nodes is arbitrary, but the first node will appear above / left of the second node in the final chart.
        public Node First { get; private set; }
        public Node Second { get; private set; }

        // The distance between the First and Second nodes
        public double Distance { get; }

        public ClusterNode(Node first, Node second, double distance, IDistanceMetric distanceMetric) : base(first.NumChildren + second.NumChildren, distanceMetric)
        {
            Distance = distance;

            // Calculate the pairwise distances between the two sides of each clustered node.
            var distFirstFirst = GetDirectionalDistance(first.FirstLeaf, second.FirstLeaf, distance);
            var distFirstSecond = second.FirstLeaf == second.SecondLeaf ? distFirstFirst : GetDirectionalDistance(first.FirstLeaf, second.SecondLeaf, distance);
            var distSecondFirst = first.FirstLeaf == first.SecondLeaf ? distFirstFirst : GetDirectionalDistance(first.SecondLeaf, second.FirstLeaf, distance);
            var distSecondSecond = second.FirstLeaf == second.SecondLeaf ? distSecondFirst : GetDirectionalDistance(first.SecondLeaf, second.SecondLeaf, distance);

            // Order the two nodes so that the minimum distance is between them.
            if (Math.Min(distFirstSecond, distSecondFirst) <= Math.Min(distFirstFirst, distSecondSecond))
            {
                if (distSecondFirst <= distFirstSecond)
                {
                    First = first;
                    Second = second;
                }
                else
                {
                    First = second;
                    Second = first;
                }
            }
            else
            {
                if (distSecondSecond <= distFirstFirst)
                {
                    First = first;
                    Second = second;
                    Second.Reverse();
                }
                else
                {
                    First = second;
                    Second = first;
                    First.Reverse();
                }
            }
            First.Parent = this;
            Second.Parent = this;
            FirstLeaf = First.FirstLeaf;
            SecondLeaf = Second.SecondLeaf;
        }

        private double GetDirectionalDistance(LeafNode leaf1, LeafNode leaf2, double distance)
            => distance == double.PositiveInfinity ? leaf1.NumSharedCoords(leaf2) : leaf1.DistanceTo(leaf2);

        // Swap the First and Second nodes, and all of their subnodes.
        public override void Reverse()
        {
            var temp = First;
            First = Second;
            Second = temp;
            First.Reverse();
            Second.Reverse();
            FirstLeaf = First.FirstLeaf;
            SecondLeaf = Second.SecondLeaf;
        }

        // Replace one node with another node.
        public void ReplaceChild(Node originalChild, Node newChild)
        {
            if (originalChild == First)
            {
                First = newChild;
                FirstLeaf = newChild.FirstLeaf;
            }
            if (originalChild == Second)
            {
                Second = newChild;
                SecondLeaf = newChild.SecondLeaf;
            }

            UpdateNumChildren();

            originalChild.Parent = null;
            newChild.Parent = this;
        }

        // Return all child clustered nodes in (depth-first) order.
        // This is the order in which they will be displayed in the final cluster diagram.
        public override IEnumerable<ClusterNode> GetOrderedClusterNodes() => First.GetOrderedClusterNodes().Concat(Second.GetOrderedClusterNodes());

        // Return all terminal leaf nodes in (depth-first) order.
        // This is the order in which they will be displayed in the final cluster diagram.
        public override IEnumerable<LeafNode> GetOrderedLeafNodes()
        {
            foreach (var leafNode in First.GetOrderedLeafNodes())
            {
                yield return leafNode;
            }
            foreach (var leafNode in Second.GetOrderedLeafNodes())
            {
                yield return leafNode;
            }
        }

        protected void UpdateNumChildren()
        {
            NumChildren = First.NumChildren + Second.NumChildren;
            Parent?.UpdateNumChildren();
        }
    }
}
