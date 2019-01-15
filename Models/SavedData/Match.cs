namespace AncestryDnaClustering.Models.SavedData
{
    public class Match
    {
        public string MatchTestAdminDisplayName { get; set; }
        public string MatchTestDisplayName { get; set; }
        public string TestGuid { get; set; }
        public double SharedCentimorgans { get; set; }
        public int SharedSegments { get; set; }
        public TreeType TreeType { get; set; }
        public int TreeSize { get; set; }
        public string Note { get; set; }

        public string Name => MatchTestAdminDisplayName == MatchTestDisplayName ? MatchTestAdminDisplayName : $"{MatchTestDisplayName} (managed by {MatchTestAdminDisplayName})";
    }
}
