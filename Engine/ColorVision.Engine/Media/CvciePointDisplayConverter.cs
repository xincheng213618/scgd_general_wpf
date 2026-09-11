using ColorVision.Engine.Templates.POI;
using System;
using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Engine.Media;

internal sealed class CvciePointDisplayConverter(bool size, string format) : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PoiPoint point) return "";
        return (size ? point.Width : point.PixelX).ToString(format, culture) + ", " + (size ? point.Height : point.PixelY).ToString(format, culture);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
