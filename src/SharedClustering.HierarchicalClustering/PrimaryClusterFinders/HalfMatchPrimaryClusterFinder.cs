using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering.PrimaryClusterFinders
{
    /// <summary>
    /// Identify clusters where each primary cluster is the largest subcluster where the first and last leaf nodes
    /// have some correlation (direct or indirect) with more than half of all leaf nodes in the cluster.
    /// </summary>
    public class HalfMatchPrimaryClusterFinder : IPrimaryClusterFinder
    {
        public IEnumerable<Node> GetPrimaryClusters(Node node)
        {
            if (node == null)
            {
                return Enumerable.Empty<Node>();
            }

            if (!(node is ClusterNode clusterNode))
            {
                return new[] { node };
            }

            var leafNodes = clusterNode.GetOrderedLeafNodes();
            var matchIndexes = leafNodes.Select(leafNode => leafNode.Index).ToHashSet();

            var firstAndLastLeafNodes = new[] { clusterNode.FirstLeaf, clusterNode.SecondLeaf };

            if (firstAndLastLeafNodes.All(leafNode => leafNode.Coords.Keys.Intersect(matchIndexes).Count() >= (matchIndexes.Count + 1) / 2))
            {
                return new[] { clusterNode };
            }

            return GetPrimaryClusters(clusterNode.First).Concat(GetPrimaryClusters(clusterNode.Second));
        }
    }
}
