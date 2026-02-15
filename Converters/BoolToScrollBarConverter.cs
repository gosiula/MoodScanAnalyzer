using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace MoodScanAnalyzer.Converters
{
    public class BoolToScrollBarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            }
            return ScrollBarVisibility.Disabled;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ScrollBarVisibility visibility)
            {
                return visibility == ScrollBarVisibility.Auto || visibility == ScrollBarVisibility.Visible;
            }
            return false;
        }
    }
}
