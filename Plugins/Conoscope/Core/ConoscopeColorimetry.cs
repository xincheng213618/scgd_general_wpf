using System;
using System.Globalization;

namespace Conoscope.Core
{
    public readonly record struct ConoscopeChromaticity(double x, double y, double u, double v, double Cct);
    public readonly record struct ConoscopeUvReference(double U, double V);

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
            if (channel is ExportChannel.ColorDifference or ExportChannel.Contrast)
            {
                throw new InvalidOperationException(channel == ExportChannel.ColorDifference
                    ? Properties.Resources.ColorDiffNeedsUVRef
                    : Properties.Resources.Conoscope_ContrastNeedsReference);
            }

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
                ExportChannel.ColorDifference => throw new InvalidOperationException(Conoscope.Properties.Resources.ColorDiffNeedsUVRef),
                ExportChannel.Contrast => throw new InvalidOperationException(Conoscope.Properties.Resources.MsgContrastReferenceRequired),
                _ => YMat.Clone()
            };
        }

        public static double CalculateColorDifference(double X, double Y, double Z, double referenceU, double referenceV)
        {
            ConoscopeChromaticity chromaticity = Calculate(X, Y, Z);
            double deltaU = chromaticity.u - referenceU;
            double deltaV = chromaticity.v - referenceV;
            return Math.Sqrt(deltaU * deltaU + deltaV * deltaV);
        }

        public static OpenCvSharp.Mat CreateColorDifferenceMat(OpenCvSharp.Mat XMat, OpenCvSharp.Mat YMat, OpenCvSharp.Mat ZMat, double referenceU, double referenceV)
        {
            using OpenCvSharp.Mat deltaU = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat deltaV = new OpenCvSharp.Mat();
            CreateUvChannelMats(XMat, YMat, ZMat, deltaU, deltaV);
            OpenCvSharp.Cv2.Subtract(deltaU, OpenCvSharp.Scalar.All(referenceU), deltaU);
            OpenCvSharp.Cv2.Subtract(deltaV, OpenCvSharp.Scalar.All(referenceV), deltaV);

            OpenCvSharp.Mat result = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Magnitude(deltaU, deltaV, result);
            return result;
        }

        public static OpenCvSharp.Mat CreateColorDifferenceMat(OpenCvSharp.Mat XMat, OpenCvSharp.Mat YMat, OpenCvSharp.Mat ZMat, OpenCvSharp.Mat referenceUMat, OpenCvSharp.Mat referenceVMat)
        {
            EnsureSameSize(XMat, referenceUMat, Properties.Resources.UReferenceImage);
            EnsureSameSize(XMat, referenceVMat, Properties.Resources.Conoscope_VReferenceImage);

            using OpenCvSharp.Mat deltaU = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat deltaV = new OpenCvSharp.Mat();
            CreateUvChannelMats(XMat, YMat, ZMat, deltaU, deltaV);

            OpenCvSharp.Mat? convertedReferenceU = ConvertReferenceIfRequired(referenceUMat, deltaU.Type());
            OpenCvSharp.Mat? convertedReferenceV = ConvertReferenceIfRequired(referenceVMat, deltaV.Type());
            try
            {
                OpenCvSharp.Cv2.Subtract(deltaU, convertedReferenceU ?? referenceUMat, deltaU);
                OpenCvSharp.Cv2.Subtract(deltaV, convertedReferenceV ?? referenceVMat, deltaV);

                OpenCvSharp.Mat result = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.Magnitude(deltaU, deltaV, result);
                return result;
            }
            finally
            {
                convertedReferenceU?.Dispose();
                convertedReferenceV?.Dispose();
            }
        }

        public static OpenCvSharp.Mat CreateContrastMat(OpenCvSharp.Mat currentYMat, OpenCvSharp.Mat referenceYMat, ContrastReferenceKind referenceKind)
        {
            EnsureSameSize(currentYMat, referenceYMat, Properties.Resources.Conoscope_ContrastReferenceImage);
            return referenceKind == ContrastReferenceKind.Black
                ? DivideWithZeroGuard(currentYMat, referenceYMat)
                : DivideWithZeroGuard(referenceYMat, currentYMat);
        }

        public static double CalculateContrast(double currentY, double referenceY, ContrastReferenceKind referenceKind)
        {
            double numerator = referenceKind == ContrastReferenceKind.Black ? currentY : referenceY;
            double denominator = referenceKind == ContrastReferenceKind.Black ? referenceY : currentY;
            return denominator > double.Epsilon ? numerator / denominator : 0;
        }

        private static void EnsureSameSize(OpenCvSharp.Mat source, OpenCvSharp.Mat reference, string referenceName)
        {
            if (source.Width != reference.Width || source.Height != reference.Height)
            {
                throw new InvalidOperationException(CompositeFormatCache.Format(Properties.Resources.SizeMismatchFormat, referenceName));
            }
        }

        private static OpenCvSharp.Mat? ConvertReferenceIfRequired(OpenCvSharp.Mat source, OpenCvSharp.MatType targetType)
        {
            if (source.Type() == targetType)
            {
                return null;
            }

            OpenCvSharp.Mat converted = new OpenCvSharp.Mat();
            source.ConvertTo(converted, targetType);
            return converted;
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
            using OpenCvSharp.Mat denominator = CreateUvDenominator(XMat, YMat, ZMat);
            return DivideWithZeroGuard(sourceNumerator, denominator, numeratorScale);
        }

        private static void CreateUvChannelMats(
            OpenCvSharp.Mat XMat,
            OpenCvSharp.Mat YMat,
            OpenCvSharp.Mat ZMat,
            OpenCvSharp.Mat uMat,
            OpenCvSharp.Mat vMat)
        {
            using OpenCvSharp.Mat denominator = CreateUvDenominator(XMat, YMat, ZMat);
            using OpenCvSharp.Mat nonPositiveMask = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Compare(denominator, OpenCvSharp.Scalar.All(0), nonPositiveMask, OpenCvSharp.CmpTypes.LE);

            OpenCvSharp.Cv2.Divide(XMat, denominator, uMat, 4.0);
            OpenCvSharp.Cv2.Divide(YMat, denominator, vMat, 9.0);
            uMat.SetTo(OpenCvSharp.Scalar.All(0), nonPositiveMask);
            vMat.SetTo(OpenCvSharp.Scalar.All(0), nonPositiveMask);
        }

        private static OpenCvSharp.Mat CreateUvDenominator(OpenCvSharp.Mat XMat, OpenCvSharp.Mat YMat, OpenCvSharp.Mat ZMat)
        {
            OpenCvSharp.Mat denominator = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.AddWeighted(XMat, 1.0, YMat, 15.0, 0, denominator);
            OpenCvSharp.Cv2.AddWeighted(denominator, 1.0, ZMat, 3.0, 0, denominator);
            return denominator;
        }

        private static OpenCvSharp.Mat DivideWithZeroGuard(OpenCvSharp.Mat numerator, OpenCvSharp.Mat denominator, double scale = 1.0)
        {
            OpenCvSharp.Mat result = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat zeroMask = new OpenCvSharp.Mat();

            OpenCvSharp.Cv2.Compare(denominator, OpenCvSharp.Scalar.All(0), zeroMask, OpenCvSharp.CmpTypes.LE);
            OpenCvSharp.Cv2.Divide(numerator, denominator, result, scale);
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
                ExportChannel.ColorDifference => "Δuv",
                ExportChannel.Contrast => Properties.Resources.Conoscope_ContrastChannel,
                _ => "Y"
            };
        }

        public static string FormatChannelValue(double value, ExportChannel channel)
        {
            if (!double.IsFinite(value))
            {
                return "--";
            }

            if (channel == ExportChannel.Contrast)
            {
                return value.ToString("F3", CultureInfo.InvariantCulture);
            }

            return channel is ExportChannel.CieX or ExportChannel.CieY or ExportChannel.CieU or ExportChannel.CieV or ExportChannel.ColorDifference
                ? value.ToString("F6", CultureInfo.InvariantCulture)
                : value.ToString("F2", CultureInfo.InvariantCulture);
        }

        public static string FormatChannelValue(double value, ExportChannel channel, int decimalPlaces)
        {
            if (!double.IsFinite(value))
            {
                return "--";
            }

            int normalizedDecimalPlaces = Math.Clamp(decimalPlaces, 0, 8);
            return value.ToString($"F{normalizedDecimalPlaces}", CultureInfo.InvariantCulture);
        }

    }
}
