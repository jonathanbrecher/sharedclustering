using System;
using System.Globalization;
using System.Windows.Data;

namespace AncestryDnaClustering.Views
{
    /// <summary>
    /// Display a value as percentage if non-zero, blank if zero
    /// </summary>
    internal class PercentageNullZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToDouble(value) == 0.0 ? null: string.Format(culture, "{0:P2}", value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
