using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AncestryDnaClustering.Models
{
    class AncestryTestsRetriever
    {
        HttpClient _ancestryClient;

        public AncestryTestsRetriever(HttpClient ancestryClient)
        {
            _ancestryClient = ancestryClient;
        }

        // Download the list of tests available to this user account.
        // As in the web site the tests are sorted with the user's own test, followed by the other tests alphabetically.
        public async Task<Dictionary<string, string>> GetTestsAsync()
        {
            return await Task.FromResult(new Dictionary<string, string>());
        }
    }
}
