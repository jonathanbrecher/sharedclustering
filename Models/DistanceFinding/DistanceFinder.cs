using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.DistanceFinding
{
    public class DistanceFinder
    {
        private readonly int _minClusterSize;
        private readonly ProgressData _progressData;

        public DistanceFinder(
            int minClusterSize,
            ProgressData progressData)
        {
            _minClusterSize = minClusterSize;
            _progressData = progressData;
        }

        public async Task FindClosestByDistanceAsync(List<IClusterableMatch> clusterableMatches, string fileName)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());
            _progressData.Reset($"Finding closest chains by distance for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = clusterableMatches
               .SelectMany(match => match.Coords.Select(coord => new { Coord = coord, Match = match }))
               .GroupBy(pair => pair.Coord, pair => pair.Match)
               .ToDictionary(g => g.Key, g => g.ToList());

            var clusters = await Task.Run(() => clusterableMatches
                .AsParallel().AsOrdered()
                .Select(match =>
                {
                    var header = $"{match.Count}" +
                        $"\t{match.Count}" +
                        $"\t{match.Match.SharedCentimorgans:####.0}" +
                        $"\t{match.Match.SharedSegments}" +
                        //$"\t{match.Match.TreeType}" +
                        //$"\t{match.Match.TreeSize}" +
                        $"\t{match.Match.Name}" +
                        $"\t{match.Match.TestGuid}" +
                        $"\t{match.Match.Note}";

                    return CalculateDistance(match.Coords, buckets, match, header, (otherMatch, overlapCount) => overlapCount >= _minClusterSize && overlapCount >= otherMatch.Count / 3, 100);
                })
                .Where(cluster => cluster != null)
                .ToList());

            _progressData.Reset();

            FileUtils.WriteAllLines(fileName, clusters, false);
        }

        public async Task FindClosestByDistanceAsync(List<IClusterableMatch> clusterableMatches, HashSet<int> indexesAsBasis, string fileName)
        {
            var average = clusterableMatches.Average(match => match.Coords.Count());
            _progressData.Reset($"Finding closest chains by distance for {clusterableMatches.Count} matches (average {average:N0} shared matches per match)...", clusterableMatches.Count);

            var buckets = clusterableMatches
                .SelectMany(match => match.Coords.Where(coord => indexesAsBasis.Contains(coord)).Select(coord => new { Coord = coord, Match = match }))
                .GroupBy(pair => pair.Coord, pair => pair.Match)
                .ToDictionary(g => g.Key, g => g.ToList());

            var clusters = await Task.Run(() => CalculateDistance(indexesAsBasis, buckets, null, null, (_, overlapCount) => overlapCount >= _minClusterSize, clusterableMatches.Count));

            _progressData.Reset();

            FileUtils.WriteAllLines(fileName, clusters, false);
        }

        private string CalculateDistance(
            HashSet<int> coords,
            Dictionary<int, List<IClusterableMatch>> buckets,
            IClusterableMatch excludeMatch,
            string header,
            Func<IClusterableMatch, int, bool> inclusionFunc,
            int maxClusterSize)
        {
            var sb = new StringBuilder();

            if (header != null)
            {
                sb.AppendLine(header);
            }

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
                return null;
            }

            foreach (var closestMatch in results)
            {
                sb.AppendLine(//$"{Math.Sqrt(closestMatch.DistSquared):N2}\t" +
                    $"{closestMatch.OtherMatch.Count}" +
                    $"\t{closestMatch.OverlapCount}" +
                    $"\t{closestMatch.OtherMatch.Match.SharedCentimorgans:####.0}" +
                    $"\t{closestMatch.OtherMatch.Match.SharedSegments}" +
                    //$"\t{closestMatch.OtherMatch.Match.TreeType}" +
                    //$"\t{closestMatch.OtherMatch.Match.TreeSize}" +
                    $"\t{closestMatch.OtherMatch.Match.Name}" +
                    $"\t{closestMatch.OtherMatch.Match.TestGuid}" +
                    $"\t{closestMatch.OtherMatch.Match.Note}");
            }
            sb.AppendLine();
            _progressData.Increment();
            return sb.ToString();
        }
    }
}