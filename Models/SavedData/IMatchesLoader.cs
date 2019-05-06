using System.Collections.Generic;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.SavedData
{
    public interface IMatchesLoader
    {
        (string fileName, string trimmedFileName) SelectFile(string fileName);
        Task<(string, List<IClusterableMatch>)> LoadClusterableMatchesAsync(string savedData, double minCentimorgansToCluster, double minCentimorgansInSharedMatches, ProgressData progressData);
    }
}
