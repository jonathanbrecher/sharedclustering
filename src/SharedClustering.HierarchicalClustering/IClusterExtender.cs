using SharedClustering.HierarchicalClustering.Distance;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedClustering.HierarchicalClustering
{
    public interface IClusterExtender
    {
        Task<List<ClusterNode>> MaybeExtendAsync(
            List<ClusterNode> nodes,
            int maxIndex,
            IReadOnlyCollection<IClusterableMatch> clusterableMatches,
            IReadOnlyCollection<Node> primaryClusters,
            double minCentimorgansToCluster,
            IDistanceMetric distanceMetric,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            ConcurrentDictionary<int, float[]> matrix);
    }
}