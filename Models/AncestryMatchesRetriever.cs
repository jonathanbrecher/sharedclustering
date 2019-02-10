using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models
{
    public class AncestryMatchesRetriever
    {
        HttpClient _dnaHomeClient;
        const int _matchesPerPage = 50;

        public AncestryMatchesRetriever(HttpClient dnaHomeClient)
        {
            _dnaHomeClient = dnaHomeClient;
        }

        public async Task<List<Match>> GetMatchesAsync(string guid, int numMatches, bool includeTreeInfo, ProgressData progressData)
        {
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    // Make sure there are no more than 10 concurrent HTTP requests, to avoid overwhelming the Ancestry web site.
                    var semaphore = new SemaphoreSlim(10);

                    var startPage = 1;
                    int numPages = (numMatches + _matchesPerPage) / _matchesPerPage;

                    progressData.Reset("Downloading matches...", numPages);

                    var matchesTasks = Enumerable.Range(startPage, numPages)
                        .Select(pageNumber => GetMatchesPageAsync(guid, pageNumber, includeTreeInfo, semaphore, progressData));
                    var matchesGroups = await Task.WhenAll(matchesTasks);
                    return matchesGroups.SelectMany(matchesGroup => matchesGroup).Take(numMatches).ToList();
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(3000);
                }
            }
        }

        public async Task<MatchCounts> GetMatchCounts(string guid)
        {
            // Make sure there are no more than 10 concurrent HTTP requests, to avoid overwhelming the Ancestry web site.
            var semaphore = new SemaphoreSlim(10);

            var thirdCousinsTask = CountThirdCousinsAsync(guid, semaphore, ProgressData.SuppressProgress);
            var fourthCousinsTask = CountFourthCousinsAsync(guid, semaphore, ProgressData.SuppressProgress);
            var totalMatchesTask = CountTotalMatchesAsync(guid, _ => true, 1, 1000, false, semaphore, ProgressData.SuppressProgress);
            await Task.WhenAll(thirdCousinsTask, fourthCousinsTask, totalMatchesTask);

            return new MatchCounts
            {
                ThirdCousins = await thirdCousinsTask,
                FourthCousins = await fourthCousinsTask,
                TotalMatches = await totalMatchesTask,
            };
        }

        private Task<int> CountThirdCousinsAsync(string guid, SemaphoreSlim semaphore, ProgressData progressData)
        {
            return CountMatches(guid, match => match.SharedCentimorgans >= 90, 1, 1, semaphore, progressData);
        }

        private class MatchesCounts
        {
            public int HighConfidenceCount { get; set; }
            public int StarredCount { get; set; }
            public int HintCount { get; set; }
            public int MatchesCount { get; set; }
        }

        private async Task<int> CountFourthCousinsAsync(string guid, SemaphoreSlim semaphore, ProgressData progressData)
        {
            // Try to get the count of fourth cousin matches directly from Ancestry.
            try
            {
                using (var testsResponse = await _dnaHomeClient.GetAsync($"dna/secure/tests/{guid}/matchCounts"))
                {
                    testsResponse.EnsureSuccessStatusCode();
                    var matchesCounts = await testsResponse.Content.ReadAsAsync<MatchesCounts>();
                    if (matchesCounts.HighConfidenceCount > 0)
                    {
                        return matchesCounts.HighConfidenceCount;
                    }
                }
            }
            catch
            {
                // If any error occurs, fall through to count the matches manually. 
            }

            // Count the matches manually. 
            return await CountMatches(guid, match => match.SharedCentimorgans >= 20, 1, 20, semaphore, progressData);
        }

        private async Task<int> CountTotalMatchesAsync(string guid, Func<Match, bool> criteria, int minPage, int maxPage, bool includeTreeInfo, SemaphoreSlim semaphore, ProgressData progressData)
        {
            // Try to get the count of fourth cousin matches directly from Ancestry.
            try
            {
                using (var testsResponse = await _dnaHomeClient.GetAsync($"dna/secure/tests/{guid}/matchCounts?includeTotal=true"))
                {
                    testsResponse.EnsureSuccessStatusCode();
                    var matchesCounts = await testsResponse.Content.ReadAsAsync<MatchesCounts>();
                    if (matchesCounts.MatchesCount > 0)
                    {
                        return matchesCounts.MatchesCount;
                    }
                }
            }
            catch
            {
                // If any error occurs, fall through to count the matches manually. 
            }

            // Count the matches manually. 
            return await CountMatches(guid, _ => true, 1, 1000, semaphore, progressData);
        }

        private async Task<int> CountMatches(string guid, Func<Match, bool> criteria, int minPage, int maxPage, SemaphoreSlim semaphore, ProgressData progressData)
        {
            IEnumerable<Match> pageMatches = new Match[0];

            // Try to find some page that is at least as high as the highest valid match.
            do
            {
                pageMatches = await GetMatchesPageAsync(guid, maxPage, false, semaphore, progressData);
                if (pageMatches.Any(match => !criteria(match)) || !pageMatches.Any())
                {
                    break;
                }
                maxPage *= 2;
            } while (true);

            // Back down to find the the page that is exactly as high as the highest valid match
            var midPage = minPage;
            while (maxPage > minPage)
            {
                midPage = (maxPage + minPage) / 2;
                pageMatches = await GetMatchesPageAsync(guid, midPage, false, semaphore, progressData);
                if (pageMatches.Any(match => criteria(match)))
                {
                    if (pageMatches.Any(match => !criteria(match)))
                    {
                        break;
                    }
                    minPage = midPage + 1;
                }
                else
                {
                    maxPage = midPage;
                }
            }

            return (midPage - 1) * _matchesPerPage + pageMatches.Count(match => criteria(match));
        }

        private async Task<IEnumerable<Match>> GetMatchesPageAsync(string guid, int pageNumber, bool includeTreeInfo, SemaphoreSlim semaphore, ProgressData progressData)
        {
            var nameUnavailableCount = 0;
            var nameUnavailableMax = 60;
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    await semaphore.WaitAsync();
                    using (var testsResponse = await _dnaHomeClient.GetAsync($"dna/secure/tests/{guid}/matches?filterBy=ALL&sortBy=RELATIONSHIP&rows={_matchesPerPage}&page={pageNumber}"))
                    {
                        testsResponse.EnsureSuccessStatusCode();
                        var matches = await testsResponse.Content.ReadAsAsync<Matches>();
                        var result = matches.MatchGroups.SelectMany(matchGroup => matchGroup.Matches);

                        // Sometimes Ancestry returns matches with partial data.
                        // If that happens, retry and hope to get full data the next time.
                        if (result.Any(match => match.Name == "name unavailable") && ++nameUnavailableCount < nameUnavailableMax)
                        {
                            await Task.Delay(3000);
                            continue;
                        }

                        if (includeTreeInfo)
                        {
                            await GetLinkedTreesAsync(guid, result);
                        }

                        progressData.Increment();
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(3000);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        private async Task<List<Match>> GetRawMatchesInCommonAsync(string guid, string guidInCommon, double minSharedCentimorgans)
        {
            var matches = new List<Match>();
            var maxPage = 10000;
            for (var pageNumber = 1; pageNumber < maxPage; ++pageNumber)
            {
                var originalCount = matches.Count;
                var pageMatches = await GetMatchesInCommonPageAsync(guid, guidInCommon, pageNumber);
                matches.AddRange(pageMatches);

                // Exit if we read past the end of the list of matches (a page with no matches),
                // or if the last entry on the page is lower than the minimum.
                if (originalCount == matches.Count || matches.Last().SharedCentimorgans < minSharedCentimorgans)
                {
                    break;
                }
            }
            return matches;
        }

        public async Task<Dictionary<string, string>> GetMatchesInCommonAsync(string guid, Match match, double minSharedCentimorgans, System.Threading.SemaphoreSlim semaphore, int index, ProgressData progressData)
        {
            try
            {
                await semaphore.WaitAsync();

                // Start retrieving the tree info in the background.
                var treeTask = match.TreeType == TreeType.Undetermined ? GetPublicTreeAsync(guid, match) : Task.CompletedTask;

                // Retrieve the matches.
                var matches = await GetRawMatchesInCommonAsync(guid, match.TestGuid, minSharedCentimorgans);
                var result = matches.GroupBy(m => m.TestGuid).ToDictionary(g => g.Key, g => g.First().TestGuid);

                // Make sure that we have finished retrieving the tree info.
                await treeTask;

                progressData.Increment();
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<IEnumerable<Match>> GetMatchesInCommonPageAsync(string guid, string guidInCommon, int pageNumber)
        {
            if (guid == guidInCommon)
            {
                return Enumerable.Empty<Match>();
            }

            var nameUnavailableCount = 0;
            var nameUnavailableMax = 60;
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    using (var testsResponse = await _dnaHomeClient.GetAsync($"dna/secure/tests/{guid}/matchesInCommon?filterBy=ALL&sortBy=RELATIONSHIP&page={pageNumber}&matchTestGuid={guidInCommon}"))
                    {
                        if (testsResponse.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            return Enumerable.Empty<Match>();
                        }
                        testsResponse.EnsureSuccessStatusCode();
                        var matches = await testsResponse.Content.ReadAsAsync<Matches>();

                        var matchesInCommon = matches.MatchGroups.SelectMany(matchGroup => matchGroup.Matches);

                        if (matchesInCommon.Any(match => match.Name == "name unavailable") && ++nameUnavailableCount < nameUnavailableMax)
                        {
                            await Task.Delay(3000);
                            continue;
                        }

                        return matchesInCommon; 
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(3000);
                }
            }
        }

        private class Matches
        {
            public List<MatchGroup> MatchGroups { get; set; }
            public int PageCount { get; set; }
        }

        private class MatchGroup
        {
            public List<Match> Matches { get; set; }
        }

        private class TestSubject
        {
            public string DisplayName { get; set; }
            public string UcdmId { get; set; }
        }

        private async Task GetPublicTreeAsync(string guid, Match match)
        {
            var retryCount = 0;
            var retryMax = 60;
            while (true)
            {
                try
                {
                    using (var testsResponse = await _dnaHomeClient.GetAsync($"dna/secure/tests/{guid}/matches/{match.TestGuid}/treeDetails"))
                    {
                        testsResponse.EnsureSuccessStatusCode();
                        var treeDetails = await testsResponse.Content.ReadAsAsync<TreeDetails>();
                        if (treeDetails.MatchTestHasTree)
                        {
                            match.TreeType = TreeType.Public;
                            match.TreeSize = treeDetails.MatchTreeNodeCount;
                        }
                        else if (treeDetails.PublicTreeInformationList?.Any(info => info.IsPublic) == true)
                        {
                            match.TreeType = TreeType.Unlinked;
                        }
                        else if (treeDetails.PublicTreeInformationList?.Any() == true)
                        {
                            match.TreeType = TreeType.Private;
                        }
                        else
                        {
                            match.TreeType = TreeType.None;
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(3000);
                }
            }
        }

        private async Task GetLinkedTreesAsync(string guid, IEnumerable<Match> matches)
        {
            if (!matches.Any())
            {
                return;
            }

            var retryCount = 0;
            var retryMax = 60;
            while (true)
            {
                try
                {
                    var matchesDictionary = matches.ToDictionary(match => match.TestGuid);
                    using (var testsResponse = await _dnaHomeClient.PostAsJsonAsync($"discoveryui-matchesservice/api/samples/{guid}/matches/treeinfo", matchesDictionary.Keys))
                    {
                        testsResponse.EnsureSuccessStatusCode();
                        var treeDetails = await testsResponse.Content.ReadAsAsync<Dictionary<string, object>>();
                        foreach (var kvp in treeDetails)
                        {
                            if (kvp.Value is TreeInfo treeInfo && matchesDictionary.TryGetValue(kvp.Key, out var match))
                            {
                                match.HasHint = treeInfo.HasHint;
                                match.TreeSize = treeInfo.PersonCount;
                                match.TreeType = treeInfo.HasUnlinkedTree ? TreeType.Unlinked 
                                    : match.TreeSize == 0 ? TreeType.None
                                    : treeInfo.PrivateTree ? TreeType.Private : TreeType.Public;
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                }

                try
                {
                    var matchesDictionary = matches.ToDictionary(match => match.TestGuid);
                    using (var testsResponse = await _dnaHomeClient.PostAsJsonAsync($"dna/secure/tests/{guid}/treeAncestors", matchesDictionary.Keys))
                    {
                        testsResponse.EnsureSuccessStatusCode();
                        var treeDetails = await testsResponse.Content.ReadAsAsync<Dictionary<string, LinkedTreeDetails>>();
                        foreach (var kvp in treeDetails)
                        {
                            if (matchesDictionary.TryGetValue(kvp.Key, out var match))
                            {
                                match.TreeSize = kvp.Value.PersonCount;
                                if (match.TreeSize > 0)
                                {
                                    match.TreeType = kvp.Value.PrivateTree ? TreeType.Private : TreeType.Public;
                                }
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(3000);
                }
            }
        }

        private class TreeInfo
        {
            public bool HasHint { get; set; }
            public bool HasUnlinkedTree { get; set; }
            public int PersonCount { get; set; }
            public bool PrivateTree { get; set; }
        }

        private class LinkedTreeDetails
        {
            public bool PrivateTree { get; set; }
            public int PersonCount { get; set; }
        }

        private class TreeDetails
        {
            public bool MatchTestHasTree { get; set; }
            public int MatchTreeNodeCount { get; set; }
            public List<PublicTreeInformation> PublicTreeInformationList { get; set; }
        }

        private class PublicTreeInformation
        {
            public bool IsPublic { get; set; }
        }
    }
}
