using OfficeOpenXml;
using SharedClustering.HierarchicalClustering;

namespace SharedClustering.Export.ColumnWriters
{
    /// <summary>
    /// Write one column of data to an output file
    /// </summary>
    public interface IColumnWriter
    {
        string Header { get; }
        bool RotateHeader { get; }
        bool IsAutofit { get; }
        bool IsDecimal { get; }
        double Width { get; }

        void WriteValue(ExcelRange cell, IClusterableMatch match, LeafNode leafNode);
        void ApplyConditionalFormatting(ExcelWorksheet ws, ExcelAddress excelAddress);
    }
}
