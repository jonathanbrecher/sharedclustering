using SharedClustering.Core;
using SharedClustering.HierarchicalClustering.Distance;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedClustering.HierarchicalClustering
{
    public interface IClusterBuilder
    {
        Task<List<ClusterNode>> BuildClustersAsync(
            IReadOnlyCollection<IClusterableMatch> clusterableMatches,
            IReadOnlyDictionary<int, float[]> matrix,
            IDistanceMetric distanceMetric,
            IProgressData progressData);
    }
}