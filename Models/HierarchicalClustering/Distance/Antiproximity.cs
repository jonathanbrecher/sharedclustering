using System.Collections.Generic;
using System.Linq;

namespace AncestryDnaClustering.Models.HierarchicalClustering.Distance
{
    /// <summary>
    /// Measure distance only considering dimensions that are non-zero for both sets of coordinates.
    /// This puts a high weight towards similar sets of dimensions, while incurring no penalty where dimensions differ.
    /// Valued is negated to convert from proximity to distance (greatest proximity is smallest distance).
    /// 
    /// CURRENTLY UNUSED.
    /// </summary>
    public class Antiproximity : IDistanceMetric
    {
        public double Calculate(Dictionary<int, double> coords1, Dictionary<int, double> coords2)
        {
            return coords1.Sum(coord => coords2.TryGetValue(coord.Key, out var otherCoord) ? -coord.Value * otherCoord : 0);
        }
    }
}
