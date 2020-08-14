using SharedClustering.HierarchicalClustering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedClustering.Export.CorrelationWriters
{
    /// <summary>
    /// Write a correlation matrix to an external file.
    /// </summary>
    public interface ICorrelationWriter : IDisposable
    {
        int MaxColumns { get; }

        IDisposable BeginWriting();
        Task<List<string>> OutputCorrelationAsync(ClusterAnalyzer clusterAnalyzer);
        string SaveFile(int fileNum);
    }
}
