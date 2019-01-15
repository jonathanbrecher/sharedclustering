using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AncestryDnaClustering.Models
{
    class AncestryLoginHelper
    {
        HttpClient _ancestryClient;
        CookieContainer _cookies;

        public AncestryLoginHelper(HttpClient ancestryClient, CookieContainer cookies)
        {
            _ancestryClient = ancestryClient;
            _cookies = cookies;
        }

        public async Task LoginAsync(string username, string password)
        {
            await Task.CompletedTask;
        }
    }
}
