using System;
using AncestryDnaClustering.Models.HierarchicalClustering;

namespace AncestryDnaClustering.Models.DistanceFinding
{
    public interface IDistanceWriter : IDisposable
    {
        void WriteHeader(IClusterableMatch match);
        void WriteLine(IClusterableMatch match, int overlapCount);
        void SkipLine();
        void Save();
    }
}