using System.Collections.Generic;
using System.Text;
using AncestryDnaClustering.Models.HierarchicalClustering;

namespace AncestryDnaClustering.Models.DistanceFinding
{
    public class TextDistanceWriter : IDistanceWriter
    {
        private readonly string _fileName;
        private StringBuilder _stringBuilder = new StringBuilder();

        public TextDistanceWriter(string testTakerTestId, List<IClusterableMatch> matches, string fileName)
        {
            _fileName = fileName;
        }

        public void Dispose()
        {
            // Nothing to dispose in the text writer
        }

        public void WriteHeader(IClusterableMatch match)
        {
            if (match == null)
            {
                return;
            }

            _stringBuilder.AppendLine($"{match.Count}" +
                $"\t{match.Count}" +
                $"\t{match.Match.SharedCentimorgans:####.0}" +
                $"\t{match.Match.SharedSegments}" +
                //$"\t{match.Match.TreeType}" +
                //$"\t{match.Match.TreeSize}" +
                $"\t{match.Match.Name}" +
                $"\t{match.Match.TestGuid}" +
                $"\t{match.Match.Note}");
        }

        public void WriteLine(IClusterableMatch match, int overlapCount)
        {
            _stringBuilder.AppendLine(//$"{Math.Sqrt(closestMatch.DistSquared):N2}\t" +
                $"{match.Count}" +
                $"\t{overlapCount}" +
                $"\t{match.Match.SharedCentimorgans:####.0}" +
                $"\t{match.Match.SharedSegments}" +
                //$"\t{closestMatch.OtherMatch.Match.TreeType}" +
                //$"\t{closestMatch.OtherMatch.Match.TreeSize}" +
                $"\t{match.Match.Name}" +
                $"\t{match.Match.TestGuid}" +
                $"\t{match.Match.Note}");
        }

        public void SkipLine()
        {
            _stringBuilder.AppendLine();
        }

        public void Save()
        {
            FileUtils.WriteAllLines(_fileName, _stringBuilder.ToString(), false);
        }
    }
}