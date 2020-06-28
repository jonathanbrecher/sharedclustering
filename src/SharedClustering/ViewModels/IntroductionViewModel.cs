using AncestryDnaClustering.SavedData;
using SharedClustering.Core;
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

        public ICommand ShowDocumentationCommand { get; } 
            = new RelayCommand(() => FileUtils.OpenUrl("https://github.com/jonathanbrecher/sharedclustering/wiki"));
    }
}
