using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering.PrimaryClusterFinders
{
    /// <summary>
    /// Identify clusters in a pseudo-visual approach, allowing overlap between clusters.
    /// </summary>
    public class MatchMatrix
    {
        private readonly bool[][] _matrix;
        private readonly int _minClusterSize;

        public List<int> ImmediateFamilyMatches { get; } = new List<int>();

        public MatchMatrix(bool[][] matrix, int minClusterSize)
        {
            _matrix = matrix;
            _minClusterSize = Math.Max(2, minClusterSize);
        }

        internal IEnumerable<(int Start, int End)> GetClusters()
        {
            var clusters = new Stack<IntRange>();
            var nextIndexToCluster = 0;
            var maximumInvalidSize = _minClusterSize - 1;
            while (nextIndexToCluster < _matrix.Length)
            {
                // Start by considering a 1 x 1 cluster.
                var range = new IntRange(nextIndexToCluster, nextIndexToCluster);

                // Try to extend the cluster by increasing the end value.
                while (CanExtendRangeDownward(range, (range.Size + 1) / 2))
                {
                    range = new IntRange(range.Start, range.End + 1);
                    if (!IsValidRangeExtendingDownward(range))
                    {
                        range = new IntRange(range.Start, range.End - maximumInvalidSize);
                        break;
                    }
                }

                // Having tried to extend downward, additionally try extending the cluster upward by decreasing the start value.
                while (CanExtendRangeUpward(range, (range.Size + 1) / (range.Size > 8 ? 3 : 2)))
                {
                    range = new IntRange(range.Start - 1, range.End);
                    if (!IsValidRangeExtendingDownward(range))
                    {
                        var altRange = new IntRange(range.Start, range.End - maximumInvalidSize);
                        if (altRange.Size > maximumInvalidSize
                            && altRange.End > nextIndexToCluster
                            && IsValidRangeExtendingDownward(altRange)
                            && IsValidRangeExtendingUpward(altRange))
                        {
                            range = altRange;
                            if (range.End <= nextIndexToCluster + 1)
                            {
                                break;
                            }
                        }
                        else
                        {
                            range = new IntRange(range.Start + maximumInvalidSize, range.End);
                            break;
                        }
                    }
                }

                // Having extended upward, try to extend downward from the new starting point.
                if (range.Start < nextIndexToCluster)
                {
                    for (var altStart = range.Start; altStart < nextIndexToCluster; ++altStart)
                    {
                        var altRange = new IntRange(altStart, altStart);
                        while (CanExtendRangeDownward(altRange, (range.Size + 1) / 2))
                        {
                            altRange = new IntRange(altRange.Start, altRange.End + 1);
                            if (!IsValidRangeExtendingDownward(altRange))
                            {
                                altRange = new IntRange(altRange.Start, altRange.End - maximumInvalidSize);
                                break;
                            }
                        }
                        if (altRange.End >= nextIndexToCluster)
                        {
                            range = altRange;
                            break;
                        }
                    }
                }

                // If backed up too far, revert to the original value.
                if (range.End < nextIndexToCluster)
                {
                    range = new IntRange(nextIndexToCluster, nextIndexToCluster);
                }

                // Add the new range to the list of recognized clusters.
                clusters = AppendCluster(clusters, range);

                // Continue with the next unexamined match.
                nextIndexToCluster = range.End + 1;
            }

            // Must reverse the clusters to convert the sense of the stack to a normal ascending enumerable.
            return clusters.Reverse().Select(cluster => (cluster.Start, cluster.End));
        }

        /// <summary>
        /// Append a new cluster to the list of known clusters, possibly merging it with the previous known cluster
        /// or modifying the size of the previously known cluster.
        /// </summary>
        private Stack<IntRange> AppendCluster(Stack<IntRange> clusters, IntRange range)
        {
            // Don't allow a single 2x2 overlap at the start.
            if (range.Size > _minClusterSize && NumMatches(range.Start, 1, range.Start + 2, range.Size - 2) == 0)
            {
                range = new IntRange(range.Start + 1, range.End);
            }

            // Exclude any invalid clusters
            if (
                range.Size < _minClusterSize // too small
                || NumMatchesInCluster(range) - range.Size <= range.Size * range.Size / 2 // too porous
            )
            {
                if (!range.AsEnumerable().Any(index => ImmediateFamilyMatches.Contains(index)))
                {
                    return clusters;
                }
            }

            // Don't allow a single 2x2 overlap at the end.
            if (range.Size > _minClusterSize && NumMatches(range.End, 1, range.Start, range.Size - 2) == 0)
            {
                range = new IntRange(range.Start, range.End - 1);
            }

            // Try to merge clusters that are fairly dense in the overlap area.
            // This is a big help for clusters with endogamy, which create fairly porous clusters with boundaries that are difficut to find.
            if (range.Size >= _minClusterSize && clusters.Count > 0 && MatchDensity(clusters.Peek().End + 1, range.End - clusters.Peek().End, clusters.Peek().Start, range.Start - clusters.Peek().Start) > 0.5)
            {
                range = new IntRange(clusters.Peek().Start, range.End);
                if (CanExtendRangeUpward(range, range.Size /2))
                {
                    range = new IntRange(range.Start - 1, range.End);
                }
            }

            // Remove any existing clusters that are fully within the range of the new one.
            while (clusters.Count > 0 && clusters.Peek().Start >= range.Start)
            {
                clusters.Pop();
            }

            // If there is a large overlap, make sure that the previous cluster wasn't over-extended.
            var clustersWithOverlap = new Stack<IntRange>();
            while (clusters.Count > 0 && HasLargeOverlap(clusters.Peek(), range))
            {
                clustersWithOverlap.Push(clusters.Pop());
            }
            foreach (var clusterWithOverlap in clustersWithOverlap)
            {
                var clusterToReAdd = clusterWithOverlap;
                while (clusterToReAdd.End > clusterToReAdd.Start)
                {
                    var nonOverlapWidth = range.Start - clusterToReAdd.Start;
                    var numMatches = NumMatches(clusterToReAdd.End, 1, clusterToReAdd.Start, nonOverlapWidth);
                    if (numMatches < nonOverlapWidth / 2)
                    {
                        clusterToReAdd = new IntRange(clusterToReAdd.Start, clusterToReAdd.End - 1);
                    }
                    else
                    {
                        break;
                    }
                }
                if (clusters.Count == 0 || clusters.Peek().End < clusterToReAdd.End)
                {
                    clusters.Push(clusterToReAdd);
                }
            }

            // Add the new clustrer if there were no previous clusters or if it wasn't contained within the previous cluster.
            if (clusters.Count == 0 || clusters.Peek().End < range.End)
            {
                clusters.Push(range);
            }

            return clusters;
        }

        /// <summary>
        /// Whether two ranges overlap by at least _minClusterSize.
        /// </summary>
        private bool HasLargeOverlap(IntRange cluster1, IntRange cluster2)
            => cluster1 != null && cluster2 != null && cluster1.End >= cluster2.Start + _minClusterSize;

        /// <summary>
        /// Whether a range can be extended downward, considering the number of matches directly below the range in row after the end of the range.
        /// </summary>
        private bool CanExtendRangeDownward(IntRange range, int minMatches)
        {
            // Cannot extend beyond the end of the matrix.
            if (range.End == _matrix.Length - 1)
            {
                return false;
            }

            // Cannot extend if the end of the next row is almost completely empty.
            if (range.Size >= _minClusterSize * 3)
            {
                var numMatchesSecondHalf = NumMatches(range.End + 1, 1, range.End - _minClusterSize * 2 + 1, _minClusterSize * 2);
                if (numMatchesSecondHalf <= 1)
                {
                    return false;
                }
            }

            // Cannot extend if the start of the next row is almost completely empty.
            if (range.Size >= _minClusterSize * 2)
            {
                var numMatchesRowStart = NumMatches(range.End + 1, 1, range.Start, _minClusterSize * 2);
                if (numMatchesRowStart <= 1)
                {
                    return false;
                }
            }

            // Can extend if the total number of matches in the next row meets the required minimum.
            return NumMatches(range.End + 1, 1, range.Start, range.Size) >= minMatches;
        }

        /// <summary>
        /// Whether a range can be extended upward, considering the number of matches directly above the range in row before the end of the range.
        /// </summary>
        private bool CanExtendRangeUpward(IntRange range, int minMatches)
        {
            // Cannot extend before the beginning of the matrix.
            if (range.Start == 0)
            {
                return false;
            }

            if (range.Size > _minClusterSize)
            {
                // Cannot extend if the start of the previous row is completely empty.
                var numMatchesFirstHalf = NumMatches(range.Start - 1, 1, range.Start, range.Size / 2);
                if (numMatchesFirstHalf == 0)
                {
                    return false;
                }

                // Cannot extend if the end of the previous row is almost completely empty.
                if (range.Size >= _minClusterSize * 3)
                {
                    var numMatchesSecondHalf = NumMatches(range.Start - 1, 1, range.End - range.Size / 2, range.Size / 2 + 1);
                    if (numMatchesSecondHalf <= 1)
                    {
                        return false;
                    }
                }
            }

            // Can extend if the total number of matches in the next row meets the required minimum.
            return NumMatches(range.Start - 1, 1, range.Start, range.Size) >= minMatches;
        }

        /// <summary>
        /// Whether the range should be considered a valid cluster, determined by at least one match in the bottom left corner of the cluster.
        /// </summary>
        private bool IsValidRangeExtendingDownward(IntRange range)
        {
            if (range.Size <= _minClusterSize)
            {
                return true;
            }

            // Consider a range of minimumInvalidSize x minimumInvalidSize matches.
            // Range starts at 2 x 2 for _minClusterSize = 3, expands to 3 x 3 when range is at least 10 x 10, etc.
            var minimumInvalidSize = Math.Max(_minClusterSize - 1, range.Size / 10 + 1);

            var numMatches = NumMatches(range.End - minimumInvalidSize + 1, minimumInvalidSize, range.Start, minimumInvalidSize);
            return numMatches > 0;
        }

        /// <summary>
        /// Whether the range should be considered a valid cluster, determined by at least one match in the top left corner of the cluster.
        /// </summary>
        private bool IsValidRangeExtendingUpward(IntRange range)
        {
            if (range.Size <= _minClusterSize)
            {
                return true;
            }

            // Consider a range of minimumInvalidSize x minimumInvalidSize matches.
            // Range starts at 2 x 2 for _minClusterSize = 3, expands to 3 x 3 when range is at least 10 x 10, etc.
            var minimumInvalidSize = Math.Max(_minClusterSize - 1, range.Size / 10 + 1);

            return NumMatches(range.Start, minimumInvalidSize, range.Start, minimumInvalidSize) > 0;
        }

        /// <summary>
        /// The number of matches within a cluster.
        /// </summary>
        private int NumMatchesInCluster(IntRange range) => NumMatches(range.Start, range.Size, range.Start, range.Size);

        /// <summary>
        /// The number of matches within a two dimensional range.
        /// </summary>
        private int NumMatches(int rowStart, int numRows, int colStart, int numCols)
            => Enumerable.Range(rowStart, numRows).Sum(index => Enumerable.Range(colStart, numCols).Count(index2 => _matrix[index][index2]));

        /// <summary>
        /// The fraction of matches (0...1) within a two dimensional range.
        /// </summary>
        private double MatchDensity(int rowStart, int numRows, int colStart, int numCols)
            => numRows <= 0 || numCols <= 0 ? 0 : NumMatches(rowStart, numRows, colStart, numCols) / (double)numRows / numCols;

        /// <summary>
        /// A simple helper class to store and integer range (inclusive).
        /// </summary>
        private class IntRange
        {
            public int Start { get; }
            public int End { get; }

            public IntRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Size => End - Start + 1;

            public IEnumerable<int> AsEnumerable() => Enumerable.Range(Start, Size);

            public override string ToString() => $"({Start}, {End})";
        }
    }
}
