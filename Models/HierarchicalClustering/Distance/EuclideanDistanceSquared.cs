using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.Models.HierarchicalClustering.Distance
{
    /// <summary>
    /// Calculate the square of Euclidean distance (in two dimensions: x^2 + y^2).
    /// Returns infinity if all dimensions are distinct.
    /// 
    /// CURRENTLY UNUSED.
    /// </summary>
    public class EuclideanDistanceSquared : IDistanceMetric
    {
        public double Calculate(Dictionary<int, double> coords1, Dictionary<int, double> coords2)
        {
            var fewerCoords = coords1.Count() < coords2.Count() ? coords1 : coords2;
            var moreCoords = fewerCoords == coords1 ? coords2 : coords1;

            var hasIntersection = false;
            var distSquared = 0.0;
            foreach (var coord in fewerCoords)
            {
                if (moreCoords.TryGetValue(coord.Key, out var otherCoordValue))
                {
                    hasIntersection = true;
                    var diff = coord.Value - otherCoordValue;
                    distSquared += diff * diff;
                }
                else
                {
                    distSquared += coord.Value * coord.Value;
                }
            }
            if (!hasIntersection)
            {
                return double.PositiveInfinity;
            }
            foreach (var otherCoord in moreCoords.Where(otherCoord => !fewerCoords.ContainsKey(otherCoord.Key)))
            {
                var diff = otherCoord.Value;
                distSquared += diff * diff;
            }
            return distSquared;
        }

        public IEnumerable<int> SignficantCoordinates(Dictionary<int, double> coords) => coords.Keys;
    }
}
