using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AncestryDnaClustering.Models;

namespace AncestryDnaClustering
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public App()
        {
            Startup += App_Startup; // Can be called from XAML 

            DispatcherUnhandledException += App_DispatcherUnhandledException;

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private static void App_Startup(object sender, StartupEventArgs e)
        {
            // Here if called from XAML, otherwise, this code can be in App() 
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        // Example 2 
        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true;
        }

        // Example 3 
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogException(exception);
        }

        // Example 4 
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.SetObserved();
        }

        private static void LogException(Exception ex)
        {
            var logfile = Path.Combine(Path.GetTempPath(), "SharedClusteringLog.txt");
            FileUtils.WriteAllLines(logfile, ex?.ToString(), false);
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
    }
}
