using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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

        public Task<bool> LoginAsync(string username, string password)
        {
            return Task.FromResult(false);
        }
    }
}
