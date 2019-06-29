using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models
{
    public class AncestryMatchesRetrieverOld
    {
        HttpClient _dnaHomeClient;
        const int _matchesPerPage = 50;

        public AncestryMatchesRetrieverOld(HttpClient dnaHomeClient)
        {
            _dnaHomeClient = dnaHomeClient;
        }

        public async Task<List<Match>> GetMatchesAsync(string guid, int numMatches, bool includeTreeInfo, Throttle throttle, ProgressData progressData)
        {
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    var startPage = 1;
                    var numPages = (numMatches + _matchesPerPage) / _matchesPerPage - startPage + 1;

                    progressData.Reset("Downloading matches...", numPages * 2);

                    var matchesTasks = Enumerable.Range(startPage, numPages)
                        .Select(pageNumber => GetMatchesPageAsync(guid, pageNumber, includeTreeInfo, throttle, progressData));
                    var matchesGroups = await Task.WhenAll(matchesTasks);
                    return matchesGroups.SelectMany(matchesGroup => matchesGroup).Take(numMatches).ToList();
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
            }
        }

        public async Task<MatchCounts> GetMatchCounts(string guid)
        {
            // Make sure there are no more than 10 concurrent HTTP requests, to avoid overwhelming the Ancestry web site.
            var throttle = new Throttle(10);

            var thirdCousinsTask = CountThirdCousinsAsync(guid, throttle, ProgressData.SuppressProgress);
            var fourthCousinsTask = CountFourthCousinsAsync(guid, throttle, ProgressData.SuppressProgress);
            var totalMatchesTask = CountTotalMatchesAsync(guid, _ => true, 1, 1000, false, throttle, ProgressData.SuppressProgress);
            await Task.WhenAll(thirdCousinsTask, fourthCousinsTask, totalMatchesTask);

            return new MatchCounts
            {
                ThirdCousins = await thirdCousinsTask,
                FourthCousins = await fourthCousinsTask,
                TotalMatches = await totalMatchesTask,
            };
        }

        private Task<int> CountThirdCousinsAsync(string guid, Throttle throttle, ProgressData progressData)
        {
            return CountMatches(guid, match => match.SharedCentimorgans >= 90, 1, 1, throttle, progressData);
        }

        private class MatchesCounts
        {
            public int HighConfidenceCount { get; set; }
            public int StarredCount { get; set; }
            public int HintCount { get; set; }
            public int MatchesCount { get; set; }
        }

        private async Task<int> CountFourthCousinsAsync(string guid, Throttle throttle, ProgressData progressData)
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
            return await CountMatches(guid, match => match.SharedCentimorgans >= 20, 1, 20, throttle, progressData);
        }

        private async Task<int> CountTotalMatchesAsync(string guid, Func<Match, bool> criteria, int minPage, int maxPage, bool includeTreeInfo, Throttle throttle, ProgressData progressData)
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
            return await CountMatches(guid, _ => true, 1, 1000, throttle, progressData);
        }

        private async Task<int> CountMatches(string guid, Func<Match, bool> criteria, int minPage, int maxPage, Throttle throttle, ProgressData progressData)
        {
            IEnumerable<Match> pageMatches = new Match[0];

            // Try to find some page that is at least as high as the highest valid match.
            do
            {
                pageMatches = await GetMatchesPageAsync(guid, maxPage, false, throttle, progressData);
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
                pageMatches = await GetMatchesPageAsync(guid, midPage, false, throttle, progressData);
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

        private async Task<IEnumerable<Match>> GetMatchesPageAsync(string guid, int pageNumber, bool includeTreeInfo, Throttle throttle, ProgressData progressData)
        {
            var nameUnavailableCount = 0;
            var nameUnavailableMax = 60;
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    await throttle.WaitAsync();
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

                        throttle.Release();
                        progressData.Increment();
                        if (includeTreeInfo)
                        {
                            try
                            {
                                await GetLinkedTreesAsync(guid, result, throttle);
                            }
                            catch
                            {
                                // non-fatal if unable to download trees
                            }
                        }

                        progressData.Increment();
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throttle.Release();
                        throw;
                    }
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
            }
        }

        private async Task<List<Match>> GetRawMatchesInCommonAsync(string guid, string guidInCommon, double minSharedCentimorgans, Throttle throttle)
        {
            var matches = new List<Match>();
            var maxPage = 10000;
            for (var pageNumber = 1; pageNumber < maxPage; ++pageNumber)
            {
                var originalCount = matches.Count;
                var (pageMatches, pageCount) = await GetMatchesInCommonPageAsync(guid, guidInCommon, pageNumber, throttle);
                matches.AddRange(pageMatches);

                // Exit if we read past the end of the list of matches (a page with no matches),
                // or if the last entry on the page is lower than the minimum.
                if (originalCount == matches.Count 
                    || matches.Last().SharedCentimorgans < minSharedCentimorgans
                    || (pageCount > 0 && pageNumber >= pageCount))
                {
                    break;
                }
            }
            return matches;
        }

        public async Task<Dictionary<string, string>> GetMatchesInCommonAsync(string guid, Match match, double minSharedCentimorgans, Throttle throttle, int index, ProgressData progressData)
        {
            // Start retrieving the tree info in the background.
            var treeTask = match.TreeType == TreeType.Undetermined ? GetPublicTreeAsync(guid, match, throttle, false) : Task.CompletedTask;

            // Retrieve the matches.
            var matches = await GetRawMatchesInCommonAsync(guid, match.TestGuid, minSharedCentimorgans, throttle);
            var result = matches.GroupBy(m => m.TestGuid).ToDictionary(g => g.Key, g => g.First().TestGuid);

            // Make sure that we have finished retrieving the tree info.
            await treeTask;

            progressData.Increment();
            return result;
        }

        private async Task<(IEnumerable<Match>, int)> GetMatchesInCommonPageAsync(string guid, string guidInCommon, int pageNumber, Throttle throttle)
        {
            if (guid == guidInCommon)
            {
                return (Enumerable.Empty<Match>(), 0);
            }

            var nameUnavailableCount = 0;
            var nameUnavailableMax = 60;
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                await throttle.WaitAsync();
                try
                {
                    using (var testsResponse = await _dnaHomeClient.GetAsync($"dna/secure/tests/{guid}/matchesInCommon?filterBy=ALL&sortBy=RELATIONSHIP&page={pageNumber}&matchTestGuid={guidInCommon}"))
                    {
                        if (testsResponse.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            return (Enumerable.Empty<Match>(), 0);
                        }
                        if (testsResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                        {
                            await Task.Delay(120000);
                            continue;
                        }
                        testsResponse.EnsureSuccessStatusCode();
                        var matches = await testsResponse.Content.ReadAsAsync<Matches>();

                        var matchesInCommon = matches.MatchGroups.SelectMany(matchGroup => matchGroup.Matches);

                        if (matchesInCommon.Any(match => match.Name == "name unavailable") && ++nameUnavailableCount < nameUnavailableMax)
                        {
                            await Task.Delay(3000);
                            continue;
                        }

                        return (matchesInCommon, matches.PageCount); 
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        throw;
                    }
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
                finally
                {
                    throttle.Release();
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

        private async Task GetPublicTreeAsync(string guid, Match match, Throttle throttle, bool doThrow)
        {
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    await throttle.WaitAsync();
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
                        else if (treeDetails.PublicTreeInformationList?.Any() == true && treeDetails.MatchTreeNodeCount > 0)
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
                        if (doThrow)
                        {
                            throw;
                        }
                        return;
                    }
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
                finally
                {
                    throttle.Release();
                }
            }
        }

        private async Task GetLinkedTreesAsync(string guid, IEnumerable<Match> matches, Throttle throttle)
        {
            if (!matches.Any())
            {
                return;
            }

            while (true)
            {
                try
                {
                    await throttle.WaitAsync();
                    var matchesDictionary = matches.ToDictionary(match => match.TestGuid);
                    var url = $"/discoveryui-matchesservice/api/samples/{guid}/matchesv2/additionalInfo?ids=[{"%22" + string.Join("%22,%22", matchesDictionary.Keys) + "%22"}]&tree=true";
                    using (var testsResponse = await _dnaHomeClient.GetAsync(url))
                    {
                        testsResponse.EnsureSuccessStatusCode();
                        var treeInfos = await testsResponse.Content.ReadAsAsync<List<TreeInfoV2>>();
                        foreach (var treeInfo in treeInfos)
                        {
                            if (matchesDictionary.TryGetValue(treeInfo.TestGuid, out var match))
                            {
                                match.TreeSize = treeInfo.TreeSize ?? 0;
                                match.TreeType = treeInfo.UnlinkedTree == true ? TreeType.Unlinked
                                    : treeInfo.PrivateTree == true ? TreeType.Private
                                    : treeInfo.PublicTree == true && match.TreeSize > 0 ? TreeType.Public 
                                    : treeInfo.NoTrees == true ? TreeType.None
                                    : TreeType.Undetermined;
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    throttle.Release();
                }

/*
                var retryCount = 0;
                var retryMax = 5;
                try
                {
                    await throttle.WaitAsync();
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
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
                finally
                {
                    throttle.Release();
                }
*/
            }
        }

        private class TreeInfo
        {
            public bool HasHint { get; set; }
            public bool HasUnlinkedTree { get; set; }
            public int PersonCount { get; set; }
            public bool PrivateTree { get; set; }
        }

        private class TreeInfoV2
        {
            public bool? NoTrees { get; set; }
            public bool? PrivateTree { get; set; }
            public bool? PublicTree { get; set; }
            public string TestGuid { get; set; }
            public string TreeId { get; set; }
            public int? TreeSize { get; set; }
            public bool? TreeUnavailable { get; set; }
            public bool? UnlinkedTree { get; set; }
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
