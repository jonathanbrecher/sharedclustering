using System;
using System.Collections.Generic;
using System.IO;

namespace SharedClustering.Core
{
    public class CoreFileUtils : ICoreFileUtils
    {
        private Func<string, string, bool> _shouldRetryFunc;
        public bool ShouldRetry(string message, string action) => _shouldRetryFunc(message, action);

        private Func<string, string, bool> _askYesNoFunc;
        public bool AskYesNo(string message, string action) => _askYesNoFunc(message, action);

        private Action<string, string> _reportErrorFunc;

        public CoreFileUtils(Func<string, string, bool> shouldRetryFunc, Func<string, string, bool> askYesNoFunc, Action<string, string> reportErrorFunc)
        {
            _shouldRetryFunc = shouldRetryFunc;
            _askYesNoFunc = askYesNoFunc;
            _reportErrorFunc = reportErrorFunc;
        }

        public void WriteAllLines(string fileName, string lines, bool append, bool doThrow)
        {
            while (true)
            {
                try
                {
                    string prefix = string.Empty;
                    try
                    {
                        if (append)
                        {
                            prefix = File.ReadAllText(fileName) + Environment.NewLine + "---" + Environment.NewLine;
                        }
                    }
                    catch (Exception) { }

                    File.WriteAllText(fileName, prefix + lines);
                    return;
                }
                catch (Exception ex)
                {
                    if (!MaybeRetry(ex, $"saving {fileName}"))
                    {
                        if (doThrow)
                        {
                            throw;
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
        }

        public void WriteAllLines(string fileName, IEnumerable<string> lines, bool doThrow)
        {
            while (true)
            {
                try
                {
                    File.WriteAllLines(fileName, lines);
                    return;
                }
                catch (Exception ex)
                {
                    if (!MaybeRetry(ex, $"saving {fileName}"))
                    {
                        if (doThrow)
                        {
                            throw;
                        }

                        return;
                    }
                }
            }
        }

        public bool AppendLines(string fileName, IEnumerable<string> lines, bool doThrow)
        {
            while (true)
            {
                try
                {
                    using (var file = new StreamWriter(fileName, true))
                    {
                        foreach (var line in lines)
                        {
                            file.WriteLine(line);
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    if (!MaybeRetry(ex, $"saving {fileName}"))
                    {
                        if (doThrow)
                        {
                            throw;
                        }

                        return false;
                    }
                }
            }
            }

            public string AddSuffixToFilename(string fileName, string suffix)
            {
                return Path.Combine(
                    Path.GetDirectoryName(fileName),
                    Path.GetFileNameWithoutExtension(fileName) + $"-{suffix}" + Path.GetExtension(fileName));
        }

        public bool MaybeRetry(Exception ex, string action)
        {
            LogException(ex, false);
            return _shouldRetryFunc(ex?.Message, action);
        }

        private static DateTimeOffset _lastExceptionTime = DateTimeOffset.MinValue;

        public void LogException(Exception ex, bool showMessage)
        {
            var logfile = Path.Combine(Path.GetTempPath(), "SharedClusteringLog.txt");
            var append = DateTimeOffset.Now < _lastExceptionTime + TimeSpan.FromMinutes(1);
            WriteAllLines(logfile, ex?.ToString(), append, false);
            _lastExceptionTime = DateTimeOffset.Now;

            if (append || !showMessage)
            {
                return;
            }

            if (ex is OutOfMemoryException)
            {
                _reportErrorFunc("Out of memory!" +
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    "Try closing other applications to free up more memory " +
                    "or increase the value of the lowest centimorgans to cluster", "Out of memory");
                return;
            }
            _reportErrorFunc($"An unexpected error has occurred: {ex?.Message}" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"A log file has been written to {logfile}", "Unexpected failure");
        }
    }
}
