using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.DistanceFinding
{
    public class DistanceFinder
    {
        private readonly int _minClusterSize;
        private readonly ProgressData _progressData;

        public DistanceFinder(int minClusterSize, ProgressData progressData)
        {
            _minClusterSize = minClusterSize;
            _progressData = progressData;
        }

        public async Task FindClosestByDistanceAsync(List<IClusterableMatch> clusterableMatches, Func<IDistanceWriter> getDistanceWriter)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());
            _progressData.Reset($"Finding closest chains by distance for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = clusterableMatches
               .SelectMany(match => match.Coords.Select(coord => new { Coord = coord, Match = match }))
               .GroupBy(pair => pair.Coord, pair => pair.Match)
               .ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                using (var writer = getDistanceWriter())
                {
                    await Task.Run(() =>
                    {
                        foreach (var match in clusterableMatches)
                        {
                            CalculateDistance(match.Coords, buckets, match, (otherMatch, overlapCount) => overlapCount >= _minClusterSize && overlapCount >= otherMatch.Count / 3, 100, writer);
                        }
                    });

                    writer.Save();
                }
            }
            finally
            {
                _progressData.Reset();
            }
        }

        public async Task FindClosestByDistanceAsync(List<IClusterableMatch> clusterableMatches, HashSet<int> indexesAsBasis, Func<IDistanceWriter> getDistanceWriter)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());
            _progressData.Reset($"Finding closest chains by distance for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = clusterableMatches
                .SelectMany(match => match.Coords.Where(coord => indexesAsBasis.Contains(coord)).Select(coord => new { Coord = coord, Match = match }))
                .GroupBy(pair => pair.Coord, pair => pair.Match)
                .ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                using (var writer = getDistanceWriter())
                {
                    await Task.Run(() => CalculateDistance(indexesAsBasis, buckets, null, (_, overlapCount) => overlapCount >= _minClusterSize, clusterableMatches.Count, writer));
                    writer.Save();
                }
            }
            finally
            {
                _progressData.Reset();
            }
        }

        public void CalculateDistance(
            HashSet<int> coords,
            Dictionary<int, List<IClusterableMatch>> buckets,
            IClusterableMatch excludeMatch,
            Func<IClusterableMatch, int, bool> inclusionFunc,
            int maxClusterSize,
            IDistanceWriter writer)
        {
            writer.WriteHeader(excludeMatch);

            var results = (
                from otherMatch in coords.SelectMany(coord => buckets[coord])
                    .GroupBy(m => m).Where(g => g.Count() >= _minClusterSize).Select(g => g.Key)
                where otherMatch != excludeMatch
                let overlapCount = coords.Intersect(otherMatch.Coords).Count()
                where inclusionFunc(otherMatch, overlapCount)
                select new
                {
                    OtherMatch = otherMatch,
                    OverlapCount = overlapCount,
                }
            )
            .OrderByDescending(pair => (double)pair.OverlapCount * pair.OverlapCount / coords.Count / pair.OtherMatch.Count)
            .Take(maxClusterSize)
            .ToList();

            if (results.Count == 0)
            {
                return;
            }

            foreach (var closestMatch in results)
            {
                writer.WriteLine(closestMatch.OtherMatch, closestMatch.OverlapCount);
            }
            writer.SkipLine();

            _progressData.Increment();
        }
    }
}