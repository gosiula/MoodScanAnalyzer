using System.Globalization;
using System.Windows.Data;
using FontAwesome.WPF;

namespace MoodScanAnalyzer.Converters
{
    public class PlayPauseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isPlaying = (bool)value;
            return isPlaying ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
