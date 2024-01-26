using System;
using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Converter
{

    public sealed class MemorySizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long memorySize)
            {
                return Common.Utilities.MemorySize.MemorySizeText(memorySize);
            }
            if (value is int memorySize1)
            {
                return Common.Utilities.MemorySize.MemorySizeText((long)memorySize1);
            }
            if (value is double memorySize2)
            {
                return Common.Utilities.MemorySize.MemorySizeText((long)memorySize2);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }

}
