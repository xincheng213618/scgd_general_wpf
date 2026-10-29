using ColorVision.ImageEditor.Algorithms;
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
    public void ThresholdMatchesExplicitSourceClone(string format)
    {
        const double threshold = 83;
        VerifyAgainstExplicitSourceClone(
            "阈值处理",
            format,
            options => SetOption(options, "Threshold", threshold),
            (source, destination) => Cv2.Threshold(
                source,
                destination,
                source.Depth() == MatType.CV_16U ? threshold * 257 : threshold,
                source.Depth() == MatType.CV_16U ? ushort.MaxValue : byte.MaxValue,
                ThresholdTypes.Binary));
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
        double nominalMaximum = source.Depth() == MatType.CV_16U ? ushort.MaxValue : source.Depth() == MatType.CV_32F ? 1 : byte.MaxValue;
        ApplyReferenceBilateral(expected, diameter, sigmaColor / byte.MaxValue * nominalMaximum, sigmaSpace);

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
            (source, destination) => BasicAdjustmentReference.Apply(
                source,
                destination,
                exposure,
                brightness,
                contrast,
                gamma));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void BasicAdjustmentNeutralIntegersPreserveEveryValueAndStorage(int bits)
    {
        int side = bits == 8 ? 16 : 256;
        ushort[,] values = new ushort[side, side];
        for (int row = 0; row < side; row++)
        {
            for (int column = 0; column < side; column++) values[row, column] = (ushort)(row * side + column);
        }
        using Mat input = Mat.FromArray(values);
        using Mat actual = new();
        input.ConvertTo(actual, bits == 8 ? MatType.CV_8U : MatType.CV_16U);
        using Mat original = actual.Clone();
        using Mat expected = actual.Clone();
        BasicAdjustmentReference.Apply(original, expected, 0, 0, 0, 1);
        IntPtr data = actual.Data;
        long step = actual.Step();

        OpenCvImageAlgorithms.AdjustBasic(actual, 0, 0, 0, 1);

        AssertMatsEqual(original, expected);
        AssertMatsEqual(expected, actual);
        Assert.Equal(data, actual.Data);
        Assert.Equal(step, actual.Step());
    }

    [Fact]
    public void BasicAdjustmentEquivalentNeutralParametersPreserveBgra64RoiAndItsOwner()
    {
        using Mat owner = new(7, 9, MatType.CV_16UC4, Scalar.All(1234));
        using Mat roi = new(owner, new Rect(1, 1, 5, 4));
        int rows = roi.Rows;
        int columns = roi.Cols;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int value = row * columns + column;
                roi.Set(row, column, new Vec4w((ushort)(value * 3079), (ushort)(value * 2003 + 17), (ushort)(value * 1237 + 31), (ushort)(ushort.MaxValue - value * 2027)));
            }
        }
        using Mat originalOwner = owner.Clone();
        using Mat original = roi.Clone();
        using Mat expected = roi.Clone();
        BasicAdjustmentReference.Apply(original, expected, exposure: 1, brightness: -50, contrast: -50, gamma: 1);
        IntPtr data = roi.Data;
        long step = roi.Step();
        Assert.False(roi.IsContinuous());

        OpenCvImageAlgorithms.AdjustBasic(roi, exposure: 1, brightness: -50, contrast: -50, gamma: 1);

        using Mat actual = roi.Clone();
        AssertMatsEqual(expected, actual);
        AssertMatsEqual(originalOwner, owner);
        AssertAlphaEqual(original, actual);
        Assert.Equal(data, roi.Data);
        Assert.Equal(step, roi.Step());
        Assert.False(roi.IsContinuous());
    }

    [Fact]
    public void BasicAdjustmentNonNeutralBgra64RoiKeepsItsStorageAlphaAndOwnerBorder()
    {
        Rect region = new(1, 1, 5, 4);
        using Mat owner = new(7, 9, MatType.CV_16UC4, Scalar.All(1234));
        using Mat roi = new(owner, region);
        for (int row = 0; row < region.Height; row++)
        {
            for (int column = 0; column < region.Width; column++)
            {
                int value = row * region.Width + column;
                roi.Set(row, column, new Vec4w((ushort)(value * 3079), (ushort)(value * 2003 + 17), (ushort)(value * 1237 + 31), (ushort)(ushort.MaxValue - value * 2027)));
            }
        }
        using Mat original = roi.Clone();
        using Mat expectedOwner = owner.Clone();
        using (Mat expectedRoi = new(expectedOwner, region))
        {
            BasicAdjustmentReference.Apply(original, expectedRoi, 0.65, -15, 28, 1.8);
        }
        IntPtr data = roi.Data;
        long step = roi.Step();
        Assert.False(roi.IsContinuous());

        OpenCvImageAlgorithms.AdjustBasic(roi, 0.65, -15, 28, 1.8);

        using Mat actual = roi.Clone();
        AssertMatsEqual(expectedOwner, owner);
        AssertAlphaEqual(original, actual);
        Assert.Equal(data, roi.Data);
        Assert.Equal(step, roi.Step());
        Assert.False(roi.IsContinuous());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BasicAdjustmentNonNeutralFloatingPointMatchesSeparatePassOracleExactly(bool useDouble)
    {
        using Mat image = useDouble
            ? Mat.FromArray(new double[,] { { -0.5, 0, 1e-7, 0.001, 0.1, 0.25, 0.49999, 0.5, 0.9, 1, 1.5 } })
            : Mat.FromArray(new float[,] { { -0.5f, 0, 1e-7f, 0.001f, 0.1f, 0.25f, 0.49999f, 0.5f, 0.9f, 1, 1.5f } });
        using Mat expected = image.Clone();
        BasicAdjustmentReference.Apply(image, expected, 0.65, -15, 28, 1.8);
        IntPtr data = image.Data;
        long step = image.Step();

        OpenCvImageAlgorithms.AdjustBasic(image, 0.65, -15, 28, 1.8);

        AssertMatsEqual(expected, image);
        Assert.Equal(data, image.Data);
        Assert.Equal(step, image.Step());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BasicAdjustmentDefaultsStillClampFloatingPointImages(bool useDouble)
    {
        using Mat image = useDouble
            ? Mat.FromArray(new double[,] { { -0.5, 0.25, 1.5 } })
            : Mat.FromArray(new float[,] { { -0.5f, 0.25f, 1.5f } });

        OpenCvImageAlgorithms.AdjustBasic(image, 0, 0, 0, 1);

        double[] expected = [0, 0.25, 1];
        for (int column = 0; column < expected.Length; column++)
        {
            double actual = useDouble ? image.At<double>(0, column) : image.At<float>(0, column);
            Assert.Equal(expected[column], actual);
        }
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(5d)]
    public void BasicAdjustmentRejectsPreCancelledTokenWithoutChangingPixelsOrStorage(double brightness)
    {
        using Mat image = Mat.FromArray(new ushort[,] { { 0, 1, 32767 }, { 32768, 65534, 65535 } });
        using Mat original = image.Clone();
        IntPtr data = image.Data;
        long step = image.Step();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            OpenCvImageAlgorithms.AdjustBasic(image, 0, brightness, 0, 1, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        AssertMatsEqual(original, image);
        Assert.Equal(data, image.Data);
        Assert.Equal(step, image.Step());
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
