using ColorVision.Common.Extension;
using System;
using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Converter
{
    public class DescriptioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum  @enum)
            {
                return @enum.ToDescription();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
