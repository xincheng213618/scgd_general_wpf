using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System;
using System.IO;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Algorithms;

internal static class LocalFlowPngOutput
{
    public static string Save(Mat image, string outputDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Empty() || image.Type() != MatType.CV_8UC3)
            throw new ArgumentException("伪彩色 PNG 输出需要 Bgr24 图像。", nameof(image));

        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetFullPath(string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Results", "PseudoColor")
            : outputDirectory.Trim());
        string path = Path.Combine(directory, $"PseudoColor_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png");
        BatchImageOutput.Save(image, path, overwrite: false);
        return path;
    }
}
