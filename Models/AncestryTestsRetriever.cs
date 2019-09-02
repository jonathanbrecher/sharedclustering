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
        private HttpClient _ancestryClient;

        public AncestryTestsRetriever(HttpClient ancestryClient)
        {
            _ancestryClient = ancestryClient;
        }

        // Download the list of tests available to this user account.
        // As in the web site the tests are sorted with the user's own test, followed by the other tests alphabetically.
        public async Task<Dictionary<string, string>> GetTestsAsync()
        {
            using (var testsResponse = await _ancestryClient.GetAsync("discoveryui-matchesservice/api/samples/"))
            {
                if (testsResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    MessageBox.Show("Invalid username or password" +
                        $"{Environment.NewLine}{Environment.NewLine}" +
                        "Your username or password is incorrect. Please try again.", "Invalid username or password", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
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
