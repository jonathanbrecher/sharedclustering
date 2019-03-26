using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace AncestryDnaClustering.Models.SavedData
{
    /// <summary>
    /// Read files saved by AutoCluster.
    /// </summary>
    public class AutoClusterCsvMatchesReader : ISerializedMatchesReader
    {
        public bool IsSupportedFileType(string fileName) => fileName != null && Path.GetExtension(fileName).ToLower() == ".csv";

        public string GetTrimmedFileName(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return null;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            return fileNameWithoutExtension;
        }

        public async Task<(Serialized input, string errorMessage)> ReadFileAsync(string fileName)
        {
            if (!IsSupportedFileType(fileName))
            {
                return (null, $"{fileName} is not a *.csv file");
            }

            var serialized = new Serialized();

            try
            {
                await Task.Run(() => ReadMatchFile(serialized, fileName));
            }
            catch (Exception ex)
            {
                return (null, $"Unexpected error while reading AutoCluster match file: {ex.Message}");
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
                csv.Configuration.RegisterClassMap<AutoClusterMatchMap>();

                serialized.Matches = new List<Match>();
                serialized.MatchIndexes = new Dictionary<string, int>();
                serialized.Icw = new Dictionary<string, List<int>>();

                csv.Read();
                csv.ReadHeader();

                var firstMatchFieldIndex = csv.GetFieldIndex("Cluster") + 1;
                if (firstMatchFieldIndex <= 0)
                {
                    firstMatchFieldIndex = csv.GetFieldIndex("cluster") + 1;
                }

                while (csv.Read())
                {
                    var match = csv.GetRecord<AutoClusterMatch>();

                    // Do not assume that the AutoCluster data is free of duplicates.
                    if (serialized.MatchIndexes.ContainsKey(match.Identifier))
                    {
                        continue;
                    }

                    var resultMatch = new Match
                    {
                        MatchTestDisplayName = match.Name,
                        TestGuid = match.Identifier,
                        SharedCentimorgans = GetDouble(match.SharedCm),
                        TreeUrl = match.Tree,
                        TreeSize = GetInt(match.TreeCount),
                        Note = match.Notes,
                    };

                    // AutoCluster sometimes writes invalid CSV files, not properly quoting a line break in the notes field.
                    // When that happens the ICW data cannot be read
                    var numHeaderFields = firstMatchFieldIndex;
                    while (csv.Context.Record.Length <= numHeaderFields)
                    {
                        csv.Read();
                        numHeaderFields = 0;
                    }

                    var icw = csv.Context.Record
                        .Skip(numHeaderFields)
                        .Where(value => !string.IsNullOrEmpty(value))
                        .Select(value => int.TryParse(value, out var intValue) ? intValue : (int?)null)
                        .Where(value => value != null)
                        .Select(value => value.Value - 1) // AutoCluster indexes are 1-based
                        .ToList();

                    // AutoCluster sometimes writes invalid CSV files, not properly quoting a line break in the notes field.
                    // When that happens the ICW data cannot be read
                    if (icw.Count == 0)
                    {
                        continue;
                    }

                    serialized.Matches.Add(resultMatch);
                    serialized.MatchIndexes[match.Identifier] = serialized.MatchIndexes.Count;
                    serialized.Icw[match.Identifier] = icw;
                }
            }

            // Do not assume that the AutoCluster data is already ordered by descending Shared Centimorgans.
            var unsortedIndexes = serialized.MatchIndexes;

            serialized.Matches = serialized.Matches.OrderByDescending(match => match.SharedCentimorgans).ToList();

            serialized.MatchIndexes = serialized.Matches
                .Select((match, index) => new { match.TestGuid, index })
                .ToDictionary(pair => pair.TestGuid, pair => pair.index);

            var indexUpdates = unsortedIndexes.ToDictionary(
                kvp => kvp.Value,
                kvp => serialized.MatchIndexes[kvp.Key]);

            serialized.Icw = serialized.Icw.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(index => indexUpdates[index]).ToList());
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

        public static int GetInt(string value)
        {
            foreach (var culture in _cultures)
            {
                if (int.TryParse(value, NumberStyles.Any, culture, out var result))
                {
                    return result;
                }
            }

            return 0;
        }

        // Load all fields as strings and parse manually, to protect against parse failures
        private class AutoClusterMatch
        {
            public string Identifier { get; set; }
            public string Name { get; set; }
            public string SharedCm { get; set; }
            public string Tree { get; set; }
            public string TreeCount { get; set; }
            public string Notes { get; set; }
        }

        private sealed class AutoClusterMatchMap : ClassMap<AutoClusterMatch>
        {
            public AutoClusterMatchMap()
            {
                Map(m => m.Identifier).Name("Identifier");
                Map(m => m.Name).Name("Name");
                Map(m => m.SharedCm).Name("total shared cM");
                Map(m => m.Tree).Name("Tree");
                Map(m => m.TreeCount).Name("Tree Person Count");
                Map(m => m.Notes).Name("Notes");
            }
        }
    }
}
