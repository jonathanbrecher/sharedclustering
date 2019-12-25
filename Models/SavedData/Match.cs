using System.Collections.Generic;

namespace AncestryDnaClustering.Models.SavedData
{
    public class Match
    {
        public string MatchTestAdminDisplayName { get; set; }
        public string MatchTestDisplayName { get; set; }
        public string TestGuid { get; set; }
        public double SharedCentimorgans { get; set; }
        public int SharedSegments { get; set; }
        public double LongestBlock { get; set; }
        public TreeType TreeType { get; set; }
        public string TreeUrl { get; set; }
        public int TreeSize { get; set; }
        public bool HasCommonAncestors { get; set; }
        public List<string> CommonAncestors { get; set; }
        public bool Starred { get; set; }
        public bool HasHint { get; set; }
        public string Note { get; set; }

        public bool IsFather { get; set; }
        public bool IsMother { get; set; }

        public string Name 
            => string.IsNullOrWhiteSpace(MatchTestAdminDisplayName)
            ? MatchTestDisplayName
            : MatchTestDisplayName == MatchTestAdminDisplayName
                ? MatchTestAdminDisplayName 
                : $"{MatchTestDisplayName} (managed by {MatchTestAdminDisplayName})";
    }
}
