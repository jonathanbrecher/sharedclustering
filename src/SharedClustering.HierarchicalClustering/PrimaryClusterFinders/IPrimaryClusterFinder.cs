using System.Collections.Generic;

namespace SharedClustering.HierarchicalClustering.PrimaryClusterFinders
{
    /// <summary>
    /// Divide an overall hierarchical cluster into primary clusters based on some criteria.
    /// </summary>
    public interface IPrimaryClusterFinder
    {
        IEnumerable<Node> GetPrimaryClusters(Node node);
    }
}
