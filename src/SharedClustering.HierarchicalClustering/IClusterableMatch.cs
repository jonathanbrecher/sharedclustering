using SharedClustering.Core;
using System;
using System.Collections.Generic;

namespace SharedClustering.HierarchicalClustering
{
    public interface IClusterableMatch : IComparable
    {
        int Index { get; }
        Match Match { get; }
        HashSet<int> Coords { get; }
        int Count { get; }
    }
}
