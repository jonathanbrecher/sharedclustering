using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering.Distance
{
    /// <summary>
    /// Measure distance as a variant of the square of Euclidean distance squared (in two dimensions: x^2 + y^2).
    /// Values less than 1 in any dimension are treated as zero.
    /// The sum of shared distance in each dimension is divided by the total scale of each set of dimensions.
    /// This puts a high weight towards similar sets of dimensions, while also incurring some penalty where dimensions differ.
    ///
    /// Additionally, matches from immediate family members are strongly weighted,
    /// in an attempt to force for example all maternal-side relatives to stay together.
    /// Unfortunately, over-weighting the immediate family matches also tends to fragment the more distant clusters.
    /// </summary>
    internal class OverlapAndCloseWeightedEuclideanDistanceSquared : IDistanceMetric
    {
        private readonly List<int> _immediateFamilyIndexes;

        public OverlapAndCloseWeightedEuclideanDistanceSquared(IEnumerable<IClusterableMatch> immediateFamily)
        {
            _immediateFamilyIndexes = immediateFamily.Select(match => match.Index).ToList();
        }

        public double Calculate(IReadOnlyDictionary<int, double> coords1, IReadOnlyDictionary<int, double> coords2)
        {
            var fewerCoords = coords1.Count < coords2.Count ? coords1 : coords2;
            var moreCoords = fewerCoords == coords1 ? coords2 : coords1;

            var overlap = 0.0;
            var distSquared = 0.0;
            foreach (var coord in fewerCoords)
            {
                var coordValue = coord.Value >= 1 ? coord.Value : 0;
                if (moreCoords.TryGetValue(coord.Key, out var otherCoordValue))
                {
                    overlap += Math.Min(coordValue, otherCoordValue);
                    var diff = coord.Value - (otherCoordValue >= 1 ? otherCoordValue : 0);
                    distSquared += diff * diff;
                }
                else if (coord.Value >= 1)
                {
                    distSquared += coordValue * coordValue;
                }
            }
            if (overlap <= 0.0)
            {
                return double.PositiveInfinity;
            }
            foreach (var otherCoord in moreCoords.Where(otherCoord => !fewerCoords.ContainsKey(otherCoord.Key)))
            {
                if (otherCoord.Value >= 1)
                {
                    distSquared += otherCoord.Value * otherCoord.Value;
                }
            }

            foreach (var index in _immediateFamilyIndexes)
            {
                if (fewerCoords.TryGetValue(index, out var coordValue) && coordValue >= 1
                    && moreCoords.TryGetValue(index, out var otherCoordValue) && otherCoordValue >= 1)
                {
                    overlap += Math.Min(coordValue, otherCoordValue) * 10;
                }
            }

            return distSquared / overlap;
        }

        public IEnumerable<int> SignificantCoordinates(IReadOnlyDictionary<int, double> coords) => coords.Keys;
    }
}
