using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace AncestryDnaClustering.ViewModels
{
    // A very simple ViewModel that implements a comment to show documentation from an online source.
    public class IntroductionViewModel : ObservableObject
    {
        public string Header { get; } = "Introduction";

        public IntroductionViewModel()
        {
            ShowDocumentationCommand = new RelayCommand(ShowDocumentation);
        }

        public ICommand ShowDocumentationCommand { get; }

        private static void ShowDocumentation()
        {
            try
            {
                Process.Start("https://github.com/jonathanbrecher/sharedclustering/wiki");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to show online documentation:{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Try again?",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}
