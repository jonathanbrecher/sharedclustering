using OfficeOpenXml;

namespace AncestryDnaClustering.Models.HierarchicalClustering.ColumnWriters
{
    public class GenericObjectWriter : IColumnWriter
    {
        public string Header { get; }
        public bool RotateHeader => true;
        public bool IsAutofit => true;
        public bool IsDecimal => false;
        public double Width => 15;

        public object GenericObject { get; set; }

        public GenericObjectWriter(string header)
        {
            Header = header;
        }

        public void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode) => cell.Value = GenericObject;
    }
}
