using System.Collections.Generic;
using System.Linq;

namespace SharedClustering.HierarchicalClustering.Distance
{
    /// <summary>
    /// Measure distance as a variant of the square of Euclidean distance squared (in two dimensions: x^2 + y^2).
    /// Values are further partitioned into three strong/medium/weak segments,
    /// where the values in each segment are weighted by different amounts.
    /// 
    /// CURRENTLY UNUSED.
    /// </summary>
    internal class StrengthWeightedEuclideanDistanceSquared : IDistanceMetric
    {
        public double Calculate(Dictionary<int, double> coords1, Dictionary<int, double> coords2)
        {
            const double strongCutoff = 0.1;
            const double weakCutoff = 0.01;
            var distSquaredStrong = 0.0;
            var distSquaredMedium = 0.0;
            var distSquaredWeak = 0.0;
            foreach (var coord in coords1)
            {
                coords2.TryGetValue(coord.Key, out var otherCoordValue);
                var diff = coord.Value + (coord.Value > 0.01 ? 1 : 0) - (otherCoordValue + (otherCoordValue > 0.01 ? 1 : 0));
                var diffSquared = diff * diff;
                if (coord.Value > strongCutoff || otherCoordValue > strongCutoff)
                {
                    distSquaredStrong += diffSquared * 2;
                    if (coord.Value > strongCutoff && otherCoordValue > strongCutoff)
                    {
                        continue;
                    }
                }
                if (coord.Value > weakCutoff || otherCoordValue > weakCutoff)
                {
                    distSquaredMedium += diffSquared * 1;
                    if (coord.Value > weakCutoff && otherCoordValue > weakCutoff)
                    {
                        continue;
                    }
                }
                distSquaredWeak += diffSquared;
            }
            foreach (var otherCoord in coords2.Where(otherCoord => !coords1.ContainsKey(otherCoord.Key)))
            {
                var otherCoordValue = otherCoord.Value + (otherCoord.Value > 0.01 ? 1 : 0);
                var diffSquared = otherCoordValue * otherCoordValue;
                if (otherCoord.Value > strongCutoff)
                {
                    distSquaredStrong += diffSquared * 2;
                    if (otherCoord.Value > strongCutoff)
                    {
                        continue;
                    }
                }
                if (otherCoord.Value > weakCutoff)
                {
                    distSquaredMedium += diffSquared * 1;
                    continue;
                }
                distSquaredWeak += diffSquared;
            }
            return distSquaredStrong + distSquaredMedium + distSquaredWeak;
        }

        public IEnumerable<int> SignificantCoordinates(Dictionary<int, double> coords) => coords.Keys;
    }
}
