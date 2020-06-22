using SharedClustering.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.SavedData
{
    public class Serialized
    {
        public string TestTakerTestId { get; set; }
        public List<Tag> Tags { get; set; }
        public List<Match> Matches { get; set; }
        public Dictionary<string, int> MatchIndexes { get; set; }
        public Dictionary<string, List<int>> Icw { get; set; }

        // Validate integrity of incoming data.
        public string Validate()
        {
            if (Matches.Count == 0)
            {
                return "Invalid data: No matches found.";
            }

            // All matches must have an index.
            var matchesWithoutIndexes = Matches.Where(match => !MatchIndexes.ContainsKey(match.TestGuid)).ToList();
            if (matchesWithoutIndexes.Count > 0)
            {
                return $"Invalid data: {matchesWithoutIndexes.Count} lack indexes.";
            }

            // All matches must have ICW data (at the very least, each match is in common with itself).
            var matchesWithoutIcw = Matches.Where(match => !Icw.ContainsKey(match.TestGuid)).ToList();
            if (matchesWithoutIcw.Count > 0)
            {
                return $"Invalid data: {matchesWithoutIcw.Count} lack ICW data.";
            }

            var sortedIndexes = MatchIndexes.Values.OrderBy(index => index).ToList();

            if (sortedIndexes.First() != 0)
            {
                return "Invalid data: Match indexes are not zero-based.";
            }

            if (sortedIndexes.Distinct().Count() != sortedIndexes.Count)
            {
                return $"Invalid data: Duplicate match indexes found.";
            }

            if (sortedIndexes.Last() != sortedIndexes.Count - 1)
            {
                return $"Invalid data: Match indexes are not consecutive.";
            }

            return null;
        }

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
                kvp => kvp.Value
                    .Select(index => originalIndexesInverted.TryGetValue(index, out var originalIndex) && MatchIndexes.TryGetValue(originalIndex, out var matchIndex) ? matchIndex : -1)
                    .Where(index => index != -1)
                    .ToList());
        }
    }
}
