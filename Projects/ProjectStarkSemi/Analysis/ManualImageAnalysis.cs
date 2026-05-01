using ColorVision.FileIO;
using ProjectStarkSemi.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ProjectStarkSemi.Analysis
{
    public sealed record ImageMeasurement(
        string FilePath,
        double X,
        double Y,
        double Z,
        ConoscopeChromaticity Chromaticity)
    {
        public string FileName => Path.GetFileName(FilePath);
        public double Luminance => Y;
    }

    public interface IImageMeasurementProvider
    {
        string Name { get; }
        bool CanRead(string filePath);
        ImageMeasurement Read(string filePath);
    }

    public static class ImageMeasurementProviderRegistry
    {
        private static readonly ObservableCollection<IImageMeasurementProvider> providers = new()
        {
            new CvcieImageMeasurementProvider()
        };

        public static IReadOnlyList<IImageMeasurementProvider> Providers => providers;

        public static void Register(IImageMeasurementProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (!providers.Any(item => item.GetType() == provider.GetType()))
            {
                providers.Add(provider);
            }
        }

        public static ImageMeasurement Read(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            IImageMeasurementProvider? provider = providers.FirstOrDefault(item => item.CanRead(filePath));
            if (provider == null)
            {
                throw new NotSupportedException("当前文件类型没有可用的图像测量读取器");
            }

            return provider.Read(filePath);
        }
    }

    public sealed class CvcieImageMeasurementProvider : IImageMeasurementProvider
    {
        public string Name => "CVCIE XYZ";

        public bool CanRead(string filePath)
        {
            return File.Exists(filePath)
                && string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase)
                && CVFileUtil.IsCVCIEFile(filePath);
        }

        public ImageMeasurement Read(string filePath)
        {
            if (!CanRead(filePath))
            {
                throw new NotSupportedException("请选择 CVCIE XYZ 图像文件");
            }

            CVFileUtil.Read(filePath, out CVCIEFile fileInfo);
            if (fileInfo.Channels < 3)
            {
                throw new NotSupportedException($"CVCIE 文件通道数不足: {fileInfo.Channels}");
            }

            int bytesPerPixel = fileInfo.Bpp / 8;
            int channelSize = fileInfo.Cols * fileInfo.Rows * bytesPerPixel;
            if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
            {
                throw new InvalidDataException("CVCIE 文件数据长度不足，无法拆分 XYZ 通道");
            }

            using OpenCvSharp.Mat xMat = CreateFloatChannelMat(fileInfo.Data, 0, channelSize, fileInfo.Rows, fileInfo.Cols, GetSingleChannelMatType(fileInfo.Bpp));
            using OpenCvSharp.Mat yMat = CreateFloatChannelMat(fileInfo.Data, channelSize, channelSize, fileInfo.Rows, fileInfo.Cols, GetSingleChannelMatType(fileInfo.Bpp));
            using OpenCvSharp.Mat zMat = CreateFloatChannelMat(fileInfo.Data, channelSize * 2, channelSize, fileInfo.Rows, fileInfo.Cols, GetSingleChannelMatType(fileInfo.Bpp));

            double meanX = OpenCvSharp.Cv2.Mean(xMat).Val0;
            double meanY = OpenCvSharp.Cv2.Mean(yMat).Val0;
            double meanZ = OpenCvSharp.Cv2.Mean(zMat).Val0;
            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(meanX, meanY, meanZ);

            return new ImageMeasurement(filePath, meanX, meanY, meanZ, chromaticity);
        }

        private static OpenCvSharp.MatType GetSingleChannelMatType(int bpp)
        {
            return bpp switch
            {
                8 => OpenCvSharp.MatType.CV_8UC1,
                16 => OpenCvSharp.MatType.CV_16UC1,
                32 => OpenCvSharp.MatType.CV_32FC1,
                64 => OpenCvSharp.MatType.CV_64FC1,
                _ => throw new NotSupportedException($"Bpp {bpp} not supported")
            };
        }

        private static OpenCvSharp.Mat CreateFloatChannelMat(byte[] source, int offset, int channelSize, int rows, int cols, OpenCvSharp.MatType sourceType)
        {
            byte[] channelData = new byte[channelSize];
            Buffer.BlockCopy(source, offset, channelData, 0, channelSize);

            using OpenCvSharp.Mat raw = OpenCvSharp.Mat.FromPixelData(rows, cols, sourceType, channelData);
            OpenCvSharp.Mat copied = raw.Clone();
            if (copied.Type() == OpenCvSharp.MatType.CV_32FC1)
            {
                return copied;
            }

            OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
            copied.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
            copied.Dispose();
            return floatMat;
        }
    }

    public sealed record ContrastResult(ImageMeasurement Black, ImageMeasurement White, double Ratio)
    {
        public string RatioText => double.IsFinite(Ratio) ? $"{Ratio:F3}:1" : "无效";
    }

    public interface IContrastCalculator
    {
        ContrastResult Calculate(ImageMeasurement black, ImageMeasurement white);
    }

    public sealed class DefaultContrastCalculator : IContrastCalculator
    {
        public ContrastResult Calculate(ImageMeasurement black, ImageMeasurement white)
        {
            if (black.Luminance <= 0)
            {
                throw new InvalidOperationException("黑场亮度必须大于 0，才能计算对比度");
            }

            return new ContrastResult(black, white, white.Luminance / black.Luminance);
        }
    }

    public readonly record struct ChromaticityPoint(double X, double Y)
    {
        public string Text => $"({X:F4}, {Y:F4})";
    }

    public sealed record ColorGamutStandard(string Name, ChromaticityPoint Red, ChromaticityPoint Green, ChromaticityPoint Blue)
    {
        public override string ToString() => Name;
    }

    public sealed record ColorGamutResult(
        ImageMeasurement Red,
        ImageMeasurement Green,
        ImageMeasurement Blue,
        ColorGamutStandard Standard,
        double SampleArea,
        double StandardArea,
        double CoveragePercent);

    public interface IColorGamutCalculator
    {
        ColorGamutResult Calculate(ImageMeasurement red, ImageMeasurement green, ImageMeasurement blue, ColorGamutStandard standard);
    }

    public sealed class DefaultColorGamutCalculator : IColorGamutCalculator
    {
        public ColorGamutResult Calculate(ImageMeasurement red, ImageMeasurement green, ImageMeasurement blue, ColorGamutStandard standard)
        {
            ArgumentNullException.ThrowIfNull(standard);

            double sampleArea = TriangleArea(ToPoint(red), ToPoint(green), ToPoint(blue));
            double standardArea = TriangleArea(standard.Red, standard.Green, standard.Blue);
            if (standardArea <= 0)
            {
                throw new InvalidOperationException($"{standard.Name} 标准色域面积无效");
            }

            return new ColorGamutResult(red, green, blue, standard, sampleArea, standardArea, sampleArea / standardArea * 100.0);
        }

        private static ChromaticityPoint ToPoint(ImageMeasurement measurement)
        {
            return new ChromaticityPoint(measurement.Chromaticity.x, measurement.Chromaticity.y);
        }

        private static double TriangleArea(ChromaticityPoint red, ChromaticityPoint green, ChromaticityPoint blue)
        {
            return Math.Abs((red.X * (green.Y - blue.Y) + green.X * (blue.Y - red.Y) + blue.X * (red.Y - green.Y)) / 2.0);
        }
    }

    public static class ColorGamutStandards
    {
        public static IReadOnlyList<ColorGamutStandard> All { get; } = new[]
        {
            new ColorGamutStandard("NTSC", new ChromaticityPoint(0.670, 0.330), new ChromaticityPoint(0.210, 0.710), new ChromaticityPoint(0.140, 0.080)),
            new ColorGamutStandard("sRGB", new ChromaticityPoint(0.640, 0.330), new ChromaticityPoint(0.300, 0.600), new ChromaticityPoint(0.150, 0.060)),
            new ColorGamutStandard("DCI-P3", new ChromaticityPoint(0.680, 0.320), new ChromaticityPoint(0.265, 0.690), new ChromaticityPoint(0.150, 0.060)),
            new ColorGamutStandard("PAL", new ChromaticityPoint(0.640, 0.330), new ChromaticityPoint(0.290, 0.600), new ChromaticityPoint(0.150, 0.060)),
            new ColorGamutStandard("Adobe RGB", new ChromaticityPoint(0.640, 0.330), new ChromaticityPoint(0.210, 0.710), new ChromaticityPoint(0.150, 0.060))
        };
    }
}