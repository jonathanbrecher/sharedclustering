using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Properties;
using AncestryDnaClustering.SavedData;
using SharedClustering.Core;

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
        private List<Test> _tests;
        public List<Test> Tests
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
                        SelectedTest = Tests?.Any(test => test.DisplayName == Settings.Default.SelectedTestId) == true
                               ? Tests.FirstOrDefault(test => test.DisplayName == Settings.Default.SelectedTestId)
                               : Tests.First();
                    }
                    else
                    {
                        SelectedTest = new Test();
                    }
                }
            }
        }

        public EventHandler OnSelectedTestChanged;

        // The test whose results will be downloaded.
        private Test _selectedTest;
        public Test SelectedTest
        {
            get => _selectedTest;
            set
            {
                if (SetFieldValue(ref _selectedTest, value, nameof(SelectedTest)))
                {
                    Settings.Default.SelectedTestId = SelectedTest?.DisplayName;
                    IsSignedIn = SelectedTest != null;
                    OnSelectedTestChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _isSignedIn;
        public bool IsSignedIn
        {
            get => _isSignedIn;
            set => SetFieldValue(ref _isSignedIn, value, nameof(IsSignedIn));
        }

        private async Task SignInAsync(PasswordBox password)
        {
            Settings.Default.Save();

            var forceLoginViaWebBrowser = true;
            if (!forceLoginViaWebBrowser)
            {
                // Try primary site. If not able to sign into the main Ancestry site, try some backups.
                foreach (var host in _loginHelper.Hosts)
                {
                    var result = await SignInAsync(password, host);
                    if (result == LoginResult.Success)
                    {
                        return;
                    }
                    if (result == LoginResult.MultifactorAuthentication || result == LoginResult.InvalidCredentials)
                    {
                        break;
                    }
                }
            }

            if (await _loginHelper.LoginViaWebBrowserAsync())
            {
                await RetrieveTestsAsync();
                return;
            }

            // Show error message from primary login failure if none of the backups worked.
            MessageBox.Show("Unable to sign in to Ancestry", "Sign in failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task<LoginResult> SignInAsync(PasswordBox password, string hostOverride)
        {
            try
            {
                var result = await _loginHelper.LoginAsync(AncestryUserName.Trim(), password.Password, hostOverride);
                if (result == LoginResult.Success)
                {
                    result = await RetrieveTestsAsync();
                }
                return result;
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, false);
                return LoginResult.Exception;
            }
        }

        private async Task<LoginResult> RetrieveTestsAsync()
        {
            try
            {
                Tests = await _testsRetriever.GetTestsAsync();
                return LoginResult.Success;
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, false);
                return LoginResult.Exception;
            }
        }
    }
}
