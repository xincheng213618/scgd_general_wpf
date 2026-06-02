using ColorVision.UI.LogImp.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI.LogImp.Controls
{
    internal sealed class LogEntryForegroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 8)
            {
                return DependencyProperty.UnsetValue;
            }

            var normalBrush = values[2] as Brush;
            if (values[0] is not LogEntryLevel level || values[1] is not bool useLevelColors || !useLevelColors)
            {
                return normalBrush ?? DependencyProperty.UnsetValue;
            }

            var brush = level switch
            {
                LogEntryLevel.Warning => values[3] as Brush,
                LogEntryLevel.Error => values[4] as Brush,
                LogEntryLevel.Fatal => values[5] as Brush,
                LogEntryLevel.Debug => values[6] as Brush,
                LogEntryLevel.Trace => values[7] as Brush,
                _ => normalBrush
            };

            return brush ?? normalBrush ?? DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
