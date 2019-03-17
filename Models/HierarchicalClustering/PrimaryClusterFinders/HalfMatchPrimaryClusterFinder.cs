using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.Models.HierarchicalClustering.PrimaryClusterFinders
{
    /// <summary>
    /// Identify clusters where each primary cluster is the largest subcluster where the first and last leaf nodes
    /// have some correlation (direct or indirect) with more than half of all leaf nodes in the cluster.
    /// </summary>
    public class HalfMatchPrimaryClusterFinder : IPrimaryClusterFinder
    {
        public IEnumerable<ClusterNode> GetPrimaryClusters(Node node)
        {
            if (!(node is ClusterNode clusterNode))
            {
                return Enumerable.Empty<ClusterNode>();
            }

            var leafNodes = clusterNode.GetOrderedLeafNodes();
            var matchIndexes = new HashSet<int>(leafNodes.Select(leafNode => leafNode.Index));

            var firstAndLastLeafNodes = new[] { clusterNode.FirstLeaf, clusterNode.SecondLeaf };

            if (firstAndLastLeafNodes.All(leafNode => leafNode.Coords.Keys.Intersect(matchIndexes).Count() >= (matchIndexes.Count + 1) / 2))
            {
                return new[] { clusterNode };
            }

            return GetPrimaryClusters(clusterNode.First).Concat(GetPrimaryClusters(clusterNode.Second));
        }
    }
}
