using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Themes.Converter
{
    /// <summary>
    /// Converts an enum value to Visibility based on whether it matches an expected value.
    /// </summary>
    public sealed class EnumToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts an enum value to Visibility.
        /// Returns Visible if the value matches the parameter (expected value), otherwise Collapsed.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            // Support both direct enum comparison and string comparison
            if (value.GetType().IsEnum)
            {
                // If parameter is a string, convert it to the enum type
                if (parameter is string paramStr)
                {
                    try
                    {
                        var expectedValue = Enum.Parse(value.GetType(), paramStr);
                        return value.Equals(expectedValue) ? Visibility.Visible : Visibility.Collapsed;
                    }
                    catch (ArgumentException)
                    {
                        // Invalid enum value string - property should remain collapsed
                        return Visibility.Collapsed;
                    }
                }
                
                // Direct enum comparison
                return value.Equals(parameter) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("EnumToVisibilityConverter does not support two-way binding.");
        }
    }

    /// <summary>
    /// Inverted version: Returns Collapsed when enum matches, Visible when it doesn't.
    /// </summary>
    public sealed class EnumToVisibilityReConverter : IValueConverter
    {
        /// <summary>
        /// Converts an enum value to Visibility (inverted).
        /// Returns Collapsed if the value matches the parameter, otherwise Visible.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Visible;

            // Support both direct enum comparison and string comparison
            if (value.GetType().IsEnum)
            {
                // If parameter is a string, convert it to the enum type
                if (parameter is string paramStr)
                {
                    try
                    {
                        var expectedValue = Enum.Parse(value.GetType(), paramStr);
                        return value.Equals(expectedValue) ? Visibility.Collapsed : Visibility.Visible;
                    }
                    catch (ArgumentException)
                    {
                        // Invalid enum value string - property should remain visible
                        return Visibility.Visible;
                    }
                }
                
                // Direct enum comparison
                return value.Equals(parameter) ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("EnumToVisibilityReConverter does not support two-way binding.");
        }
    }
}
