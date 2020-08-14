using SharedClustering.Core;
using SharedClustering.Core.Anonymizers;
using SharedClustering.HierarchicalClustering;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedClustering.Models
{
    public interface IMatchesLoader
    {
        string SelectFile(string fileName);
        string GetTrimmedFileName(string fileName);
        Task<(string, List<IClusterableMatch>, List<Tag>)> LoadClusterableMatchesAsync(string savedData, double minCentimorgansToCluster, double minCentimorgansInSharedMatches, IAnonymizer anonymizer, IProgressData progressData);
    }
}
