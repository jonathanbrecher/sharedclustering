using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;

namespace AncestryDnaClustering.Models
{
    internal class AncestryLoginHelper
    {
        private Dictionary<string, HttpClient> _ancestryClients;
        private CookieContainer _cookies;

        public AncestryLoginHelper(Dictionary<string, HttpClient> ancestryClients, CookieContainer cookies)
        {
            _ancestryClients = ancestryClients;
            _cookies = cookies;
        }

        public IEnumerable<string> Hosts => _ancestryClients.Keys;
        public HttpClient AncestryClient { get; private set; }

        public async Task<LoginResult> LoginAsync(string username, string password, string host)
        {
            if (!_ancestryClients.TryGetValue(host, out var ancestryClient))
            {
                return LoginResult.InternalError;
            }

            LoginResult? primaryStatus = null;
            foreach (var expect100Continue in new[] { false/*, true*/ })
            {
                // The default value of True "should" be right
                ServicePointManager.Expect100Continue = expect100Continue;

                foreach (var query in new[] { $"{{\"password\":\"{password}\",\"username\":\"{username}\"}}"/*, $"username={username}&password={password}"*/ })
                {
                    foreach (var url in new[] { "account/signin/frame/authenticate"/*, "account/signin"*/ })
                    {
                        var queryString = new StringContent(query);
                        queryString.Headers.ContentType = new MediaTypeHeaderValue(query[0] == '{' ? "application/json" : "application/x-www-form-urlencoded");

                        var status = await LoginAsync(url, queryString, ancestryClient);
                        if (primaryStatus == null)
                        {
                            primaryStatus = status;
                        }

                        if (status == LoginResult.Success)
                        {
                            try
                            {
                                var uri = new Uri($"https://{host}");
                                var domain = uri.Authority.Replace("www.", "");
                                foreach (Cookie cookie in _cookies.GetCookies(uri))
                                {
                                    _cookies.Add(uri, new Cookie(cookie.Name, cookie.Value, cookie.Path, $".{domain}"));
                                }
                            }
                            catch (Exception)
                            {
                                // Not fatal if we can't copy the cookies
                            }

                            AncestryClient = ancestryClient;
                            return status;
                        }
                        else if (status == LoginResult.MultifactorAuthentication || status == LoginResult.InvalidCredentials)
                        {
                            return status;
                        }
                    }
                }
            }

            return primaryStatus ?? LoginResult.InternalError;
        }

        private async Task<LoginResult> LoginAsync(string requestUri, StringContent queryString, HttpClient ancestryClient)
        {
            using (var loginResponse = await ancestryClient.PostAsync(requestUri, queryString))
            {
                if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return LoginResult.Unauthorized;
                }
                try
                {
                    loginResponse.EnsureSuccessStatusCode();
                    var result = await loginResponse.Content.ReadAsAsync<LoginResultDto>();
                    if (result.Status.Equals("invalidCredentials", StringComparison.OrdinalIgnoreCase))
                    {
                        return LoginResult.InvalidCredentials;
                    }
                    else if (result.Status.Equals("mfa", StringComparison.OrdinalIgnoreCase))
                    {
                        return LoginResult.MultifactorAuthentication;
                    }
                }
                catch (Exception e)
                {
                    FileUtils.LogException(e, false);
                    return LoginResult.Exception;
                }
            }

            return LoginResult.Success;
        }

        private class LoginResultDto
        {
            public string Status { get; set; }
            public string UserId { get; set; }
        }
    }
}
