using System.Collections.Generic;

namespace AncestryDnaClustering.Models.HierarchicalClustering.Distance
{
    /// <summary>
    /// A measure of the distance between two sets of coordinates.
    /// </summary>
    public interface IDistanceMetric
    {
        double Calculate(Dictionary<int, double> coords1, Dictionary<int, double> coords2);

        /// <summary>
        /// Some distance metrics may ignore some coordinates that it considers insignificant.
        /// </summary>
        IEnumerable<int> SignficantCoordinates(Dictionary<int, double> coords);
    }
}
