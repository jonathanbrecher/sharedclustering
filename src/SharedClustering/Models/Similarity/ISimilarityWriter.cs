using System;
using SharedClustering.HierarchicalClustering;

namespace AncestryDnaClustering.Models.SimilarityFinding
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