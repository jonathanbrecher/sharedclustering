namespace AncestryDnaClustering.SavedData
{
    public interface ISerializedMatchesWriter
    {
        void Write(string fileName, Serialized serialized);
    }
}
