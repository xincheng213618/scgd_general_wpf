using OpenCvSharp;
using System.Windows.Media;

namespace Pattern
{
    public static class PatternExtension
    {
        public static string ToColorTag(this SolidColorBrush solidColorBrush)
        {
            Color color = solidColorBrush.Color;
            if (color == Colors.Red) return "R";
            if (color == Colors.Lime) return "G";
            if (color == Colors.Blue) return "B";
            if (color == Colors.White) return "W";
            if (color == Colors.Black) return "K";
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static Scalar ToScalar(this SolidColorBrush solidColorBrush)
        {
            return new Scalar(solidColorBrush.Color.B, solidColorBrush.Color.G, solidColorBrush.Color.R, solidColorBrush.Color.A);
        }

        public static Scalar ToScalar(this Color color)
        {
            return new Scalar(color.B, color.G, color.R, color.A);
        }
    }
}
