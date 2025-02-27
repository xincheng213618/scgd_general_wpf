#pragma warning disable CS8602,CS8604
using System;
using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI
{
    public class RoundToNearestHalfConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return Math.Round(doubleValue * 2, MidpointRounding.AwayFromZero) / 2;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return Math.Round(doubleValue * 2, MidpointRounding.AwayFromZero) / 2;
            }
            return value;
        }
    }
}
