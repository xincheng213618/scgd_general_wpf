﻿using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace ColorVision.Themes.Converter
{
    public sealed class BooleanToVisibilityReConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
        {
            return (Visibility) value  == Visibility.Visible;
        }
    }
}
