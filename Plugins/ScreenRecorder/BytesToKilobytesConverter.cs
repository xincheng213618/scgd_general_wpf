using System.Globalization;
using System.Windows.Data;

namespace ScreenRecorder
{
    /// <summary>
    /// WPF值转换器：将字节转换为千字节（KB）
    /// </summary>
    public class BytesToKilobytesConverter : IValueConverter
    {
        /// <summary>
        /// 将字节转换为千字节
        /// </summary>
        /// <param name="value">字节值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换器参数（未使用）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>千字节值</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToInt64(value) / 1000;
        }

        /// <summary>
        /// 将千字节转换为字节
        /// </summary>
        /// <param name="value">千字节值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换器参数（未使用）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>字节值</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToInt64(value) * 1000;
        }
    }
}
