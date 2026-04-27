using System;

namespace ProjectStarkSemi.Conoscope
{
    public readonly record struct ConoscopeChromaticity(double x, double y, double u, double v, double Cct);

    public static class ConoscopeColorimetry
    {
        public static ConoscopeChromaticity Calculate(double X, double Y, double Z)
        {
            double x = 0;
            double y = 0;
            double u = 0;
            double v = 0;
            double cct = 0;

            double xyzSum = X + Y + Z;
            if (Math.Abs(xyzSum) > double.Epsilon)
            {
                x = X / xyzSum;
                y = Y / xyzSum;
            }

            double uvDenominator = X + 15 * Y + 3 * Z;
            if (Math.Abs(uvDenominator) > double.Epsilon)
            {
                u = 4 * X / uvDenominator;
                v = 9 * Y / uvDenominator;
            }

            if (Math.Abs(0.1858 - y) > double.Epsilon)
            {
                double n = (x - 0.3320) / (0.1858 - y);
                cct = 449.0 * Math.Pow(n, 3) + 3525.0 * Math.Pow(n, 2) + 6823.3 * n + 5520.33;
            }

            if (!double.IsFinite(cct) || cct < 0)
            {
                cct = 0;
            }

            return new ConoscopeChromaticity(x, y, u, v, cct);
        }

        public static double GetChannelValue(double X, double Y, double Z, ExportChannel channel)
        {
            if (channel is ExportChannel.CieX or ExportChannel.CieY or ExportChannel.CieU or ExportChannel.CieV)
            {
                ConoscopeChromaticity chromaticity = Calculate(X, Y, Z);
                return channel switch
                {
                    ExportChannel.CieX => chromaticity.x,
                    ExportChannel.CieY => chromaticity.y,
                    ExportChannel.CieU => chromaticity.u,
                    ExportChannel.CieV => chromaticity.v,
                    _ => Y
                };
            }

            return channel switch
            {
                ExportChannel.X => X,
                ExportChannel.Y => Y,
                ExportChannel.Z => Z,
                _ => Y
            };
        }

        public static OpenCvSharp.Mat CreateChannelMat(OpenCvSharp.Mat XMat, OpenCvSharp.Mat YMat, OpenCvSharp.Mat ZMat, ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => XMat.Clone(),
                ExportChannel.Y => YMat.Clone(),
                ExportChannel.Z => ZMat.Clone(),
                ExportChannel.CieX => CreateXyChannelMat(XMat, YMat, ZMat, XMat),
                ExportChannel.CieY => CreateXyChannelMat(XMat, YMat, ZMat, YMat),
                ExportChannel.CieU => CreateUvChannelMat(XMat, YMat, ZMat, XMat, 4.0),
                ExportChannel.CieV => CreateUvChannelMat(XMat, YMat, ZMat, YMat, 9.0),
                _ => YMat.Clone()
            };
        }

        private static OpenCvSharp.Mat CreateXyChannelMat(OpenCvSharp.Mat XMat, OpenCvSharp.Mat YMat, OpenCvSharp.Mat ZMat, OpenCvSharp.Mat numerator)
        {
            using OpenCvSharp.Mat denominator = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Add(XMat, YMat, denominator);
            OpenCvSharp.Cv2.Add(denominator, ZMat, denominator);
            return DivideWithZeroGuard(numerator, denominator);
        }

        private static OpenCvSharp.Mat CreateUvChannelMat(OpenCvSharp.Mat XMat, OpenCvSharp.Mat YMat, OpenCvSharp.Mat ZMat, OpenCvSharp.Mat sourceNumerator, double numeratorScale)
        {
            using OpenCvSharp.Mat denominator = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat scaledZ = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat numerator = new OpenCvSharp.Mat();

            OpenCvSharp.Cv2.AddWeighted(XMat, 1.0, YMat, 15.0, 0, denominator);
            ZMat.ConvertTo(scaledZ, ZMat.Type(), 3.0);
            OpenCvSharp.Cv2.Add(denominator, scaledZ, denominator);
            sourceNumerator.ConvertTo(numerator, sourceNumerator.Type(), numeratorScale);

            return DivideWithZeroGuard(numerator, denominator);
        }

        private static OpenCvSharp.Mat DivideWithZeroGuard(OpenCvSharp.Mat numerator, OpenCvSharp.Mat denominator)
        {
            OpenCvSharp.Mat result = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat zeroMask = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat safeDenominator = denominator.Clone();

            OpenCvSharp.Cv2.Compare(safeDenominator, OpenCvSharp.Scalar.All(0), zeroMask, OpenCvSharp.CmpTypes.EQ);
            safeDenominator.SetTo(OpenCvSharp.Scalar.All(1), zeroMask);
            OpenCvSharp.Cv2.Divide(numerator, safeDenominator, result);
            result.SetTo(OpenCvSharp.Scalar.All(0), zeroMask);
            return result;
        }

        public static string GetChannelLabel(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => "X",
                ExportChannel.Y => "Y",
                ExportChannel.Z => "Z",
                ExportChannel.CieX => "x",
                ExportChannel.CieY => "y",
                ExportChannel.CieU => "u",
                ExportChannel.CieV => "v",
                _ => "Y"
            };
        }

        public static string FormatChannelValue(double value, ExportChannel channel)
        {
            return channel is ExportChannel.CieX or ExportChannel.CieY or ExportChannel.CieU or ExportChannel.CieV
                ? value.ToString("F6")
                : value.ToString("F2");
        }

        public static string FormatCct(double cct)
        {
            return cct > 0 ? $"{cct:F0}K" : "--";
        }
    }
}