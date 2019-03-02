namespace AncestryDnaClustering.Models.SavedData
{
    public class Match
    {
        public string MatchTestAdminDisplayName { get; set; }
        public string MatchTestDisplayName { get; set; }
        public string TestId { get; set; }
        public double SharedCentimorgans { get; set; }
        public int SharedSegments { get; set; }
        public double LongestBlock { get; set; }
        public TreeType TreeType { get; set; }
        public int TreeSize { get; set; }
        public bool Starred { get; set; }
        public bool HasHint { get; set; }
        public string Note { get; set; }

        public string Name 
            => string.IsNullOrWhiteSpace(MatchTestAdminDisplayName)
            ? MatchTestDisplayName
            : MatchTestDisplayName == MatchTestAdminDisplayName
                ? MatchTestAdminDisplayName 
                : $"{MatchTestDisplayName} (managed by {MatchTestAdminDisplayName})";
    }
}
