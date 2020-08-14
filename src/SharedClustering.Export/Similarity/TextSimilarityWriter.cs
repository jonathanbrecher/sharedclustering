using SharedClustering.Core;
using SharedClustering.HierarchicalClustering;
using System.Collections.Generic;
using System.Text;

namespace SharedClustering.Export.Similarity
{
    public class TextSimilarityWriter : ISimilarityWriter
    {
        private readonly string _fileName;
        private readonly ICoreFileUtils _coreFileUtils;
        private StringBuilder _stringBuilder = new StringBuilder();

        public TextSimilarityWriter(string testTakerTestId, List<IClusterableMatch> matches, string fileName, ICoreFileUtils coreFileUtils)
        {
            _fileName = fileName;
            _coreFileUtils = coreFileUtils;
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

        public bool FileLimitReached() => false;

        public string Save()
        {
            _coreFileUtils.WriteAllLines(_fileName, _stringBuilder.ToString(), false, false);
            return _fileName;
        }
    }
}