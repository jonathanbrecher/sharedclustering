using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Properties;

namespace AncestryDnaClustering.ViewModels
{
    internal class AncestryDnaSignInViewModel : ObservableObject
    {
        private readonly AncestryLoginHelper _loginHelper;
        private readonly AncestryTestsRetriever _testsRetriever;

        public AncestryDnaSignInViewModel(AncestryLoginHelper loginHelper, AncestryTestsRetriever testsRetriever)
        {
            _loginHelper = loginHelper;
            _testsRetriever = testsRetriever;

            SignInCommand = new RelayCommand<PasswordBox>(async password => await SignInAsync(password));

            AncestryUserName = Settings.Default.AncestryUserName;
        }

        public ICommand SignInCommand { get; }

        // The user name for the account to use. This value is saved and will be restored when the application is relaunched.
        // For security, the password is not saved.
        private string _ancestryUserName;
        public string AncestryUserName
        {
            get => _ancestryUserName;
            set
            {
                if (SetFieldValue(ref _ancestryUserName, value, nameof(AncestryUserName)))
                {
                    Settings.Default.AncestryUserName = AncestryUserName;
                    CanSignIn = !string.IsNullOrWhiteSpace(AncestryUserName);
                }
            }
        }

        // A non-empty username is needed.
        private bool _canSignIn;
        public bool CanSignIn
        {
            get => _canSignIn;
            set => SetFieldValue(ref _canSignIn, value, nameof(CanSignIn));
        }

        // All of the tests (test ID and test taker name) available to the signed-in account.
        private Dictionary<string, string> _tests;
        public Dictionary<string, string> Tests
        {
            get => _tests;
            set
            {
                if (SetFieldValue(ref _tests, value, nameof(Tests)))
                {
                    if (Tests?.Count > 0)
                    {
                        // The selected test is the first one that matches the last-used value, otherwise the first one.
                        // The tests are ordered in the same order as in the Ancestry web site, with test taker's own test listed first.
                        SelectedTest = Tests?.Any(test => test.Key == Settings.Default.SelectedTestId) == true
                               ? Tests.FirstOrDefault(test => test.Key == Settings.Default.SelectedTestId)
                               : Tests.First();
                    }
                    else
                    {
                        SelectedTest = new KeyValuePair<string, string>();
                    }
                }
            }
        }

        public EventHandler OnSelectedTestChanged;

        // The test whose results will be downloaded.
        private KeyValuePair<string, string> _selectedTest;
        public KeyValuePair<string, string> SelectedTest
        {
            get => _selectedTest;
            set
            {
                if (SetFieldValue(ref _selectedTest, value, nameof(SelectedTest)))
                {
                    Settings.Default.SelectedTestId = SelectedTest.Key;
                    OnSelectedTestChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private async Task SignInAsync(PasswordBox password)
        {
            Settings.Default.Save();

            // Try primary site, and capture error if any.
            var primaryHost = _loginHelper.Hosts.First();
            var errorMessage = await SignInAsync(password, primaryHost);
            if (errorMessage == null)
            {
                return;
            }

            // If not able to sign into the main Ancestry site, try some backups.
            foreach (var alternateHost in _loginHelper.Hosts.Skip(1))
            {
                if (await SignInAsync(password, alternateHost) == null)
                {
                    return;
                }
            }

            // Show error message from primary login failure if none of the backups worked.
            MessageBox.Show(errorMessage, "Sign in failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task<string> SignInAsync(PasswordBox password, string hostOverride)
        {
            try
            {
                if (await _loginHelper.LoginAsync(AncestryUserName.Trim(), password.Password, hostOverride))
                {
                    Tests = await _testsRetriever.GetTestsAsync();
                    return null;
                }
                else
                {
                    return $"Unable to sign in to Ancestry";
                }
            }
            catch (Exception ex)
            {
                return $"Unable to sign in to Ancestry {Environment.NewLine}{Environment.NewLine}{ex.Message}";
            }
        }
    }
}
