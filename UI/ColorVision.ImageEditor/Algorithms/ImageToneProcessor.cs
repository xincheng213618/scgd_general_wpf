using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>Runs the standard tone algorithms on writable destinations owned by the caller.</summary>
public static class ImageToneProcessor
{
    public static void Adjust(Mat source, Mat destination, BasicAdjustmentParameters parameters, CancellationToken cancellationToken)
    {
        EnsureValid(parameters.Validate());
        OpenCvImageAlgorithms.AdjustBasic(source, destination, parameters.Exposure, parameters.Brightness,
            parameters.Contrast, parameters.Gamma, cancellationToken);
    }

    public static void ScaleRgb(Mat source, Mat destination, WhiteBalanceParameters parameters, CancellationToken cancellationToken)
    {
        EnsureValid(parameters.Validate());
        cancellationToken.ThrowIfCancellationRequested();
        destination.Create(source.Rows, source.Cols, source.Type());
        OpenCvAlgorithmProvider.ApplyWhiteBalance(source, destination, parameters, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void EnsureValid(AlgorithmValidationResult validation)
    {
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
    }
}
