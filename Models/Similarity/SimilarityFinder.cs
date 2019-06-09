using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.SimilarityFinding
{
    public class SimilarityFinder
    {
        private readonly int _minClusterSize;
        private readonly ProgressData _progressData;

        public SimilarityFinder(int minClusterSize, ProgressData progressData)
        {
            _minClusterSize = minClusterSize;
            _progressData = progressData;
        }

        public async Task FindClosestBySimilarityAsync(List<IClusterableMatch> clusterableMatches, Func<ISimilarityWriter> getSimilarityWriter)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());
            _progressData.Reset($"Finding closest chains by Similarity for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = clusterableMatches
               .SelectMany(match => match.Coords.Select(coord => new { Coord = coord, Match = match }))
               .GroupBy(pair => pair.Coord, pair => pair.Match)
               .ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                using (var writer = getSimilarityWriter())
                {
                    await Task.Run(() =>
                    {
                        foreach (var match in clusterableMatches)
                        {
                            CalculateSimilarity(match.Coords, buckets, match, (otherMatch, overlapCount) => overlapCount >= _minClusterSize && overlapCount >= otherMatch.Count / 3, 100, writer);
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

        public async Task FindClosestBySimilarityAsync(List<IClusterableMatch> clusterableMatches, HashSet<int> indexesAsBasis, Func<ISimilarityWriter> getSimilarityWriter)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());
            _progressData.Reset($"Finding closest chains by Similarity for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = clusterableMatches
                .SelectMany(match => match.Coords.Where(coord => indexesAsBasis.Contains(coord)).Select(coord => new { Coord = coord, Match = match }))
                .GroupBy(pair => pair.Coord, pair => pair.Match)
                .ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                using (var writer = getSimilarityWriter())
                {
                    await Task.Run(() => CalculateSimilarity(indexesAsBasis, buckets, null, (_, overlapCount) => overlapCount >= _minClusterSize, clusterableMatches.Count, writer));
                    writer.Save();
                }
            }
            finally
            {
                _progressData.Reset();
            }
        }

        public void CalculateSimilarity(
            HashSet<int> coords,
            Dictionary<int, List<IClusterableMatch>> buckets,
            IClusterableMatch excludeMatch,
            Func<IClusterableMatch, int, bool> inclusionFunc,
            int maxClusterSize,
            ISimilarityWriter writer)
        {
            writer.WriteHeader(excludeMatch);

            var results = (
                from otherMatch in coords
                    .Where(coord => buckets.ContainsKey(coord))
                    .SelectMany(coord => buckets[coord])
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