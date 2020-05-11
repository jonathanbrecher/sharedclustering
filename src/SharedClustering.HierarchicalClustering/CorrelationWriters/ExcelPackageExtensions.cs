using System;
using System.IO;
using OfficeOpenXml;

namespace SharedClustering.HierarchicalClustering
{
    public static class ExcelPackageExtensions
    {
        public static void SaveWithRetry(this ExcelPackage p, string fileName, Func<string, string, bool> shouldRetry, Action<Exception, bool> logException)
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
                    logException(ex, false);
                    var message = ex.Message;
                    if (ex.ToString().Contains("being used by another process"))
                    {
                        message = "A file in that location is already open in another application.";
                    }
                    if (!shouldRetry($"saving {fileName}", message))
                    {
                        throw;
                    }
                }
            }
        }
    }
}
