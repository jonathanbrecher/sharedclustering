using System.Threading.Tasks;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models.SavedData
{
    public interface ISerializedMatchesReader
    {
        /// <summary>
        /// A quick validation of whether the file type is supported by this reader.
        /// </summary>
        bool IsSupportedFileType(string fileName);

        /// <summary>
        /// A trimmed version of the file name, suitable for renaming, or null if not a a supported file type.
        /// </summary>
        string GetTrimmedFileName(string fileName);

        /// <summary>
        /// Read the file, or return an error message
        /// </summary>
        Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName, ProgressData progressData);
    }
}
