using System.Threading.Tasks;

namespace AncestryDnaClustering.Models.SavedData
{
    public interface ISerializedMatchesReader
    {
        /// <summary>
        /// A quick validation of whether the file type is supported by this reader.
        /// </summary>
        bool IsSupportedFileType(string fileName);

        /// <summary>
        /// Read the file, or return an error message
        /// </summary>
        Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName);
    }
}
