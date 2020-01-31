using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.HierarchicalClustering.MatrixBuilders
{
    /// <summary>
    /// Build a matrix that is weighted as a fraction of total appearances.
    /// If two matches (A) and (B) do _not_ appear on each other's shared match lists,
    /// then the matrix value is in the range 0...1
    /// representing the fraction of matches containing (A) in their shared match list also contain (B).
    /// If two matches (A) and (B) _do_ appear on each other's shared match lists,
    /// then the matrix value is in the range 1...2
    /// representing 1 + the fraction of matches as above.
    /// 
    /// In other words, the higher the matrix value, the more likely two matches appear together,
    /// with an additional +1 bump if the matches appear on each other's shared match lists.
    /// </summary>
    public class AppearanceWeightedMatrixBuilder : IMatrixBuilder
    {
        private readonly ProgressData _progressData;
        private readonly double _lowestClusterableCentimorgans;
        private readonly double _maxIndirectPercentage;

        public AppearanceWeightedMatrixBuilder(double lowestClusterableCentimorgans, double maxIndirectPercentage, ProgressData progressData)
        {
            _lowestClusterableCentimorgans = lowestClusterableCentimorgans;
            _maxIndirectPercentage = Math.Min(100, Math.Max(0, maxIndirectPercentage));
            _progressData = progressData;
        }

        public Task<ConcurrentDictionary<int, float[]>> CorrelateAsync(List<IClusterableMatch> clusterableMatches, List<IClusterableMatch> immediateFamily)
        {
            _progressData.Reset("Correlating data...", clusterableMatches.Count);

            return Task.Run(async () =>
            {
                var matchIndexes = new HashSet<int>(clusterableMatches.Select(match => match.Index));

                // Skip over any immediate family matches. Immediate family matches tend to have huge numbers of shared matches.
                // If the immediate family are included, the entire cluster diagram will get swamped with low-level
                // indirect matches (gray cells in the final), obscuring the useful clusters. 
                // The immediate family matches will still be included in the cluster diagram
                // by virtue of the other matches that are shared directly with them.
                clusterableMatches = clusterableMatches.Skip(immediateFamily.Count).ToList();

                // Count how often each match appears in any match's match list.
                // Every match appears at least once, in its own match list.
                var appearances = clusterableMatches
                    .SelectMany(match => match.Coords)
                    .Where(index => matchIndexes.Contains(index))
                    .GroupBy(index => index)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Matches below 20 cM never appear in a shared match list on Ancestry,
                // so only the stronger matches can be clustered.
                var clusterableMatchesOverLowestClusterableCentimorgans = clusterableMatches
                    .Where(match => match.Match.SharedCentimorgans >= _lowestClusterableCentimorgans)
                    .ToList();
                var maxIndex = clusterableMatchesOverLowestClusterableCentimorgans.Max(match => Math.Max(match.Index, match.Coords.Max()));

                var matrix = new ConcurrentDictionary<int, float[]>();

                // For the immediate family, populate the matrix based only on direct shared matches.
                var immediateFamilyTasks = immediateFamily.Select(match => Task.Run(() =>
                {
                    ExtendMatrixDirect(matrix, match, maxIndex, maxIndex, 1.0f);
                    _progressData.Increment();
                }));
                await Task.WhenAll(immediateFamilyTasks);

                // For the other clusterable matches, populate the matrix based on both the direct and indirect matches.
                var clusterableMatchesTasks = clusterableMatches.Select(match => Task.Run(() =>
                {
                    ExtendMatrixDirect(matrix, match, maxIndex, maxIndex, 1.0f);
                    ExtendMatrixIndirect(matrix, match, appearances, maxIndex);
                    _progressData.Increment();
                }));
                await Task.WhenAll(clusterableMatchesTasks);

                ReduceIndirectCoords(matrix, clusterableMatchesOverLowestClusterableCentimorgans.Count * matrix.Count());

                _progressData.Reset("Done");

                return matrix;
            });
        }

        private void ReduceIndirectCoords(IDictionary<int, float[]> matrix, int totalCoords)
        {
            if (_maxIndirectPercentage >= 100)
            {
                return;
            }

            var numDirectCoords = matrix.Values.Sum(row => row.Count(coord => coord >= 1));
            var numIndirectCoords = matrix.AsParallel().Sum(row => row.Value.Count(coord => coord > 0 && coord < 1));
            var maxAllowedIndirectCoords = (int)((totalCoords - numDirectCoords) * _maxIndirectPercentage);
            
            if (numIndirectCoords > maxAllowedIndirectCoords)
            {
                var minAllowedIndirectCoord = matrix
                    .SelectMany(row => row.Value.Where(coord => coord > 0 && coord < 1))
                    .NthLargest(maxAllowedIndirectCoords);
                foreach (var row in matrix.Values)
                {
                    for (var i = 0; i < row.Length; ++i)
                    {
                        if (row[i] < minAllowedIndirectCoord)
                        {
                            row[i] = 0;
                        }
                    }
                }
            }
        }

        // An indirect match is when two matches A and B appear together on the shared match list of some other match C.
        // Matches are rated on a scale of 0...1
        // If two matches A and B never appear on the same match list, then matrix[A][B] has a value of 0.
        // If match A appears in 4 shared match lists, and match B appears in 3 of those lists, then matrix[A][B] has a value of 0.75
        // If every shared match list that contains match A also contains match B, then matrix[A][B] has a value of 1.
        private static void ExtendMatrixIndirect(ConcurrentDictionary<int, float[]> matrix, IClusterableMatch match, IReadOnlyDictionary<int, int> appearances, int maxIndex)
        {
            foreach (var coord1 in match.Coords)
            {
                if (!appearances.TryGetValue(coord1, out var numAppearances))
                {
                    continue;
                }

                var row = matrix.GetOrAdd(coord1, _ => new float[maxIndex + 1]);

                if (coord1 == match.Index)
                {
                    if (coord1 < row.Length)
                    {
                        row[coord1] += 1.0f / numAppearances;
                    }
                }
                else
                {
                    foreach (var coord2 in match.Coords.Where(coord2 => coord2 != match.Index && coord2 <= maxIndex))
                    {
                        row[coord2] += 1.0f / numAppearances;
                    }
                }
            }
        }

        // A direct match is when match B appears on the shared match list of match A.
        // When the shared match list of match A contains match B, then matrix[A][B] is incremented by a given amount.
        private static void ExtendMatrixDirect(ConcurrentDictionary<int, float[]> matrix, IClusterableMatch match, int maxIndex, int maxSharedIndex, float increment)
        {
            if (match.Index <= maxIndex)
            {
                var row = matrix.GetOrAdd(match.Index, _ => new float[maxSharedIndex + 1]);
                foreach (var coord2 in match.Coords.Where(coord2 => coord2 < row.Length))
                {
                    row[coord2] += increment;
                }
            }
        }

        public void ExtendMatrix(
            ConcurrentDictionary<int, float[]> matrix,
            List<IClusterableMatch> clusterableMatches,
            int maxIndex)
        {
            var maxSharedIndex = matrix.Max(row => row.Value.Length) - 1;
            foreach (var match in clusterableMatches)
            {
                ExtendMatrixDirect(matrix, match, maxIndex, maxSharedIndex, 1.0f);
            }
        }
    }
}
