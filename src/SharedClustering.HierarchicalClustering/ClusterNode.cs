using SharedClustering.HierarchicalClustering.Distance;
using System;
using System.Collections.Generic;
using System.Linq;

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

            // If the distance between the two nodes is infinite, then the four pairwise distances between leaf noes will also all be infinite.
            // Instead, calculate the distance so that the two leaf nodes with the largest number of shared coordinates have ths amallest distance.
            var distanceBasedOnNumSharedCoords = double.IsPositiveInfinity(distance);

            // Calculate the pairwise distances between the two sides of each clustered node.
            var distFirstFirst = distanceBasedOnNumSharedCoords ? -first.FirstLeaf.NumSharedCoords(second.FirstLeaf) : first.FirstLeaf.DistanceTo(second.FirstLeaf);
            var distFirstSecond = second.FirstLeaf == second.SecondLeaf ? distFirstFirst : distanceBasedOnNumSharedCoords ? -first.FirstLeaf.NumSharedCoords(second.SecondLeaf): first.FirstLeaf.DistanceTo(second.SecondLeaf);
            var distSecondFirst = first.FirstLeaf == first.SecondLeaf ? distFirstFirst : distanceBasedOnNumSharedCoords ? -first.SecondLeaf.NumSharedCoords(second.FirstLeaf) : first.SecondLeaf.DistanceTo(second.FirstLeaf);
            var distSecondSecond = second.FirstLeaf == second.SecondLeaf ? distSecondFirst : distanceBasedOnNumSharedCoords ? -first.SecondLeaf.NumSharedCoords(second.SecondLeaf) : first.SecondLeaf.DistanceTo(second.SecondLeaf);

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
