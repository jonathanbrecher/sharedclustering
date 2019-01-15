using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalCustering;
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

        public AppearanceWeightedMatrixBuilder(ProgressData progressData)
        {
            _progressData = progressData;
        }

        public Task<ConcurrentDictionary<int, double[]>> CorrelateAsync(List<IClusterableMatch> clusterableMatches, List<IClusterableMatch> immediateFamily)
        {
            _progressData.Reset("Correlating data...", clusterableMatches.Count);

            return Task.Run(() =>
            {
                var matchIndexes = new HashSet<int>(clusterableMatches.Select(match => match.Index));
                var matchesDictionary = clusterableMatches.ToDictionary(match => match.Match.TestGuid);

                // Skip over any immediate family matches. Immediate family matches tend to have huge numbers of shared matches.
                // If the immediate family are included, the entire cluster diagram will get swamped with low-level
                // indirect matches (gray cells in the final), obscuring the useful clusters. 
                // The immediate family matches will still be included in the cluster diagram
                // by virtue of the other matches that are shared directly with them.
                var immediateFamilyIndexes = new HashSet<int>(immediateFamily.Select(match => match.Index));
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
                var maxIndex = clusterableMatches
                    .Where(match => match.Match.SharedCentimorgans >= 20)
                    .Max(match => Math.Max(match.Index, match.Coords.Max()));

                var matrix = new ConcurrentDictionary<int, double[]>();

                // For the immediate family, populate the matrix based only on direct shared matches.
                Parallel.ForEach(immediateFamily, match =>
                {
                    ExtendMatrixDirect(matrix, match, maxIndex, 1.0);

                    _progressData.Increment();
                });

                // For the other clusterable matches, populate the matrix based on both the direct and indirect matches.
                Parallel.ForEach(clusterableMatches, match =>
                {
                    ExtendMatrixDirect(matrix, match, maxIndex, 1.0);
                    ExtendMatrixIndirect(matrix, match, appearances, immediateFamilyIndexes, maxIndex);

                    _progressData.Increment();
                });

                _progressData.Reset("Done");

                return matrix;
            });
        }

        // An indirect match is when two matches A and B appear together on the shared match list of some other match C.
        // Matches are rated on a scale of 0...1
        // If two matches A and B never appear on the same match list, then matrix[A][B] has a value of 0.
        // If match A appears in 4 shared match lists, and match B appears in 3 of those lists, then matrix[A][B] has a value of 0.75
        // If every shared match list that contains match A also contains match B, then matrix[A][B] has a value of 1.
        private void ExtendMatrixIndirect(ConcurrentDictionary<int, double[]> matrix, IClusterableMatch match, Dictionary<int, int> appearances, HashSet<int> strongMatchesIndexes, int maxIndex)
        {
            foreach (var coord1 in match.Coords)
            {
                if (!appearances.TryGetValue(coord1, out var numAppearances))
                {
                    continue;
                }

                var row = matrix.GetOrAdd(coord1, _ => new double[maxIndex + 1]);

                if (coord1 == match.Index)
                {
                    if (coord1 < row.Length)
                    {
                        row[coord1] += 1.0 / numAppearances;
                    }
                }
                else
                {
                    foreach (var coord2 in match.Coords.Where(coord2 => coord2 != match.Index && coord2 <= maxIndex))
                    {
                        row[coord2] += 1.0 / numAppearances;
                    }
                }
            }
        }

        // A direct match is when match B appears on the shared match list of match A.
        // When the shared match list of match A contains match B, then matrix[A][B] is incremented by a given amount.
        private void ExtendMatrixDirect(ConcurrentDictionary<int, double[]> matrix, IClusterableMatch match, int maxIndex, double increment)
        {
            if (match.Index <= maxIndex)
            {
                var row = matrix.GetOrAdd(match.Index, _ => new double[maxIndex + 1]);
                foreach (var coord2 in match.Coords.Where(coord2 => coord2 < row.Length))
                {
                    row[coord2] += increment;
                }
            }
        }

        public void ExtendMatrix(
            ConcurrentDictionary<int, double[]> matrix,
            List<IClusterableMatch> clusterableMatches,
            int maxIndex)
        {
            foreach (var match in clusterableMatches)
            {
                ExtendMatrixDirect(matrix, match, maxIndex, 1.0);
            }
        }
    }
}
