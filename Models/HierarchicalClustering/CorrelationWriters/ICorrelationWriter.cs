using System.Collections.Generic;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalCustering;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters
{
    /// <summary>
    /// Write a correlation matrix to an external file.
    /// </summary>
    public interface ICorrelationWriter
    {
        int MaxColumns { get; }
        int MaxColumnsPerSplit { get; }
        Task OutputCorrelationAsync(List<ClusterNode> nodes, Dictionary<int, IClusterableMatch> matchesByIndex, Dictionary<int, int> indexClusterNumbers);
    }
}
