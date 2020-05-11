namespace SharedClustering.Core.Anonymizers
{
    public interface IAnonymizer
    {
        string GetAnonymizedName(string originalName);
        string GetAnonymizedGuid(string originalGuid);
    }
}
