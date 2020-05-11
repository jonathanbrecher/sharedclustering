namespace SharedClustering.Core
{
    /// <summary>
    /// An object that describes the "colored dots" categories used by Ancestry.
    /// </summary>
    public class Tag
    {
        public int TagId { get; set; }
        public string Color { get; set; }
        public string Label { get; set; }
    }
}
