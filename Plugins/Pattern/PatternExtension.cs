using OpenCvSharp;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern
{
    public static class PatternExtension
    {
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
