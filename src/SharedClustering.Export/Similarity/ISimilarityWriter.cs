using SharedClustering.HierarchicalClustering;
using System;

namespace SharedClustering.Export.Similarity
{
    public interface ISimilarityWriter : IDisposable
    {
        void WriteHeader(IClusterableMatch match);
        void WriteLine(IClusterableMatch match, int overlapCount);
        void SkipLine();
        bool FileLimitReached();
        string Save();
    }
}