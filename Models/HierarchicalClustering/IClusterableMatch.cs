using System;
using System.Collections.Generic;
using AncestryDnaClustering.Models.SavedData;

namespace AncestryDnaClustering.Models.HierarchicalCustering
{
    public interface IClusterableMatch : IComparable
    {
        int Index { get; }
        Match Match { get; }
        HashSet<int> Coords { get; }
        int Count { get; }
    }
}
