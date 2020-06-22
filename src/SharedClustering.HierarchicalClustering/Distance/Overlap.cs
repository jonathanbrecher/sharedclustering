using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering.Distance
{
    /// <summary>
    /// Measure distance only considering dimensions that are non-zero for both sets of coordinates.
    /// The sum of shared distance in each dimension is divided by the total scale of each set of dimensions.
    /// This puts a high weight towards similar sets of dimensions, while also incurring some penalty where dimensions differ.
    /// Valued is negated to convert from proximity to distance (greatest proximity is smallest distance).
    /// 
    /// CURRENTLY UNUSED.
    /// </summary>
    internal class Overlap : IDistanceMetric
    {
        public double Calculate(IReadOnlyDictionary<int, double> coords1, IReadOnlyDictionary<int, double> coords2)
        {
            var overlapCount = 0.0;
            foreach (var coord in coords1)
            {
                if (coords2.TryGetValue(coord.Key, out var otherCoordValue))
                {
                    overlapCount += Math.Min(coord.Value, otherCoordValue);
                }
            }
            return -overlapCount * overlapCount / coords1.Values.Sum() / coords2.Values.Sum();
        }

        public IEnumerable<int> SignificantCoordinates(IReadOnlyDictionary<int, double> coords) => coords.Keys;
    }
}
