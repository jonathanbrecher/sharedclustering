using System.IO;
using System.Threading.Tasks;

namespace AncestryDnaClustering.Models.SavedData
{
    /// <summary>
    /// Read files saved natively by Shared Clustering.
    /// </summary>
    public class SharedClusteringMatchesReader : ISerializedMatchesReader
    {
        public bool IsSupportedFileType(string fileName) => fileName != null && Path.GetExtension(fileName).ToLower() == ".txt";

        public async Task<(Serialized, string)> ReadFileAsync(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return (null, $"{fileName} is not a *.txt file");
            }

            var serialized = await Task.Run(() => FileUtils.ReadAsJson<Serialized>(fileName, false, false));
            return (serialized, null);
        }
    }
}
