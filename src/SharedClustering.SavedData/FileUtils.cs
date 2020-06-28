using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using SharedClustering.Core;

namespace AncestryDnaClustering.SavedData
{
    public static class FileUtils
    {
        private static bool ShouldRetryFunc(string message, string action)
        {
            return MessageBox.Show(
                $"An error occurred while {action}:{Environment.NewLine}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}Try again?",
                "File error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private static bool AskYesNo(string message, string title)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private static void ReportErrorFunc(string errorMessage, string title)
        {
            MessageBox.Show(errorMessage, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static ICoreFileUtils CoreFileUtils { get; } = new CoreFileUtils(ShouldRetryFunc, AskYesNo, ReportErrorFunc);

        public static void LogException(Exception ex, bool showMessage) => CoreFileUtils.LogException(ex, showMessage);
        public static string AddSuffixToFilename(string fileName, string suffix) => CoreFileUtils.AddSuffixToFilename(fileName, suffix);
        public static void WriteAllLines(string fileName, string lines, bool append, bool doThrow) => CoreFileUtils.WriteAllLines(fileName, lines, append, doThrow);
        public static void WriteAllLines(string fileName, IEnumerable<string> lines, bool doThrow) => CoreFileUtils.WriteAllLines(fileName, lines, doThrow);
        public static void OpenUrl(string url) => CoreFileUtils.OpenUrl(url);

        public static bool ShouldRetry(string message, string action)
        {
            return MessageBox.Show($"An error occurred while {action}:{Environment.NewLine}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}Try again?",
                "File error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes;
        }

        public static string ReadAllText(string fileName, bool doThrow)
        {
            while (true)
            {
                try
                {
                    return File.ReadAllText(fileName);
                }
                catch (Exception ex)
                {
                    if (!CoreFileUtils.MaybeRetry(ex, $"reading {fileName}"))
                    {
                        if (doThrow)
                        {
                            throw;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        public static T ReadAsJson<T>(string fileName, bool doRetry, bool doThrow)
        {
            while (true)
            {
                try
                {
                    using (var file = File.OpenText(fileName))
                    using (var reader = new JsonTextReader(file))
                    {
                        return _ignoreNullsSerializer.Deserialize<T>(reader);
                    }
                }
                catch (Exception ex)
                {
                    if (!doRetry || !CoreFileUtils.MaybeRetry(ex, $"reading {fileName}"))
                    {
                        if (doThrow)
                        {
                            throw;
                        }
                        else
                        {
                            return default(T);
                        }
                    }
                }
            }
        }

        private static JsonSerializer _ignoreNullsSerializer = JsonSerializer.Create(new JsonSerializerSettings
        { 
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        });

        public static void WriteAsJson<T>(string fileName, T data, bool doThrow)
        {
            while (true)
            {
                try
                {
                    using (var file = File.CreateText(fileName))
                    using (var writer = new JsonTextWriter(file))
                    {
                        _ignoreNullsSerializer.Serialize(writer, data);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    if (!CoreFileUtils.MaybeRetry(ex, $"saving {fileName}"))
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

        public static bool Delete(string fileName, bool doThrow)
        {
            while (true)
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    if (!CoreFileUtils.MaybeRetry(ex, $"deleting {fileName}"))
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

        public static void LaunchFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
            {
                MessageBox.Show(
                    $"The file {fileName} does not exist{Environment.NewLine}{Environment.NewLine}It may have been moved or deleted.",
                    "File error",
                    MessageBoxButton.OK);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(fileName);
            }
            catch (Exception ex)
            {
                CoreFileUtils.LogException(ex, true);
            }
        }
    }
}
