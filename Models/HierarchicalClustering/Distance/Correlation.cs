using System;
using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.Models.HierarchicalClustering.Distance
{
    /// <summary>
    /// Calculate mathematical correlation. See https://en.wikipedia.org/wiki/Covariance_and_correlation
    /// 
    /// CURRENTLY UNUSED.
    /// </summary>
    public class Correlation : IDistanceMetric
    {
        public double Calculate(Dictionary<int, double> coords1, Dictionary<int, double> coords2)
        {
            var n = 1000;
            var sumX = 0.0;
            var sumX2 = 0.0;
            var sumY = 0.0;
            var sumY2 = 0.0;
            var sumXY = 0.0;

            foreach (var coord in coords1)
            {
                var x = coord.Value;
                coords2.TryGetValue(coord.Key, out var y);

                sumX += x;
                sumX2 += x * x;
                sumY += y;
                sumY2 += y * y;
                sumXY += x * y;
            }
            foreach (var otherCoord in coords2.Where(otherCoord => !coords1.ContainsKey(otherCoord.Key)))
            {
                var y = otherCoord.Value;

                sumY += y;
                sumY2 += y * y;
            }

            var stdX = Math.Sqrt(sumX2 / n - sumX * sumX / n / n);
            var stdY = Math.Sqrt(sumY2 / n - sumY * sumY / n / n);
            var covariance = (sumXY / n - sumX * sumY / n / n);

            return covariance / stdX / stdY;
        }

        public IEnumerable<int> SignficantCoordinates(Dictionary<int, double> coords) => coords.Keys;
    }
}
