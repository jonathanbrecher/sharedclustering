using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace AncestryDnaClustering.Models.SavedData
{
    /// <summary>
    /// Read files saved by DNAGedcom.
    /// </summary>
    public class DnaGedcomMatchesReader : ISerializedMatchesReader
    {
        public bool IsSupportedFileType(string fileName) => fileName != null && Path.GetExtension(fileName).ToLower() == ".csv";

        public async Task<(Serialized, string)> ReadFileAsync(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return (null, $"{fileName} is not a *.csv file");
            }

            // DNAGedcom saves two files: a match file starting with m_,
            // and an in-common-with file starting with icw_
            var path = Path.GetDirectoryName(fileName);
            var matchFile = Path.GetFileName(fileName);
            var icwFile = matchFile;
            if (matchFile.Substring(0, 2).ToLower() == "m_")
            {
                icwFile = Path.Combine(path, "icw_" + matchFile.Substring(2));
                matchFile = Path.Combine(path, matchFile);
            }
            else if (matchFile.Substring(0, 4).ToLower() == "icw_")
            {
                matchFile = Path.Combine(path, "m_" + matchFile.Substring(4));
                icwFile = Path.Combine(path, icwFile);
            }
            else
            {
                return (null, "File name does not start with m_ or icw_");
            }

            if (!File.Exists(matchFile) || !File.Exists(icwFile))
            {
                return (null, $"Could not find both {matchFile} and {icwFile}");
            }

            try
            {
                var serialized = await Task.Run(() => ReadFile(matchFile, icwFile));
                return (serialized, null);
            }
            catch (Exception ex)
            {
                return (null, $"Unexpected error while reading DNAGedcom files: {ex.Message}");
            }
        }

        private Serialized ReadFile(string matchFile, string icwFile)
        {
            var serialized = new Serialized();

            using (var matchReader = new StreamReader(matchFile))
            using (var csv = new CsvReader(matchReader))
            {
                csv.Configuration.HeaderValidated = null;
                csv.Configuration.MissingFieldFound = null;
                csv.Configuration.RegisterClassMap<DnaGedcomMatchMap>();
                var dnaGedcomMatches = csv.GetRecords<DnaGedcomMatch>();

                // In case DNAGedcom file has data from more than one test, find the test ID with the largest number of matches.
                var matches = dnaGedcomMatches
                    .GroupBy(match => match.TestId ?? "")
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                if (matches == null)
                {
                    return serialized;
                }

                // This is the Test GUID for the person taking the test
                serialized.TestTakerTestGuid = matches.Key;

                // Translate match properties from DNAGedcom naming to Shared Clustering naming.
                serialized.Matches = matches
                    .AsParallel()
                    .Select(match => new Match
                    {
                        MatchTestAdminDisplayName = match.Admin,
                        MatchTestDisplayName = match.Name,
                        TestGuid = match.MatchId,
                        SharedCentimorgans = double.TryParse(match.SharedCm, out var sharedCmDouble) ? sharedCmDouble : 0.0,
                        SharedSegments = int.TryParse(match.SharedSegments, out var sharedSegmentsInt) ? sharedSegmentsInt : 0,

                        // DNAGedcom does not include information about unlinked trees
                        TreeType = int.TryParse(match.People, out var peopleInt) && peopleInt > 0 ? TreeType.Public : TreeType.Undetermined,

                        TreeSize = peopleInt,
                        Note = match.Note,
                    })
                    // Do not assume that the DNAGedcom data is free of duplicates.
                    .GroupBy(match => match.TestGuid)
                    .Select(g => g.First())
                    // Do not assume that the DNAGedcom data is already ordered by descending Shared Centimorgans.
                    .OrderByDescending(match => match.SharedCentimorgans)
                    .ToList();
            }

            // Assign zero-based indexes to the matches sorted by shared centimorgans descending.
            serialized.MatchIndexes = serialized.Matches
                .Select(match => match.TestGuid)
                .Distinct()
                .Select((id, index) => new { Id = id, Index = index })
                .ToDictionary(pair => pair.Id, pair => pair.Index);

            using (var icwReader = new StreamReader(icwFile))
            using (var csv = new CsvReader(icwReader))
            {
                csv.Configuration.HeaderValidated = null;
                csv.Configuration.MissingFieldFound = null;
                csv.Configuration.RegisterClassMap<DnaGedcomIcwMap>();

                // Translate the ICW data.
                // Shared Clustering assumes that every match also matches themselves.
                // DNAGedcom does not include the self-matches in the saved ICW data,
                // so the self-matches need to be added during the translation.
                serialized.Icw = csv.GetRecords<DnaGedcomIcw>().GroupBy(icw => icw.MatchId, icw => icw.IcwId)
                    .ToDictionary
                    (
                        g => g.Key, 
                        g => g.Concat(new[] { g.Key })
                            .Select(id => serialized.MatchIndexes.TryGetValue(id, out var index) ? index : -1).Where(i => i >= 0)
                            .OrderBy(i => i)
                            .ToList()
                    );
            }

            // Also add self-matches to every match that has no shared matches at all.
            foreach (var guidAndIndex in serialized.MatchIndexes)
            {
                if (!serialized.Icw.ContainsKey(guidAndIndex.Key))
                {
                    serialized.Icw[guidAndIndex.Key] = new List<int> { guidAndIndex.Value };
                }
            }

            return serialized;
        }

        // Load all fields as strings and parse manually, to protect against parse failures
        private class DnaGedcomMatch
        {
            public string TestId { get; set; }
            public string MatchId { get; set; }
            public string Name { get; set; }
            public string Admin { get; set; }
            public string People { get; set; }
            //public string Range { get; set; }
            //public double Confidence { get; set; }
            public string SharedCm { get; set; }
            public string SharedSegments { get; set; }
            //lastlogin { get; set; }
            //starred { get; set; }
            //viewed { get; set; }
            //private { get; set; }
            //hint { get; set; }
            //archived { get; set; }
            public string Note { get; set; }
            //imageurl { get; set; }
            //profileurl { get; set; }
            //treeurl { get; set; }
            //scanned { get; set; }
            //membersince { get; set; }
            //ethnicregions { get; set; }
            //ethnictraceregions { get; set; }
            //public string MatchUrl { get; set; }
        }

        private sealed class DnaGedcomMatchMap : ClassMap<DnaGedcomMatch>
        {
            public DnaGedcomMatchMap()
            {
                Map(m => m.TestId).Name("testid");
                Map(m => m.MatchId).Name("matchid");
                Map(m => m.Name).Name("name");
                Map(m => m.Admin).Name("admin");
                Map(m => m.People).Name("people");
                Map(m => m.SharedCm).Name("sharedCM");
                Map(m => m.SharedSegments).Name("sharedSegments");
                Map(m => m.Note).Name("note");
            }
        }

        private class DnaGedcomIcw
        {
            public string MatchId { get; set; }
            public string MatchName { get; set; }
            public string MatchAdmin { get; set; }
            public string IcwId { get; set; }
            public string IcwName { get; set; }
            public string IcwAdmin { get; set; }
        }

        private sealed class DnaGedcomIcwMap : ClassMap<DnaGedcomIcw>
        {
            public DnaGedcomIcwMap()
            {
                Map(m => m.MatchId).Name("matchid");
                Map(m => m.MatchName).Name("matchname");
                Map(m => m.MatchAdmin).Name("matchadmin");
                Map(m => m.IcwId).Name("icwid");
                Map(m => m.IcwName).Name("icwname");
                Map(m => m.IcwAdmin).Name("icwadmin");
            }
        }
    }
}
