using System.Threading.Tasks;

namespace SharedClustering.Sample
{
    class Program
    {
        static async Task Main(string[] _)
        {
            var usageSample = new UsageSample();
            await usageSample.DoClusteringAsync();
        }
    }
}
