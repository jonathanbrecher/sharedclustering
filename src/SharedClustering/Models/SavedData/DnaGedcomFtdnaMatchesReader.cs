using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using SharedClustering.Core;

namespace AncestryDnaClustering.Models.SavedData
{
    /// <summary>
    /// Read files saved by DNAGedcom.
    /// </summary>
    public class DnaGedcomFtdnaMatchesReader : ISerializedMatchesReader
    {
        public bool IsSupportedFileType(string fileName) => fileName != null && Path.GetExtension(fileName).ToLower() == ".csv";

        public string GetTrimmedFileName(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return null;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            foreach (var suffix in new[] { "_family_finder_matches", "_icw" })
            {
                if (fileNameWithoutExtension.ToLower().EndsWith(suffix))
                {
                    return fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - suffix.Length);
                }
            }

            return null;
        }

        public async Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName, IProgressData progressData)
        {
            if (!IsSupportedFileType(fileName))
            {
                return (null, $"{fileName} is not a *.csv file");
            }

            // DNAGedcom saves two files: a match file ending with _Family_Finder_Matches,
            // and an in-common-with file ending with _ICW
            var trimmedFileName = GetTrimmedFileName(fileName);
            if (trimmedFileName == null)
            {
                return (null, "File name does not end with _Family_Finder_Matches or _ICW");
            }

            var path = Path.GetDirectoryName(fileName);
            var matchFile = Path.Combine(path, $"{trimmedFileName}_Family_Finder_Matches.csv");
            var icwFile = Path.Combine(path, $"{trimmedFileName}_ICW.csv");

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

            try
            {
                var treeFile = Path.Combine(path, $"{trimmedFileName}_Family_Finder_Trees.csv");
                await Task.Run(() => ReadTreeFile(serialized, treeFile));
            }
            catch (Exception)
            {
                // Not a problem if we can't read the tree file
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
                if (dnaGedcomMatches == null)
                {
                    return;
                }

                // Translate match properties from DNAGedcom naming to Shared Clustering naming.
                serialized.Matches = dnaGedcomMatches
                    .Where(match => match != null)
                    .AsParallel()
                    .Select(match => new Match
                    {
                        MatchTestDisplayName = match.Name,
                        TestGuid = match.MatchId,
                        SharedCentimorgans = GetDouble(match.SharedCm),
                        LongestBlock = GetDouble(match.LongestBlock),
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

        private static void ReadTreeFile(Serialized serialized, string treeFile)
        {
            using (var fileStream = new FileStream(treeFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var treeReader = new StreamReader(fileStream))
            using (var csv = new CsvReader(treeReader))
            {
                csv.Configuration.Delimiter = ",";
                csv.Configuration.HeaderValidated = null;
                csv.Configuration.MissingFieldFound = null;
                csv.Configuration.BadDataFound = null;
                csv.Configuration.LineBreakInQuotedFieldIsBadData = false;
                csv.Configuration.RegisterClassMap<DnaGedcomTreeNodeMap>();
                csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.Replace('_', ' ');

                // Translate the ICW data.
                // Shared Clustering assumes that every match also matches themselves.
                // DNAGedcom does not include the self-matches in the saved ICW data,
                // so the self-matches need to be added during the translation.
                var trees = csv.GetRecords<DnaGedcomTreeNode>()
                    .Where(treeNode => treeNode?.ResultId != null)
                    .ToLookup(treeNode => treeNode.ResultId);

                foreach (var match in serialized.Matches)
                {
                    match.TreeSize = trees[match.TestGuid].Count();
                }
            }
        }

        // Load all fields as strings and parse manually, to protect against parse failures
        private class DnaGedcomMatch
        {
            public string MatchId { get; set; }
            public string Name { get; set; }
            public string SharedCm { get; set; }
            //public string MatchDate { get; set; }
            //public string RelationshipRange { get; set; }
            //public string SuggestedRelationship { get; set; }
            public string LongestBlock { get; set; }
            //public string KnownRelationship { get; set; }
            //public string Email { get; set; }
            //public string Ancestral { get; set; }
            //public string YdnaHaplogroup { get; set; }
            //public string MtDnaHaplogroup { get; set; }
            //public string Notes { get; set; }
            //public string Name { get; set; }
        }

        private sealed class DnaGedcomMatchMap : ClassMap<DnaGedcomMatch>
        {
            public DnaGedcomMatchMap()
            {
                //Map(m => m.TestId).Name("testid");
                Map(m => m.MatchId).Name("ResultID2");
                Map(m => m.Name).Name("Full Name");
                //Map(m => m.Admin).Name("admin");
                //Map(m => m.People).Name("people");
                Map(m => m.SharedCm).Name("Shared cM");
                //Map(m => m.SharedSegments).Name("sharedSegments");
                Map(m => m.LongestBlock).Name("Longest Block");
                //Map(m => m.Note).Name("note");
            }
        }

        private class DnaGedcomIcw
        {
            public string MatchId { get; set; }
            public string MatchName { get; set; }
            public string IcwId { get; set; }
            public string IcwName { get; set; }
            //public string TotalCm { get; set; }
            //public string MaxCm { get; set; }
            //public string Email { get; set; }
        }

        private sealed class DnaGedcomIcwMap : ClassMap<DnaGedcomIcw>
        {
            public DnaGedcomIcwMap()
            {
                Map(m => m.MatchId).Name("Profile KitID");
                Map(m => m.MatchName).Name("Profile Name");
                Map(m => m.IcwId).Name("Match KitID");
                Map(m => m.IcwName).Name("Full Name");
            }
        }

        private class DnaGedcomTreeNode
        {
            public string ResultId { get; set; }
            //public string TreeId { get; set; }
            //public string KitNumber { get; set; }
            //public string FirstName { get; set; }
            //public string MiddleName { get; set; }
            //public string LastName { get; set; }
            //public string MotherId { get; set; }
            //public string FatherId { get; set; }
            //public string Generation { get; set; }
            //public string Gender { get; set; }
            //public string BirthDate { get; set; }
            //public string BirthPlace { get; set; }
            //public string DeathDate { get; set; }
            //public string DeathPlace { get; set; }
        }

        private sealed class DnaGedcomTreeNodeMap : ClassMap<DnaGedcomTreeNode>
        {
            public DnaGedcomTreeNodeMap()
            {
                Map(m => m.ResultId).Name("Resultid");
            }
        }
    }
}
