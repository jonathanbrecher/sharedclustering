using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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
        public async Task<List<Test>> GetTestsAsync()
        {
            using (var testsResponse = await _ancestryLoginHelper.AncestryClient.GetAsync("dna/secure/tests"))
            {
                if (testsResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Your username or password is incorrect. Please try again.");
                }
                testsResponse.EnsureSuccessStatusCode();
                var tests = await testsResponse.Content.ReadAsAsync<Tests>();
                return tests.Data.CompleteTests.Select(test => new Test { DisplayName = test.TestSubject.DisplayName, TestGuid = test.Guid }).ToList();
            }
        }

        private class Tests
        {
            public TestData Data { get; set; }
        }

        private class TestData
        {
            public List<CompleteTest> CompleteTests { get; set; }
        }

        private class CompleteTest
        {
            public string Guid { get; set; }
            public TestSubject TestSubject { get; set; }
        }

        private class TestSubject
        {
            public string DisplayName { get; set; }
        }
    }
}
