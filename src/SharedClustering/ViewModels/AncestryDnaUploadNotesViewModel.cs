using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Properties;
using AncestryDnaClustering.SavedData;

namespace AncestryDnaClustering.ViewModels
{
    internal class AncestryDnaUploadNotesViewModel : ObservableObject
    {
        private readonly AncestryNotesUpdater _ancestryNotesUpdater;

        public AncestryDnaSignInViewModel SignInViewModel { get; }
        public string Header { get; } = "Upload Notes";

        public ProgressData ProgressData { get; } = new ProgressData();

        public AncestryDnaUploadNotesViewModel(
            AncestryDnaSignInViewModel signInViewModel,
            AncestryNotesUpdater ancestryNotesUpdater)
        {
            SignInViewModel = signInViewModel;
            _ancestryNotesUpdater = ancestryNotesUpdater;

            UploadNotesCommand = new RelayCommand(async () => await UploadNotesAsync());
        }

        public ICommand UploadNotesCommand { get; }

        private bool _canUploadNotes = true;
        public bool CanUploadNotes
        {
            get => _canUploadNotes;
            set => SetFieldValue(ref _canUploadNotes, value, nameof(CanUploadNotes));
        }

        private async Task UploadNotesAsync()
        {
            Settings.Default.Save();

            if (!SignInViewModel.IsSignedIn && !FileUtils.CoreFileUtils.AskYesNo("You are not signed in to your Ancestry account, so you will only be able to update local files. Continue anyway?", "Not signed in"))
            {
                return;
            }

            var fileName = _ancestryNotesUpdater.SelectFile("");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            try
            {
                var throttle = new Throttle(50);

                await _ancestryNotesUpdater.UpdateNotesAsync(SignInViewModel.SelectedTest.TestGuid, fileName, SignInViewModel.IsSignedIn, throttle, ProgressData);
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, true);
            }
            finally
            {
                ProgressData.Reset("Done");
            }
        }
    }
}
