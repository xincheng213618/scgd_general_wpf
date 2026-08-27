using ColorVision.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Algorithms
{
    /// <summary>
    /// Executes local pixel algorithms against the process-local camera frame plane.
    /// The legacy remote MQTT/device <c>AlgorithmNode</c> remains a separate execution plane.
    /// </summary>
    public static class LocalFlowImageAlgorithmAdapter
    {
        public static ValueTask<AlgorithmResult> ExecuteRawAsync(
            LocalFlowFrameLease frame,
            AlgorithmInvocation invocation,
            IProgress<AlgorithmProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(invocation);
            cancellationToken.ThrowIfCancellationRequested();

            LocalFrameMetadata metadata = frame.Metadata;
            AlgorithmImageFormat format = ResolveRawFormat(metadata.SourceBpp, metadata.Channels);
            int stride = checked(metadata.Width * format.BytesPerPixel());
            int requiredLength = checked(stride * metadata.Height);
            byte[] raw = frame.CopyRawToArray();
            if (raw.Length < requiredLength)
            {
                throw new InvalidOperationException($"RAW frame {frame.FrameId:N} contains {raw.Length} bytes; {requiredLength} are required by its metadata.");
            }
            if (raw.Length != requiredLength) Array.Resize(ref raw, requiredLength);

            AlgorithmImageBuffer image = new(metadata.Width, metadata.Height, stride, format, raw);
            return ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = invocation,
                Inputs = new[]
                {
                    new AlgorithmInput
                    {
                        Name = "source",
                        Image = image,
                        Ownership = AlgorithmInputOwnership.Transferred,
                        SourceRevision = frame.FrameId.ToString("N", CultureInfo.InvariantCulture),
                        SourceUri = string.IsNullOrWhiteSpace(metadata.SourceFilePath) ? null : metadata.SourceFilePath,
                    },
                },
                RequiredCapabilities = AlgorithmHostCapabilities.Flow
                    | AlgorithmHostCapabilities.Headless
                    | AlgorithmHostCapabilities.Local,
                Progress = progress,
            }, cancellationToken);
        }

        public static AlgorithmImageFormat ResolveRawFormat(int bitsPerChannel, int channels) => (bitsPerChannel, channels) switch
        {
            (8, 1) => AlgorithmImageFormat.Gray8,
            (16, 1) => AlgorithmImageFormat.Gray16,
            (8, 3) => AlgorithmImageFormat.Bgr24,
            (16, 3) => AlgorithmImageFormat.Bgr48,
            (8, 4) => AlgorithmImageFormat.Bgra32,
            (16, 4) => AlgorithmImageFormat.Bgra64,
            _ => throw new NotSupportedException($"Local Flow pixel algorithms do not support {bitsPerChannel}-bit, {channels}-channel RAW frames."),
        };
    }
}
