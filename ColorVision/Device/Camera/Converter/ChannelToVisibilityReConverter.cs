using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using ColorVision.Device.Camera;

namespace ColorVision.Device.Camera.Converter
{
    public sealed class ChannelToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is ImageChannel imageChannel)
            {
                return imageChannel == ImageChannel.One ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
        {
            return (Visibility) value  == Visibility.Visible;
        }
    }

    public sealed class ChannelToVisibilityReConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImageChannel imageChannel)
            {
                return imageChannel == ImageChannel.One ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
        }
    }
}
