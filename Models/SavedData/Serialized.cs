using System.Collections.Generic;

namespace AncestryDnaClustering.Models.SavedData
{
    public class Serialized
    {
        public string TestTakerTestId { get; set; }
        public List<Match> Matches { get; set; }
        public Dictionary<string, int> MatchIndexes { get; set; }
        public Dictionary<string, List<int>> Icw { get; set; }
    }
}
