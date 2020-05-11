namespace SharedClustering.Core.Anonymizers
{
    public class NonAnonymizer : IAnonymizer
    {
        public string GetAnonymizedName(string originalName) => originalName;
        public string GetAnonymizedGuid(string originalGuid) => originalGuid;
    }
}
