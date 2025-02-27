#pragma warning disable CS8602,CS8604
using System;
using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI
{
    public class RadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (double.TryParse(value.ToString(), out double result))
            {
                if (result % 1 == 0 || result % 1 == 0.5)
                {
                    return result;
                }
            }
            return Binding.DoNothing;
        }
    }
}
