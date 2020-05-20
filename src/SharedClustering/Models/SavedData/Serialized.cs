using SharedClustering.Core;
using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.Models.SavedData
{
    public class Serialized
    {
        public string TestTakerTestId { get; set; }
        public List<Tag> Tags { get; set; }
        public List<Match> Matches { get; set; }
        public Dictionary<string, int> MatchIndexes { get; set; }
        public Dictionary<string, List<int>> Icw { get; set; }

        // Some external data sources may have unsorted matches.
        public void SortMatchesDescending()
        {
            var originalMatchIndexes = Matches
                .Select((match, index) => (match.TestGuid, index))
                .ToDictionary(pair => pair.TestGuid, pair => pair.index);

            var originalIndexesInverted = originalMatchIndexes.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            Matches = Matches.OrderByDescending(match => match.SharedCentimorgans).ToList();

            MatchIndexes = Matches
                .Select((match, index) => (match.TestGuid, index))
                .ToDictionary(pair => pair.TestGuid, pair => pair.index);

            Icw = Icw.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(index => MatchIndexes[originalIndexesInverted[index]]).ToList());
        }
    }
}
