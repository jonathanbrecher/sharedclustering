using System.Collections.Generic;

namespace SharedClustering.HierarchicalClustering.Distance
{
    /// <summary>
    /// A measure of the distance between two sets of coordinates.
    /// </summary>
    public interface IDistanceMetric
    {
        double Calculate(IReadOnlyDictionary<int, double> coords1, IReadOnlyDictionary<int, double> coords2);

        /// <summary>
        /// Some distance metrics may ignore some coordinates that it considers insignificant.
        /// </summary>
        IEnumerable<int> SignificantCoordinates(IReadOnlyDictionary<int, double> coords);
    }
}
