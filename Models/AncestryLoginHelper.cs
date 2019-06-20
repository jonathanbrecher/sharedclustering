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
            foreach (var query in new[] { $"{{\"password\":\"{password}\",\"username\":\"{username}\"}}", $"username={username}&password={password}" })
            {
                var queryString = new StringContent(query);
                queryString.Headers.ContentType = new MediaTypeHeaderValue(query[0] == '{' ? "application/json" : "application/x-www-form-urlencoded");

                foreach (var url in new[] { "account/signin/frame/authenticate", "account/signin" })
                {
                    if (await LoginAsync(url, queryString))
                    {
                        foreach (Cookie cookie in _cookies.GetCookies(new Uri("https://www.ancestry.com")))
                        {
                            _cookies.Add(new Uri("https://www.ancestry.com"), new Cookie(cookie.Name, cookie.Value, cookie.Path, "ancestry.com"));
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<bool> LoginAsync(string requestUri, StringContent queryString)
        {
            using (var loginResponse = await _ancestryClient.PostAsync(requestUri, queryString))
            {
                if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return false;
                }
                loginResponse.EnsureSuccessStatusCode();
                try
                {
                    var result = await loginResponse.Content.ReadAsAsync<LoginResult>();
                    if (result.Status == "invalidCredentials")
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    FileUtils.LogException(e, false);
                    return false;
                }
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
