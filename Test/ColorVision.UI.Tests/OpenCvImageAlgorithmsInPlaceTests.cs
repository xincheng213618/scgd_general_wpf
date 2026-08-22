using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public class OpenCvImageAlgorithmsInPlaceTests
{
    public static IEnumerable<object[]> MorphologyCases()
    {
        string[] formats = ["Gray8", "Gray16", "Bgr24", "Bgra32"];
        for (int operation = 0; operation < 7; operation++)
        {
            foreach (string format in formats)
            {
                yield return [operation, format];
            }
        }
    }

    [Theory]
    [InlineData("Gray8")]
    [InlineData("Gray16")]
    [InlineData("Bgr24")]
    [InlineData("Bgra32")]
    public void GaussianBlurMatchesExplicitSourceClone(string format)
    {
        VerifyAgainstExplicitSourceClone(
            "高斯模糊",
            format,
            options =>
            {
                SetOption(options, "KernelSize", 4);
                SetOption(options, "Sigma", 1.25d);
            },
            (source, destination) => Cv2.GaussianBlur(source, destination, new Size(5, 5), 1.25d));
    }

    [Theory]
    [InlineData("Gray8")]
    [InlineData("Gray16")]
    [InlineData("Bgr24")]
    [InlineData("Bgra32")]
    public void MedianBlurMatchesExplicitSourceClone(string format)
    {
        VerifyAgainstExplicitSourceClone(
            "中值滤波",
            format,
            options => SetOption(options, "KernelSize", 4),
            (source, destination) => Cv2.MedianBlur(source, destination, 5));
    }

    [Theory]
    [InlineData("Gray8")]
    [InlineData("Gray16")]
    [InlineData("Bgr24")]
    [InlineData("Bgra32")]
    public void SharpenMatchesExplicitSourceClone(string format)
    {
        using Mat kernel = Mat.FromArray(new float[,]
        {
            { 0, -1, 0 },
            { -1, 5, -1 },
            { 0, -1, 0 },
        });

        VerifyAgainstExplicitSourceClone(
            "锐化",
            format,
            configure: null,
            (source, destination) => Cv2.Filter2D(source, destination, source.Depth(), kernel));
    }

    [Theory]
    [InlineData("Gray8")]
    [InlineData("Gray16")]
    [InlineData("Bgr24")]
    [InlineData("Bgra32")]
    public void MeanBlurMatchesExplicitSourceClone(string format)
    {
        VerifyAgainstExplicitSourceClone(
            "降噪滤波",
            format,
            options =>
            {
                SetEnumOption(options, "Operation", 1);
                SetOption(options, "KernelSize", 4);
            },
            (source, destination) => Cv2.Blur(source, destination, new Size(5, 5)));
    }

    [Theory]
    [InlineData("CV_8UC4")]
    [InlineData("CV_32FC4")]
    [InlineData("CV_16UC4")]
    public void BilateralFourChannelMatchesSplitMergeReference(string format)
    {
        const int diameter = 5;
        const double sigmaColor = 31.5;
        const double sigmaSpace = 7.25;
        using Mat source = CreateFourChannelSource(format);
        using Mat original = source.Clone();
        using Mat expected = source.Clone();
        ApplyReferenceBilateral(expected, diameter, sigmaColor, sigmaSpace);

        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == "降噪滤波");
        SetEnumOption(algorithm.Options, "Operation", 0);
        SetOption(algorithm.Options, "KernelSize", diameter);
        SetOption(algorithm.Options, "SigmaColor", sigmaColor);
        SetOption(algorithm.Options, "SigmaSpace", sigmaSpace);

        using Mat actual = algorithm.Apply(source);

        AssertMatsEqual(expected, actual);
        AssertMatsEqual(original, source);
        AssertAlphaEqual(original, actual);
    }

    [Theory]
    [MemberData(nameof(MorphologyCases))]
    public void MorphologyMatchesExplicitSourceClone(int operation, string format)
    {
        VerifyAgainstExplicitSourceClone(
            "形态学操作",
            format,
            options =>
            {
                SetEnumOption(options, "Operation", operation);
                SetOption(options, "KernelSize", 4);
                SetOption(options, "Iterations", 2);
            },
            (source, destination) => ApplyReferenceMorphology(source, destination, operation));
    }

    [Theory]
    [InlineData("Gray16")]
    [InlineData("Bgra32")]
    public void BasicAdjustmentMatchesExplicitSourceClone(string format)
    {
        const double exposure = 0.65;
        const double brightness = -15;
        const double contrast = 28;
        const double gamma = 1.8;

        VerifyAgainstExplicitSourceClone(
            "基础调整",
            format,
            options =>
            {
                SetOption(options, "Exposure", exposure);
                SetOption(options, "Brightness", brightness);
                SetOption(options, "Contrast", contrast);
                SetOption(options, "Gamma", gamma);
            },
            (source, destination) => ApplyReferenceBasicAdjustment(
                source,
                destination,
                exposure,
                brightness,
                contrast,
                gamma));
    }

    private static void VerifyAgainstExplicitSourceClone(
        string algorithmName,
        string format,
        Action<object>? configure,
        Action<Mat, Mat> referenceOperation)
    {
        using Mat source = CreateSource(format);
        using Mat original = source.Clone();
        using Mat expected = source.Clone();
        using (Mat referenceSource = expected.Clone())
        {
            referenceOperation(referenceSource, expected);
        }

        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == algorithmName);
        configure?.Invoke(algorithm.Options);

        using Mat actual = algorithm.Apply(source);

        AssertMatsEqual(expected, actual);
        AssertMatsEqual(original, source);
    }

    private static Mat CreateSource(string format)
    {
        Mat source;
        double upperBound;
        switch (format)
        {
            case "Gray8":
                source = new Mat(19, 23, MatType.CV_8UC1);
                upperBound = byte.MaxValue;
                break;
            case "Gray16":
                source = new Mat(19, 23, MatType.CV_16UC1);
                upperBound = ushort.MaxValue;
                break;
            case "Bgr24":
                source = new Mat(19, 23, MatType.CV_8UC3);
                upperBound = byte.MaxValue;
                break;
            case "Bgra32":
                source = new Mat(19, 23, MatType.CV_8UC4);
                upperBound = byte.MaxValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        Cv2.Randu(source, Scalar.All(0), Scalar.All(upperBound));
        return source;
    }

    private static Mat CreateFourChannelSource(string format)
    {
        Mat source;
        double upperBound;
        switch (format)
        {
            case "CV_8UC4":
                source = new Mat(19, 23, MatType.CV_8UC4);
                upperBound = byte.MaxValue + 1d;
                break;
            case "CV_32FC4":
                source = new Mat(19, 23, MatType.CV_32FC4);
                upperBound = 1;
                break;
            case "CV_16UC4":
                source = new Mat(19, 23, MatType.CV_16UC4);
                upperBound = ushort.MaxValue + 1d;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        Cv2.Randu(source, Scalar.All(0), Scalar.All(upperBound));
        return source;
    }

    private static void ApplyReferenceBilateral(Mat mat, int diameter, double sigmaColor, double sigmaSpace)
    {
        if (mat.Depth() != MatType.CV_8U && mat.Depth() != MatType.CV_32F)
        {
            using Mat converted = new();
            mat.ConvertTo(converted, MatType.MakeType(MatType.CV_32F, mat.Channels()));
            ApplyReferenceBilateral(converted, diameter, sigmaColor, sigmaSpace);
            converted.ConvertTo(mat, mat.Type());
            return;
        }

        Mat[] channels = Cv2.Split(mat);
        try
        {
            using Mat color = new();
            using Mat filteredColor = new();
            Cv2.Merge(channels.Take(3).ToArray(), color);
            Cv2.BilateralFilter(color, filteredColor, diameter, sigmaColor, sigmaSpace);

            Mat[] filteredChannels = Cv2.Split(filteredColor);
            try
            {
                Cv2.Merge([filteredChannels[0], filteredChannels[1], filteredChannels[2], channels[3]], mat);
            }
            finally
            {
                foreach (Mat channel in filteredChannels)
                {
                    channel.Dispose();
                }
            }
        }
        finally
        {
            foreach (Mat channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static void AssertAlphaEqual(Mat expected, Mat actual)
    {
        using Mat expectedAlpha = new();
        using Mat actualAlpha = new();
        Cv2.ExtractChannel(expected, expectedAlpha, 3);
        Cv2.ExtractChannel(actual, actualAlpha, 3);
        AssertMatsEqual(expectedAlpha, actualAlpha);
    }

    private static void ApplyReferenceMorphology(Mat source, Mat destination, int operation)
    {
        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        switch (operation)
        {
            case 0:
                Cv2.Erode(source, destination, kernel, iterations: 2);
                break;
            case 1:
                Cv2.Dilate(source, destination, kernel, iterations: 2);
                break;
            case 2:
                Cv2.MorphologyEx(source, destination, MorphTypes.Open, kernel, iterations: 2);
                break;
            case 3:
                Cv2.MorphologyEx(source, destination, MorphTypes.Close, kernel, iterations: 2);
                break;
            case 4:
                Cv2.MorphologyEx(source, destination, MorphTypes.Gradient, kernel, iterations: 2);
                break;
            case 5:
                Cv2.MorphologyEx(source, destination, MorphTypes.TopHat, kernel, iterations: 2);
                break;
            case 6:
                Cv2.MorphologyEx(source, destination, MorphTypes.BlackHat, kernel, iterations: 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static void ApplyReferenceBasicAdjustment(
        Mat source,
        Mat destination,
        double exposure,
        double brightness,
        double contrast,
        double gamma)
    {
        double maximum = source.Depth() == MatType.CV_16U ? ushort.MaxValue : byte.MaxValue;
        double exposureGain = Math.Pow(2, Math.Clamp(exposure, -5, 5));
        double brightnessOffset = Math.Clamp(brightness, -100, 100) / 100;
        double contrastGain = 1 + Math.Clamp(contrast, -100, 100) / 100;
        double alpha = exposureGain * contrastGain;
        double beta = brightnessOffset * contrastGain + 0.5 * (1 - contrastGain);
        double gammaValue = Math.Clamp(gamma, 0.1, 5);

        if (source.Channels() != 4)
        {
            ApplyReferenceToneAdjustment(source, destination, alpha, beta, gammaValue, maximum);
            return;
        }

        Mat[] channels = Cv2.Split(source);
        try
        {
            for (int index = 0; index < 3; index++)
            {
                ApplyReferenceToneAdjustment(channels[index], channels[index], alpha, beta, gammaValue, maximum);
            }

            Cv2.Merge(channels, destination);
        }
        finally
        {
            foreach (Mat channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static void ApplyReferenceToneAdjustment(
        Mat mat,
        Mat destination,
        double alpha,
        double beta,
        double gamma,
        double maximum)
    {
        using Mat source = mat.Clone();
        MatType workingType = MatType.MakeType(MatType.CV_32F, mat.Channels());
        using Mat normalized = new();
        source.ConvertTo(normalized, workingType, 1 / maximum);
        normalized.ConvertTo(normalized, workingType, alpha, beta);
        Cv2.Max(normalized, 0, normalized);
        Cv2.Pow(normalized, 1 / gamma, normalized);
        Cv2.Min(normalized, 1, normalized);
        normalized.ConvertTo(destination, mat.Type(), maximum);
    }

    private static void SetOption(object options, string propertyName, object value)
    {
        PropertyInfo property = options.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Missing option property: {propertyName}");
        property.SetValue(options, value);
    }

    private static void SetEnumOption(object options, string propertyName, int value)
    {
        PropertyInfo property = options.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Missing option property: {propertyName}");
        property.SetValue(options, Enum.ToObject(property.PropertyType, value));
    }

    private static void AssertMatsEqual(Mat expected, Mat actual)
    {
        Assert.Equal(expected.Type(), actual.Type());
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.Cols, actual.Cols);
        Assert.True(expected.IsContinuous());
        Assert.True(actual.IsContinuous());

        int length = checked((int)(expected.Total() * expected.ElemSize()));
        byte[] expectedBytes = new byte[length];
        byte[] actualBytes = new byte[length];
        Marshal.Copy(expected.Data, expectedBytes, 0, length);
        Marshal.Copy(actual.Data, actualBytes, 0, length);
        Assert.Equal(expectedBytes, actualBytes);
    }
}
