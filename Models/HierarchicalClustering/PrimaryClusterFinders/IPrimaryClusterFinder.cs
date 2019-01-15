using System.Collections.Generic;

namespace AncestryDnaClustering.Models.HierarchicalClustering.PrimaryClusterFinders
{
    /// <summary>
    /// Divide an overall hierarchical cluster into primary clusters based on some criteria.
    /// </summary>
    public interface IPrimaryClusterFinder
    {
        IEnumerable<ClusterNode> GetPrimaryClusters(Node node);
    }
}
