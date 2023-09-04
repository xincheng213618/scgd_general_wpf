using System;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.Extension
{
    /// <summary>
    // 扩展加载，没有特殊标记的丢在这里，反正会自动识别加载
    /// </summary>
    internal static class Extensions
    {

        internal static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);


        internal static SolidColorBrush ToBrush(this Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        internal static SolidColorBrush ToBrush(this Color? color)
        {
            if (color == null)
                return new SolidColorBrush(Colors.Transparent);
            return new SolidColorBrush((Color)color);
        }

        internal static Color ToColor(this string color)
        {
            return (Color)ColorConverter.ConvertFromString(color);
        }

        internal static Color ToColor(this SolidColorBrush brush)
        {
            if (brush == null)
                return Colors.Transparent;
            return brush.Color;
        }

        internal static Color ToColor(this Brush brush)
        {
            if (brush == null)
                return Colors.Transparent;
            if (brush is SolidColorBrush)
                return (brush as SolidColorBrush).Color;
            else if (brush is LinearGradientBrush)
                return (brush as LinearGradientBrush).GradientStops[0].Color;
            else if (brush is RadialGradientBrush)
                return (brush as RadialGradientBrush).GradientStops[0].Color;
            else
                return Colors.Transparent;
        }

        /// <summary>
        /// 对图标的扩展
        /// </summary>
        /// <param name="icon"></param>
        /// <returns></returns>
        internal static ImageSource ToImageSource(this System.Drawing.Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }


        internal static string Description(object obj)
        {
            Type type = obj.GetType();
            MemberInfo[] infos = type.GetMember(obj.ToString() ?? "");
            if (infos != null && infos.Length != 0)
            {
                object[] attrs = infos[0].GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);
                if (attrs != null && attrs.Length != 0)
                {
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }
            return type.ToString();
        }


    }
}
