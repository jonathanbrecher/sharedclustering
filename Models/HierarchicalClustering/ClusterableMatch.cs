using System.Collections.Generic;
using AncestryDnaClustering.Models.SavedData;

namespace AncestryDnaClustering.Models.HierarchicalClustering
{
    public class ClusterableMatch : IClusterableMatch
    {
        public int Index { get; }
        public Match Match { get; }
        public HashSet<int> Coords { get; }
        public int Count { get; }

        public ClusterableMatch(int index, Match match, ICollection<int> matchIndexes)
        {
            Index = index;
            Match = match;
            Coords = new HashSet<int>(matchIndexes);
            Count = matchIndexes.Count;
        }

        public int CompareTo(object other) => Index.CompareTo(((IClusterableMatch)other).Index);

        public override string ToString() => Match.Name;
    }
}
