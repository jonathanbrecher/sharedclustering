using System;
using System.Collections.Generic;
using System.Linq;
using AncestryDnaClustering.Models.SavedData;

namespace AncestryDnaClustering.Models.HierarchicalCustering
{
    public class ClusterableMatch : IClusterableMatch
    {
        public int Index { get; }
        public Match Match { get; }
        public HashSet<int> Coords { get; }
        public int Count { get; }

        public ClusterableMatch(int index, Match match, IList<int> matchIndexes)
        {
            Index = index;
            Match = match;
            Coords = new HashSet<int>(matchIndexes);
            Count = matchIndexes.Count;
        }

        public int CompareTo(object other)
        {
            return Index.CompareTo(((IClusterableMatch)other).Index);
        }

        public override string ToString()
        {
            return Match.Name;
        }
    }
}
