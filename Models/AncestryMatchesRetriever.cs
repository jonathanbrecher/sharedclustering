using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.ViewModels;

namespace AncestryDnaClustering.Models
{
    public class AncestryMatchesRetriever
    {
        HttpClient _dnaHomeClient;
        public int MatchesPerPage { get; } = 200;

        public AncestryMatchesRetriever(HttpClient dnaHomeClient)
        {
            _dnaHomeClient = dnaHomeClient;
        }

        public async Task<List<Match>> GetMatchesAsync(string guid, int numMatches, bool includeAdditionalInfo, Throttle throttle, ProgressData progressData)
        {
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                try
                {
                    var startPage = 1;
                    var numPages = (numMatches + MatchesPerPage) / MatchesPerPage - startPage + 1;

                    progressData.Reset("Downloading matches...", numPages * 2);

                    var matchesTasks = Enumerable.Range(startPage, numPages)
                        .Select(pageNumber => GetMatchesPageAsync(guid, pageNumber, includeAdditionalInfo, throttle, progressData));
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
            var matchesTask = CountMatchesAsync(guid, throttle, ProgressData.SuppressProgress);
            await Task.WhenAll(thirdCousinsTask, matchesTask);

            return new MatchCounts
            {
                ThirdCousins = await thirdCousinsTask,
                FourthCousins = (await matchesTask).fourthCousins,
                TotalMatches = (await matchesTask).totalMatches,
            };
        }

        private Task<int> CountThirdCousinsAsync(string guid, Throttle throttle, ProgressData progressData)
        {
            return CountMatches(guid, match => match.SharedCentimorgans >= 90, 1, 1, throttle, progressData);
        }

        private class MatchesCounts
        {
            public int All { get; set; }
            public int Close { get; set; }
            public int Starred { get; set; }
        }

        private async Task<(int fourthCousins, int totalMatches)> CountMatchesAsync(string guid, Throttle throttle, ProgressData progressData)
        {
            try
            {
                using (var testsResponse = await _dnaHomeClient.GetAsync($"discoveryui-matchesservice/api/samples/{guid}/matchlist/counts"))
                {
                    testsResponse.EnsureSuccessStatusCode();
                    var matchesCounts = await testsResponse.Content.ReadAsAsync<MatchesCounts>();
                    return (matchesCounts.Close, matchesCounts.All);
                }
            }
            catch
            {
                // If any error occurs, fall through to count the matches manually. 
            }

            // Count the matches manually. 
            var fourthCousinsTask = CountMatches(guid, match => match.SharedCentimorgans >= 20, 1, 20, throttle, progressData);
            var totalMatchesTask = CountMatches(guid, _ => true, 1, 1000, throttle, progressData);
            await Task.WhenAll(fourthCousinsTask, totalMatchesTask);
            return (await fourthCousinsTask, await totalMatchesTask);
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

            return (midPage - 1) * MatchesPerPage + pageMatches.Count(match => criteria(match));
        }

        public async Task<IEnumerable<Match>> GetMatchesPageAsync(string guid, int pageNumber, bool includeAdditionalInfo, Throttle throttle, ProgressData progressData)
        {
            var nameUnavailableCount = 0;
            var nameUnavailableMax = 60;
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                await throttle.WaitAsync();
                var throttleReleased = false;

                try
                {
                    using (var testsResponse = await _dnaHomeClient.GetAsync($"discoveryui-matchesservice/api/samples/{guid}/matchesv2?page={pageNumber}&bookmarkdata={{\"moreMatchesAvailable\":true,\"lastMatchesServicePageIdx\":{pageNumber - 1}}}"))
                    {
                        throttle.Release();
                        throttleReleased = true;

                        testsResponse.EnsureSuccessStatusCode();
                        var matches = await testsResponse.Content.ReadAsAsync<MatchesV2>();
                        var result = matches.MatchGroups.SelectMany(matchGroup => matchGroup.Matches)
                            .Select(match => new Match
                            {
                                MatchTestAdminDisplayName = match.AdminDisplayName,
                                MatchTestDisplayName = match.DisplayName,
                                TestGuid = match.TestGuid,
                                SharedCentimorgans = match.Relationship?.SharedCentimorgans ?? 0,
                                SharedSegments = match.Relationship?.SharedSegments ?? 0,
                                Starred = match.Starred,
                                Note = match.Note,
                            })
                            .ToList();

                        // Sometimes Ancestry returns matches with partial data.
                        // If that happens, retry and hope to get full data the next time.
                        if (result.Any(match => match.Name == "name unavailable") && ++nameUnavailableCount < nameUnavailableMax)
                        {
                            await Task.Delay(3000);
                            continue;
                        }

                        progressData.Increment();
                        if (includeAdditionalInfo)
                        {
                            try
                            {
                                await GetAdditionalInfoAsync(guid, result, throttle);
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
                        FileUtils.LogException(ex, true);
                        await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                        return Enumerable.Empty<Match>();
                    }
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
                finally
                {
                    if (!throttleReleased)
                    {
                        throttle.Release();
                    }
                }
            }
        }

        public async Task<Match> GetMatchAsync(string guid, string testGuid, Throttle throttle, ProgressData progressData)
        {
            await throttle.WaitAsync();

            try
            {
                using (var testsResponse = await _dnaHomeClient.GetAsync($"discoveryui-matchesservice/api/samples/{guid}/matches/{testGuid}/details"))
                {
                    return testsResponse.IsSuccessStatusCode ? await testsResponse.Content.ReadAsAsync<Match>() : null;
                }
            }
            finally
            {
                progressData.Increment();
                throttle.Release();
            }
        }

        public async Task<List<Match>> GetRawMatchesInCommonAsync(string guid, string guidInCommon, int maxPage, double minSharedCentimorgans, Throttle throttle)
        {
            var matches = new List<Match>();
            for (var pageNumber = 1; pageNumber <= maxPage; ++pageNumber)
            {
                var originalCount = matches.Count;
                var (pageMatches, moreMatchesAvailable) = await GetMatchesInCommonPageAsync(guid, guidInCommon, pageNumber, throttle);
                matches.AddRange(pageMatches);

                // Exit if we read past the end of the list of matches (a page with no matches),
                // or if the last entry on the page is lower than the minimum.
                if (!moreMatchesAvailable
                    || originalCount == matches.Count 
                    || matches.Last().SharedCentimorgans < minSharedCentimorgans
                    || matches.Count < MatchesPerPage)
                {
                    break;
                }
            }
            return matches;
        }

        public async Task<List<int>> GetMatchesInCommonAsync(string guid, Match match, double minSharedCentimorgans, Throttle throttle, Dictionary<string, int> matchIndexes, ProgressData progressData)
        {
            var commonAncestorsTask = match.HasCommonAncestors ? GetCommonAncestorsAsync(guid, match.TestGuid, throttle) : Task.FromResult((List<string>)null);

            // Retrieve the matches.
            const int maxPage = 10000;
            var matches = await GetRawMatchesInCommonAsync(guid, match.TestGuid, maxPage, minSharedCentimorgans, throttle);
            var result = matches.GroupBy(m => m.TestGuid).ToDictionary(g => g.Key, g => g.First().TestGuid);

            match.CommonAncestors = await commonAncestorsTask;

            progressData.Increment();
            return result.Keys
                .Select(matchName =>
                    matchIndexes.TryGetValue(matchName, out var matchIndex) ? matchIndex : (int?)null).Where(i => i != null)
                        .Select(i => i.Value)
                        .Concat(new[] { matchIndexes[match.TestGuid] })
                        .ToList();
        }

        private async Task<(IEnumerable<Match>, bool)> GetMatchesInCommonPageAsync(string guid, string guidInCommon, int pageNumber, Throttle throttle)
        {
            if (guid == guidInCommon)
            {
                return (Enumerable.Empty<Match>(), false);
            }

            var nameUnavailableCount = 0;
            var nameUnavailableMax = 60;
            var retryCount = 0;
            var retryMax = 5;
            while (true)
            {
                await throttle.WaitAsync();
                var throttleReleased = false;

                try
                {
                    using (var testsResponse = await _dnaHomeClient.GetAsync($"discoveryui-matchesservice/api/samples/{guid}/matchesv2?page={pageNumber}&relationguid={guidInCommon}&bookmarkdata={{\"moreMatchesAvailable\":true,\"lastMatchesServicePageIdx\":{pageNumber - 1}}}"))
                    {
                        throttle.Release();
                        throttleReleased = true;

                        if (testsResponse.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            return (Enumerable.Empty<Match>(), false);
                        }
                        if (testsResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                        {
                            await Task.Delay(120000);
                            continue;
                        }
                        testsResponse.EnsureSuccessStatusCode();
                        var matches = await testsResponse.Content.ReadAsAsync<MatchesV2>();

                        var matchesInCommon = matches.MatchGroups.SelectMany(matchGroup => matchGroup.Matches)
                            .Select(match => new Match
                            {
                                MatchTestAdminDisplayName = match.AdminDisplayName,
                                MatchTestDisplayName = match.DisplayName,
                                TestGuid = match.TestGuid,
                                SharedCentimorgans = match.Relationship?.SharedCentimorgans ?? 0,
                                SharedSegments = match.Relationship?.SharedSegments ?? 0,
                                Starred = match.Starred,
                                Note = match.Note,
                            })
                            .ToList();

                        if (matchesInCommon.Any(match => match.Name == "name unavailable") && ++nameUnavailableCount < nameUnavailableMax)
                        {
                            await Task.Delay(3000);
                            continue;
                        }

                        return (matchesInCommon, matches.BookmarkData.MoreMatchesAvailable); 
                    }
                }
                catch (Exception ex)
                {
                    if (++retryCount >= retryMax)
                    {
                        FileUtils.LogException(ex, true);
                        await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                        return (Enumerable.Empty<Match>(), false);
                    }
                    await Task.Delay(ex is UnsupportedMediaTypeException ? 30000 : 3000);
                }
                finally
                {
                    if (!throttleReleased)
                    {
                        throttle.Release();
                    }
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

        private class MatchesV2
        {
            public List<MatchGroupV2> MatchGroups { get; set; }
            public BookmarkDataV2 BookmarkData { get; set; }
        }

        private class BookmarkDataV2
        {
            public int LastMatchesServicePageIdx { get; set; }
            public bool MoreMatchesAvailable { get; set; }
        }

        private class MatchGroupV2
        {
            public List<MatchV2> Matches { get; set; }
        }

        private class MatchV2
        {
            public string AdminDisplayName { get; set; }
            public string DisplayName { get; set; }
            public string TestGuid { get; set; }
            public Relationship Relationship { get; set; }
            public bool Starred { get; set; }
            public string Note { get; set; }
        }

        private class Relationship
        {
            public double SharedCentimorgans { get; set; }
            public int SharedSegments { get; set; }
        }

        private async Task GetAdditionalInfoAsync(string guid, IEnumerable<Match> matches, Throttle throttle)
        {
            while (true)
            {
                await throttle.WaitAsync();
                var throttleReleased = false;

                try
                {
                    var matchesDictionary = matches.ToDictionary(match => match.TestGuid);
                    var url = $"/discoveryui-matchesservice/api/samples/{guid}/matchesv2/additionalInfo?ids=[{"%22" + string.Join("%22,%22", matchesDictionary.Keys) + "%22"}]&ancestors=true&tree=true";
                    using (var testsResponse = await _dnaHomeClient.GetAsync(url))
                    {
                        throttle.Release();
                        throttleReleased = true;

                        testsResponse.EnsureSuccessStatusCode();
                        var undeterminedCount = 0;
                        var additionalInfos = await testsResponse.Content.ReadAsAsync<List<AdditionalInfo>>();
                        foreach (var additionalInfo in additionalInfos)
                        {
                            if (matchesDictionary.TryGetValue(additionalInfo.TestGuid, out var match))
                            {
                                match.TreeSize = additionalInfo.TreeSize ?? 0;
                                match.TreeType = 
                                      additionalInfo.PrivateTree == true ? TreeType.Private // might also be unlinked
                                    : additionalInfo.UnlinkedTree == true ? TreeType.Unlinked
                                    : additionalInfo.PublicTree == true && match.TreeSize > 0 ? TreeType.Public
                                    : additionalInfo.NoTrees == true ? TreeType.None
                                    : TreeType.Undetermined;
                                if (match.TreeType == TreeType.Undetermined)
                                {
                                    ++undeterminedCount;
                                }
                                match.HasCommonAncestors = additionalInfo.CommonAncestors ?? false;
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
                    if (!throttleReleased)
                    {
                        throttle.Release();
                    }
                }
            }
        }

        private class Ancestors
        {
            public List<AncestorCouple> AncestorCouples { get; set; }
        }

        private class AncestorCouple
        {
            public Ancestor Father { get; set; }
            public Ancestor Mother { get; set; }
        }

        private class Ancestor
        {
            public PersonData PersonData { get; set; }
        }

        private class PersonData
        {
            public string DisplayName { get; set; }
            public bool Potential { get; set; }
        }

        private async Task<List<string>> GetCommonAncestorsAsync(string guid, string testGuid, Throttle throttle)
        {
            while (true)
            {
                await throttle.WaitAsync();
                var throttleReleased = false;

                try
                {
                    var url = $"/discoveryui-matchesservice/api/compare/{guid}/with/{testGuid}/commonancestors/";
                    using (var testsResponse = await _dnaHomeClient.GetAsync(url))
                    {
                        throttle.Release();
                        throttleReleased = true;

                        testsResponse.EnsureSuccessStatusCode();
                        var ancestorCouples = await testsResponse.Content.ReadAsAsync<Ancestors>();
                        return ancestorCouples.AncestorCouples.SelectMany(couple => new[] { couple.Father, couple.Mother })
                            .Select(ancestor => ancestor?.PersonData?.DisplayName)
                            .Where(name => !string.IsNullOrEmpty(name))
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    if (!throttleReleased)
                    {
                        throttle.Release();
                    }
                }
            }
        }

        public async Task UpdateNotesAsync(string guid, string testGuid, string note, Throttle throttle)
        {
            await throttle.WaitAsync();

            try
            {
                var url = $"/discoveryui-matchesservice/api/samples/{guid}/matches/{testGuid}";
                using (var testsResponse = await _dnaHomeClient.PutAsJsonAsync(url, new { note }))
                {
                    testsResponse.EnsureSuccessStatusCode();
                }
            }
            finally
            {
                throttle.Release();
            }
        }

        private class TreeInfo
        {
            public bool HasHint { get; set; }
            public bool HasUnlinkedTree { get; set; }
            public int PersonCount { get; set; }
            public bool PrivateTree { get; set; }
        }

        private class AdditionalInfo
        {
            public bool? NoTrees { get; set; }
            public bool? PrivateTree { get; set; }
            public bool? PublicTree { get; set; }
            public string TestGuid { get; set; }
            public string TreeId { get; set; }
            public int? TreeSize { get; set; }
            public bool? TreeUnavailable { get; set; }
            public bool? UnlinkedTree { get; set; }
            public bool? CommonAncestors { get; set; }
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
