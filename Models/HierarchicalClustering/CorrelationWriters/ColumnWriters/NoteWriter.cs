using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters.ColumnWriters
{
    public class NoteWriter : IColumnWriter
    {
        public string Header => "Note";
        public bool RotateHeader => false;
        public bool IsAutofit => false;
        public bool IsDecimal => false;
        public double Width => 15;

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode)
        {
            if (!string.IsNullOrEmpty(match.Match.Note))
            {
                cell.Value = match.Match.Note;
            }
        }
    }
}
