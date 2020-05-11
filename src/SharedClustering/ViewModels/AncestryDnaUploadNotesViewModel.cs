using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Properties;
using SharedClustering.Core;

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

            SignInViewModel.OnSelectedTestChanged += SelectedTestChanged;

            UploadNotesCommand = new RelayCommand(async () => await UploadNotesAsync());
        }

        public ICommand UploadNotesCommand { get; }

        private void SelectedTestChanged(object sender, EventArgs e)
        {
            CheckCanUploadNotes();
        }

        private bool CheckCanUploadNotes() => CanUploadNotes = SignInViewModel.Tests?.Count > 0;

        private bool _canUploadNotes;
        public bool CanUploadNotes
        {
            get => _canUploadNotes;
            set => SetFieldValue(ref _canUploadNotes, value, nameof(CanUploadNotes));
        }

        private async Task UploadNotesAsync()
        {
            Settings.Default.Save();

            var fileName = _ancestryNotesUpdater.SelectFile("");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            try
            {
                var throttle = new Throttle(50);

                await _ancestryNotesUpdater.UpdateNotesAsync(SignInViewModel.SelectedTest.TestGuid, fileName, throttle, ProgressData);
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
