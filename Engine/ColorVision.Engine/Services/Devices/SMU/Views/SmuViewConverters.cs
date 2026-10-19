using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.SMU.Views;

public sealed class SmuReadingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double number || !double.IsFinite(number)) return "—";
        string unit = parameter as string ?? string.Empty;
        if (unit == "mA") { number /= 1000; unit = "A"; }
        double magnitude = Math.Abs(number);
        (double scale, string prefix) = magnitude switch
        {
            >= 1e9 => (1e9, "G"), >= 1e6 => (1e6, "M"), >= 1e3 => (1e3, "k"),
            >= 1 or 0 => (1, ""), >= 1e-3 => (1e-3, "m"), >= 1e-6 => (1e-6, "µ"),
            >= 1e-9 => (1e-9, "n"), _ => (1e-12, "p")
        };
        return (number / scale).ToString("G5", culture) + $" {prefix}{unit}";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class SmuHistoryHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values.Length > 2 && values[2] is Visibility.Collapsed ? new GridLength(0) : values[0] is Visibility.Visible
        ? new GridLength(values[1] is double height && double.IsFinite(height) && height > 0 ? Math.Max(140, height) : 200)
        : new GridLength(1, GridUnitType.Star);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
