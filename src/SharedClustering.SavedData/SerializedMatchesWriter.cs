namespace AncestryDnaClustering.SavedData
{
    public class SerializedMatchesWriter : ISerializedMatchesWriter
    {
        public void Write(string fileName, Serialized serialized)
        {
            FileUtils.WriteAsJson(fileName, serialized, false);
        }
    }
}
