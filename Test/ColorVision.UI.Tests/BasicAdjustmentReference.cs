using OpenCvSharp;

namespace ColorVision.UI.Tests;

// Test-only oracle: keep the original separate OpenCV passes independent of the production path.
internal static class BasicAdjustmentReference
{
    public static void Apply(Mat source, Mat destination, double exposure, double brightness, double contrast, double gamma)
    {
        MatType depth = source.Depth();
        double maximum = depth == MatType.CV_8U ? byte.MaxValue
            : depth == MatType.CV_16U ? ushort.MaxValue
            : depth == MatType.CV_32F || depth == MatType.CV_64F ? 1
            : throw new NotSupportedException($"Unsupported basic-adjustment oracle depth: {depth}.");
        double exposureGain = Math.Pow(2, Math.Clamp(exposure, -5, 5));
        double brightnessOffset = Math.Clamp(brightness, -100, 100) / 100;
        double contrastGain = 1 + Math.Clamp(contrast, -100, 100) / 100;
        double alpha = exposureGain * contrastGain;
        double beta = brightnessOffset * contrastGain + 0.5 * (1 - contrastGain);
        double gammaValue = Math.Clamp(gamma, 0.1, 5);

        if (source.Channels() != 4)
        {
            ApplyTone(source, destination, alpha, beta, gammaValue, maximum);
            return;
        }

        Mat[] channels = Cv2.Split(source);
        try
        {
            for (int index = 0; index < 3; index++)
            {
                ApplyTone(channels[index], channels[index], alpha, beta, gammaValue, maximum);
            }

            Cv2.Merge(channels, destination);
        }
        finally
        {
            foreach (Mat channel in channels) channel.Dispose();
        }
    }

    private static void ApplyTone(Mat mat, Mat destination, double alpha, double beta, double gamma, double maximum)
    {
        using Mat source = mat.Clone();
        MatType workingType = MatType.MakeType(mat.Depth() == MatType.CV_64F ? MatType.CV_64F : MatType.CV_32F, mat.Channels());
        using Mat normalized = new();
        source.ConvertTo(normalized, workingType, 1 / maximum);
        normalized.ConvertTo(normalized, workingType, alpha, beta);
        Cv2.Max(normalized, 0, normalized);
        Cv2.Pow(normalized, 1 / gamma, normalized);
        Cv2.Min(normalized, 1, normalized);
        normalized.ConvertTo(destination, mat.Type(), maximum);
    }
}
