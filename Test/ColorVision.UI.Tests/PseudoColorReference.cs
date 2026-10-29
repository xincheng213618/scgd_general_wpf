using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

// Independent pre-optimization implementation. Protects quantization, threshold and color semantics.
internal static class PseudoColorReference
{
        internal static Mat Apply(Mat source, PseudoColorParameters parameters)
        {
            using Mat gray = SelectChannel(source, parameters.Channel);
            using Mat gray8 = ConvertPseudoColorSource(gray, parameters, out double minimum, out double maximum);
            using Mat scaled = new();
            if (maximum > minimum)
            {
                gray8.ConvertTo(scaled, MatType.CV_8U, byte.MaxValue / (maximum - minimum), -minimum * byte.MaxValue / (maximum - minimum));
            }
            else
            {
                gray8.CopyTo(scaled);
            }
            Mat result = new();
            Cv2.ApplyColorMap(scaled, result, (OpenCvSharp.ColormapTypes)(int)parameters.Colormap);
            using Mat below = new();
            using Mat above = new();
            Cv2.Compare(gray8, minimum, below, CmpTypes.LT);
            Cv2.Compare(gray8, maximum, above, CmpTypes.GT);
            result.SetTo(Scalar.Black, below);
            result.SetTo(Scalar.White, above);
            return result;
        }

        private static Mat SelectChannel(Mat source, int channel)
        {
            if (source.Channels() == 1) return source.Clone();
            if (channel >= 0 && channel < source.Channels())
            {
                Mat selected = new();
                Cv2.ExtractChannel(source, selected, channel);
                return selected;
            }
            return ConvertToGray(source);
        }

        private static Mat ConvertPseudoColorSource(Mat source, PseudoColorParameters parameters, out double minimum, out double maximum)
        {
            if (parameters.UseNominalRange)
            {
                minimum = 0;
                maximum = byte.MaxValue;
                return ConvertTo8BitNominal(source);
            }

            minimum = parameters.Minimum;
            maximum = parameters.Maximum;
            if (source.Depth() == MatType.CV_8U) return source.Clone();

            Mat result = new();
            if (source.Depth() == MatType.CV_16U)
            {
                if (parameters.AutoRange)
                {
                    double range = parameters.DataMaximum - (double)parameters.DataMinimum;
                    source.ConvertTo(result, MatType.CV_8U, byte.MaxValue / range, -parameters.DataMinimum * byte.MaxValue / range);
                    minimum = Math.Clamp((parameters.Minimum - (double)parameters.DataMinimum) / range * byte.MaxValue, 0, byte.MaxValue);
                    maximum = Math.Clamp((parameters.Maximum - (double)parameters.DataMinimum) / range * byte.MaxValue, 0, byte.MaxValue);
                }
                else
                {
                    source.ConvertTo(result, MatType.CV_8U, 1d / 257d);
                    minimum = Math.Min(byte.MaxValue, parameters.Minimum >> 8);
                    maximum = Math.Min(byte.MaxValue, parameters.Maximum >> 8);
                }
                return result;
            }

            if (source.Depth() == MatType.CV_32F || source.Depth() == MatType.CV_64F)
            {
                Cv2.Normalize(source, result, 0, byte.MaxValue, NormTypes.MinMax, MatType.CV_8U);
                if (parameters.AutoRange)
                {
                    minimum = 0;
                    maximum = byte.MaxValue;
                }
                else
                {
                    minimum = Math.Min(byte.MaxValue, minimum);
                    maximum = Math.Min(byte.MaxValue, maximum);
                }
                return result;
            }

            throw new NotSupportedException($"Unsupported pseudo-color image depth: {source.Depth()}.");
        }

        internal static Mat ConvertToGray(Mat source)
        {
            if (source.Channels() == 1) return source.Clone();
            Mat result = new();
            Cv2.CvtColor(source, result, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
            return result;
        }

        internal static Mat ConvertToBgr(Mat source)
        {
            if (source.Channels() == 3) return source.Clone();
            Mat result = new();
            Cv2.CvtColor(source, result, source.Channels() == 4 ? ColorConversionCodes.BGRA2BGR : ColorConversionCodes.GRAY2BGR);
            return result;
        }

        internal static Mat ConvertTo8BitNominal(Mat source)
        {
            if (source.Depth() == MatType.CV_8U) return source.Clone();
            Mat result = new();
            MatType depth = source.Depth();
            double scale;
            if (depth == MatType.CV_16U) scale = byte.MaxValue / (double)ushort.MaxValue;
            else if (depth == MatType.CV_32F || depth == MatType.CV_64F) scale = byte.MaxValue;
            else throw new NotSupportedException($"Unsupported image depth: {depth}");
            source.ConvertTo(result, MatType.MakeType(MatType.CV_8U, source.Channels()), scale);
            return result;
        }

}

