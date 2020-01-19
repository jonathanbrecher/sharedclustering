namespace AncestryDnaClustering.Models.Anonymizers
{
    public interface IAnonymizer
    {
        string GetAnonymizedName(string originalName);
        string GetAnonymizedGuid(string originalGuid);
    }
}
