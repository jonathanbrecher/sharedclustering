using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AncestryDnaClustering.ViewModels;
using CsvHelper;
using CsvHelper.Configuration;

namespace AncestryDnaClustering.Models.SavedData
{
    /// <summary>
    /// Read files saved by DNAGedcom.
    /// </summary>
    public class DnaGedcomAncestryMatchesReader : ISerializedMatchesReader
    {
        public bool IsSupportedFileType(string fileName) => fileName != null && Path.GetExtension(fileName).ToLower() == ".csv";

        public string GetTrimmedFileName(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return null;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            foreach (var prefix in new[] { "m_", "icw_" })
            {
                if (fileNameWithoutExtension.StartsWith(prefix))
                {
                    return fileNameWithoutExtension.Substring(prefix.Length);
                }
            }

            return null;
        }

        public async Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName, ProgressData progressData)
        {
            if (!IsSupportedFileType(fileName))
            {
                return (null, $"{fileName} is not a *.csv file");
            }

            // DNAGedcom saves two files: a match file starting with m_,
            // and an in-common-with file starting with icw_
            var trimmedFileName = GetTrimmedFileName(fileName);
            if (trimmedFileName == null)
            {
                return (null, "File name does not start with m_ or icw_");
            }

            var path = Path.GetDirectoryName(fileName);
            var matchFile = Path.Combine(path, $"m_{trimmedFileName}.csv");
            var icwFile = Path.Combine(path, $"icw_{trimmedFileName}.csv");

            if (!File.Exists(matchFile) || !File.Exists(icwFile))
            {
                return (null, $"Could not find both {matchFile} and {icwFile}");
            }

            var serialized = new Serialized();

            try
            {
                await Task.Run(() => ReadMatchFile(serialized, matchFile));
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, false);
                return (null, $"Unexpected error while reading DNAGedcom match file: {ex.Message}");
            }

            try
            {
                await Task.Run(() => ReadIcwFile(serialized, icwFile));
            }
            catch (Exception ex)
            {
                FileUtils.LogException(ex, false);
                return (null, $"Unexpected error while reading DNAGedcom icw file: {ex.Message}");
            }

            return (serialized, null);
        }

        private static void ReadMatchFile(Serialized serialized, string matchFile)
        {
            using (var fileStream = new FileStream(matchFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var matchReader = new StreamReader(fileStream))
            using (var csv = new CsvReader(matchReader))
            {
                csv.Configuration.Delimiter = ",";
                csv.Configuration.HeaderValidated = null;
                csv.Configuration.MissingFieldFound = null;
                csv.Configuration.BadDataFound = null;
                csv.Configuration.LineBreakInQuotedFieldIsBadData = false;
                csv.Configuration.RegisterClassMap<DnaGedcomMatchMap>();
                csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.Replace('_', ' ');
                var dnaGedcomMatches = csv.GetRecords<DnaGedcomMatch>();

                // In case DNAGedcom file has data from more than one test, find the test ID with the largest number of matches.
                var matches = dnaGedcomMatches
                    .Where(match => match != null)
                    .GroupBy(match => match.TestId ?? "")
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                if (matches == null)
                {
                    return;
                }

                // This is the Test ID for the person taking the test
                serialized.TestTakerTestId = matches.Key;

                // Translate match properties from DNAGedcom naming to Shared Clustering naming.
                serialized.Matches = matches
                    .AsParallel()
                    .Select(match => new Match
                    {
                        MatchTestAdminDisplayName = match.Admin,
                        MatchTestDisplayName = match.Name,
                        TestGuid = match.MatchId,
                        SharedCentimorgans = GetDouble(match.SharedCm),
                        SharedSegments = int.TryParse(match.SharedSegments, out var sharedSegmentsInt) ? sharedSegmentsInt : 0,
                        TreeSize = int.TryParse(match.People, out var peopleInt) ? peopleInt : 0,
                        Starred = bool.TryParse(match.Starred, out var isStarred) && isStarred,
                        HasHint = bool.TryParse(match.Hint, out var hasHint) && hasHint,
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
        }

        private static readonly IFormatProvider[] _cultures = { CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo("en-US"), CultureInfo.InvariantCulture };

        public static double GetDouble(string value)
        {
            foreach (var culture in _cultures)
            {
                if (double.TryParse(value, NumberStyles.Any, culture, out var result))
                {
                    return result;
                }
            }

            return 0.0;
        }

        private static void ReadIcwFile(Serialized serialized, string icwFile)
        {
            using (var fileStream = new FileStream(icwFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var icwReader = new StreamReader(fileStream))
            using (var csv = new CsvReader(icwReader))
            {
                csv.Configuration.Delimiter = ",";
                csv.Configuration.HeaderValidated = null;
                csv.Configuration.MissingFieldFound = null;
                csv.Configuration.BadDataFound = null;
                csv.Configuration.LineBreakInQuotedFieldIsBadData = false;
                csv.Configuration.RegisterClassMap<DnaGedcomIcwMap>();
                csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.Replace('_', ' ');

                // Translate the ICW data.
                // Shared Clustering assumes that every match also matches themselves.
                // DNAGedcom does not include the self-matches in the saved ICW data,
                // so the self-matches need to be added during the translation.
                serialized.Icw = csv.GetRecords<DnaGedcomIcw>()
                    .Where(icw => icw != null)
                    .GroupBy(icw => icw.MatchId, icw => icw.IcwId)
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
            public string Starred { get; set; }
            //viewed { get; set; }
            //private { get; set; }
            public string Hint { get; set; }
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
                Map(m => m.Starred).Name("starred");
                Map(m => m.Hint).Name("hint");
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
