using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace AncestryDnaClustering.Models
{
    internal class AncestryTestsRetriever
    {
        private AncestryLoginHelper _ancestryLoginHelper;

        public AncestryTestsRetriever(AncestryLoginHelper ancestryLoginHelper)
        {
            _ancestryLoginHelper = ancestryLoginHelper;
        }

        // Download the list of tests available to this user account.
        // As in the web site the tests are sorted with the user's own test, followed by the other tests alphabetically.
        public async Task<Dictionary<string, string>> GetTestsAsync()
        {
            using (var testsResponse = await _ancestryLoginHelper.AncestryClient.GetAsync("discoveryui-matchesservice/api/samples/"))
            {
                if (testsResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Your username or password is incorrect. Please try again.");
                }
                testsResponse.EnsureSuccessStatusCode();
                var tests = await testsResponse.Content.ReadAsAsync<SamplesSet>();
                return tests.Samples.Complete
                    .ToDictionary(test => test.DisplayName, test => test.TestGuid);
            }
        }

        private class SamplesSet
        {
            public CompleteSamples Samples { get; set; }
        }

        private class CompleteSamples
        {
            public List<CompleteSample> Complete { get; set; }
        }

        private class CompleteSample
        {
            public string TestGuid { get; set; }
            public string DisplayName { get; set; }
        }
    }
}
