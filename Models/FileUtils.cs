using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AncestryDnaClustering.Properties;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace AncestryDnaClustering.Models
{
    public static class FileUtils
    {
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
                    if (!MaybeRetry(ex, $"reading {fileName}"))
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
                    if (!doRetry || !MaybeRetry(ex, $"reading {fileName}"))
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

        public static void WriteAllLines(string fileName, string lines, bool append, bool doThrow)
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

        public static void WriteAllLines(string fileName, IEnumerable<string> lines, bool doThrow)
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

        public static bool AppendLines(string fileName, IEnumerable<string> lines, bool doThrow)
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
                    if (!MaybeRetry(ex, $"deleting {fileName}"))
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

        public static string AddSuffixToFilename(string fileName, string suffix)
        {
            return Path.Combine(
                Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + $"-{suffix}" + Path.GetExtension(fileName));
        }

        private static bool MaybeRetry(Exception ex, string action)
        {
            LogException(ex, false);
            return MessageBox.Show(
                $"An error occurred while {action}:{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Try again?",
                "File error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private static DateTimeOffset _lastExceptionTime = DateTimeOffset.MinValue;

        public static void LogException(Exception ex, bool showMessage)
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
                MessageBox.Show("Out of memory!" +
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    "Try closing other applications to free up more memory " +
                    "or increase the value of the lowest centimorgans to cluster", "Out of memory", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show($"An unexpected error has occurred: {ex?.Message}" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"A log file has been written to {logfile}", "Unexpected failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void Save(ExcelPackage p, string fileName)
        {
            while (true)
            {
                try
                {
                    p.SaveAs(new FileInfo(fileName));
                    break;
                }
                catch (Exception ex)
                {
                    LogException(ex, false);
                    var message = ex.Message;
                    if (ex.ToString().Contains("being used by another process"))
                    {
                        message = "A file in that location is already open in another application.";
                    }
                    if (MessageBox.Show(
                        $"An error occurred while saving {fileName}:{Environment.NewLine}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}Try again?",
                        "File error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        throw;
                    }
                }
            }
        }

        public static string GetDefaultDirectory(string defaultFileName)
        {
            if (!string.IsNullOrEmpty(defaultFileName) && Directory.Exists(Path.GetDirectoryName(defaultFileName)))
            {
                return Path.GetDirectoryName(defaultFileName);
            }

            if (!string.IsNullOrEmpty(Settings.Default.LastUsedDirectory) && Directory.Exists(Settings.Default.LastUsedDirectory))
            {
                return Settings.Default.LastUsedDirectory;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
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
                LogException(ex, true);
            }
        }
    }
}
