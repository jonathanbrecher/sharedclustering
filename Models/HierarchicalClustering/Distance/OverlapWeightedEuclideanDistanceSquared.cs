using System;
using System.Collections.Generic;
using System.Linq;
using AncestryDnaClustering.Models.HierarchicalCustering;

namespace AncestryDnaClustering.Models.HierarchicalClustering.Distance
{
    /// <summary>
    /// Measure distance as a variant of the square of Euclidean distance squared (in two dimensions: x^2 + y^2).
    /// Values less than 1 in any dimension are treated as zero.
    /// The sum of shared distance in each dimension is divided by the total scale of each set of dimensions.
    /// This puts a high weight towards similar sets of dimensions, while also incurring some penalty where dimensions differ.
    /// </summary>
    public class OverlapWeightedEuclideanDistanceSquared : IDistanceMetric
    {
        public double Calculate(Dictionary<int, double> coords1, Dictionary<int, double> coords2)
        {
            var fewerCoords = coords1.Count() < coords2.Count() ? coords1 : coords2;
            var moreCoords = fewerCoords == coords1 ? coords2 : coords1;

            var overlap = 0.0;
            var distSquared = 0.0;
            foreach (var coord in fewerCoords)
            {
                var coordValue = (coord.Value >= 1 ? coord.Value : 0);
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
            if (overlap == 0.0)
            {
                return double.PositiveInfinity;
            }
            foreach (var otherCoord in moreCoords)
            {
                if (otherCoord.Value >= 1 && !fewerCoords.ContainsKey(otherCoord.Key))
                {
                    distSquared += otherCoord.Value * otherCoord.Value;
                }
            }

            return distSquared / overlap;
        }

        public IEnumerable<int> SignficantCoordinates(Dictionary<int, double> coords)
            => coords.Where(kvp => kvp.Value >= 1).Select(kvp => kvp.Key);
    }
}
