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
            return new List<Match>();
        }

        public async Task<MatchCounts> GetMatchCounts(string guid)
        {
            return new MatchCounts();
        }

        public async Task<Dictionary<string, string>> GetMatchesInCommonAsync(string guid, Match match, double minSharedCentimorgans, System.Threading.SemaphoreSlim semaphore, int index, ProgressData progressData)
        {
            return await Task.FromResult(new Dictionary<string, string>());
        }
    }
}
