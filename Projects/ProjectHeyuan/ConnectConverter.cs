﻿using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Projects.ProjectHeyuan
{
    public sealed class ConnectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isconnect)
            {
                return isconnect ? "已经连接":"未连接";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }
}