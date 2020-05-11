using System;
using System.Collections.Generic;

namespace SharedClustering.Core
{
    public interface ICoreFileUtils
    {
        void WriteAllLines(string fileName, string lines, bool append, bool doThrow);
        void WriteAllLines(string fileName, IEnumerable<string> lines, bool doThrow);
        bool AppendLines(string fileName, IEnumerable<string> lines, bool doThrow);
        string AddSuffixToFilename(string fileName, string suffix);
        bool ShouldRetry(string message, string action);
        bool MaybeRetry(Exception ex, string action);
        bool AskYesNo(string message, string action);
        void LogException(Exception ex, bool showMessage);
    }
}
