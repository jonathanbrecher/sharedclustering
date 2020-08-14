using SharedClustering.SavedData;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace SharedClustering
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

            SetWebBrowserEmulationMode();
        }

        private static void App_Startup(object sender, StartupEventArgs e)
        {
            // Here if called from XAML, otherwise, this code can be in App() 
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void SetWebBrowserEmulationMode()
        {
            try
            {
                var minimumVersion = 11001;
                var keyName = $@"Software\{(Environment.Is64BitOperatingSystem ? "" : @"Wow6432Node\")}Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, true))
                {
                    var app = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                    key.SetValue(app, minimumVersion, Microsoft.Win32.RegistryValueKind.DWord);
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, false);
            }
        }

        // Example 2 
        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            FileUtils.LogException(e.Exception, true);
            e.Handled = true;
        }

        // Example 3 
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            FileUtils.LogException(exception, true);
        }

        // Example 4 
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            FileUtils.LogException(e.Exception, true);
            e.SetObserved();
        }
    }
}
