using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AncestryDnaClustering.Models.HierarchicalClustering.MatrixBuilders
{
    public interface IMatrixBuilder
    {
        /// <summary>
        /// Generate a correlation matrix.
        /// </summary>
        Task<ConcurrentDictionary<int, double[]>> CorrelateAsync(
            List<IClusterableMatch> clusterableMatches,
            List<IClusterableMatch> immediateFamily
        );

        /// <summary>
        /// Add further matches to an existing correlation matrix.
        /// </summary>
        void ExtendMatrix(
            ConcurrentDictionary<int, double[]> matrix,
            List<IClusterableMatch> clusterableMatches,
            int maxIndex
        );
    }
}
