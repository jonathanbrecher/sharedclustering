using System.Collections.Generic;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.SavedData
{
    public interface IMatchesLoader
    {
        string SelectFile(string fileName);
        string GetTrimmedFileName(string fileName);
        Task<(string, List<IClusterableMatch>, List<Tag>)> LoadClusterableMatchesAsync(string savedData, double minCentimorgansToCluster, double minCentimorgansInSharedMatches, ProgressData progressData);
    }
}
