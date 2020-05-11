using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedClustering.HierarchicalClustering.MatrixBuilders
{
    public interface IMatrixBuilder
    {
        /// <summary>
        /// Generate a correlation matrix.
        /// </summary>
        Task<ConcurrentDictionary<int, float[]>> CorrelateAsync(
            List<IClusterableMatch> clusterableMatches,
            List<IClusterableMatch> immediateFamily
        );

        /// <summary>
        /// Add further matches to an existing correlation matrix.
        /// </summary>
        void ExtendMatrix(
            ConcurrentDictionary<int, float[]> matrix,
            List<IClusterableMatch> clusterableMatches,
            int maxIndex
        );
    }
}
