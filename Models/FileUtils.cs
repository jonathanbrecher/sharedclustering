using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Newtonsoft.Json;

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
                    var json = ReadAllText(fileName, true);
                    return JsonConvert.DeserializeObject<T>(json);
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

        public static void WriteAllLines(string fileName, string lines, bool doThrow)
        {
            while (true)
            {
                try
                {
                    File.WriteAllText(fileName, lines);
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
                        else
                        {
                            return;
                        }
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
                        else
                        {
                            return false;
                        }
                    }
                }
            }
        }

        public static void WriteAsJson<T>(string fileName, T data, bool doThrow)
        {
            while (true)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(data);
                    WriteAllLines(fileName, json, true);
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
                        else
                        {
                            return false;
                        }
                    }
                }
            }
        }

        private static bool MaybeRetry(Exception ex, string action)
        {
            return MessageBox.Show(
                $"An error occurred while {action}:{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Try again?",
                "File error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
    }
}
