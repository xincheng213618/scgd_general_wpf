using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Services.PhyCameras;

public sealed class LicenseExpiryTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 5 || values[0] is not string fullText) return string.Empty;
        if (values[1] is not DateTime expiry || values[2] is not double width || width <= 0 || values[3] is not TextBlock textBlock)
            return fullText;

        double Measure(string text, FontWeight weight)
        {
            var probe = new TextBlock
            {
                Text = text, FontFamily = textBlock.FontFamily, FontStyle = textBlock.FontStyle,
                FontWeight = weight, FontStretch = textBlock.FontStretch,
                FontSize = textBlock.FontSize, FlowDirection = textBlock.FlowDirection,
                UseLayoutRounding = textBlock.UseLayoutRounding
            };
            System.Windows.Media.TextOptions.SetTextFormattingMode(probe, System.Windows.Media.TextOptions.GetTextFormattingMode(textBlock));
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return probe.DesiredSize.Width;
        }
        if (values[4] is string summary && summary.Length > 0)
            width -= Measure(summary, FontWeights.Normal) + textBlock.Margin.Right;
        return FitText(fullText, expiry, width, text => Measure(text, textBlock.FontWeight));
    }

    internal static string FitText(string fullText, DateTime expiry, double availableWidth, Func<string, double> measure)
    {
        if (measure(fullText) <= availableWidth) return fullText;
        string timestamp = expiry.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string minutes = fullText.Replace(timestamp, expiry.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (measure(minutes) <= availableWidth) return minutes;
        return fullText.Replace(timestamp, expiry.ToString("yyyy-MM-dd HH", CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
