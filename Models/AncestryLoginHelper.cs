using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;

namespace AncestryDnaClustering.Models
{
    internal class AncestryLoginHelper
    {
        private HttpClient _ancestryClient;
        private CookieContainer _cookies;

        public AncestryLoginHelper(HttpClient ancestryClient, CookieContainer cookies)
        {
            _ancestryClient = ancestryClient;
            _cookies = cookies;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var queryString = new StringContent($"username={username}&password={password}");
            queryString.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            using (var loginResponse = await _ancestryClient.PostAsync("account/signin", queryString))
            {
                if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return LoginFailure();
                }
                loginResponse.EnsureSuccessStatusCode();
                try
                {
                    var result = await loginResponse.Content.ReadAsAsync<LoginResult>();
                    if (result.Status == "invalidCredentials")
                    {
                        return LoginFailure();
                    }
                }
                catch (Exception e)
                {
                    FileUtils.LogException(e, false);
                    return LoginFailure();
                }                
            }

            foreach (Cookie cookie in _cookies.GetCookies(new Uri("https://www.ancestry.com")))
            {
                _cookies.Add(new Uri("https://www.ancestry.com"), new Cookie(cookie.Name, cookie.Value, cookie.Path, "ancestry.com"));
            }
            return true;
        }

        private bool LoginFailure()
        {
            MessageBox.Show("Invalid username or password" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Your username or password is incorrect. Please try again.", "Invalid username or password", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        private class LoginResult
        {
            public string Status { get; set; }
            public string UserId { get; set; }
        }
    }
}
