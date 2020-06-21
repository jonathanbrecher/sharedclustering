using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedClustering.HierarchicalClustering.CorrelationWriters
{
    /// <summary>
    /// Write a correlation matrix to an external file.
    /// </summary>
    public interface ICorrelationWriter : IDisposable
    {
        int MaxColumns { get; }

        IDisposable BeginWriting();
        Task<List<string>> OutputCorrelationAsync(
            IReadOnlyCollection<ClusterNode> nodes,
            IReadOnlyDictionary<int, IClusterableMatch> matchesByIndex,
            IReadOnlyDictionary<int, int> indexClusterNumbers);
        string SaveFile(int fileNum);
    }
}
