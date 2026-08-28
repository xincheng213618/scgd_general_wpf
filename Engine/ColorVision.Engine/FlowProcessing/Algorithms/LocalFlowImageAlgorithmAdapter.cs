using ColorVision.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            => ExecuteRawAsync(ImageAlgorithmPlatform.Runtime, frame, invocation, progress, cancellationToken);

        public static ValueTask<AlgorithmResult> ExecuteRawAsync(
            AlgorithmRuntime runtime,
            LocalFlowFrameLease frame,
            AlgorithmInvocation invocation,
            IProgress<AlgorithmProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(invocation);
            cancellationToken.ThrowIfCancellationRequested();

            AlgorithmInput input = CreateRawInput(frame, "source");
            return runtime.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = invocation,
                Inputs = [input],
                RequiredCapabilities = AlgorithmHostCapabilities.Flow
                    | AlgorithmHostCapabilities.Headless
                    | AlgorithmHostCapabilities.Local,
                Progress = progress,
            }, cancellationToken);
        }

        /// <summary>Explicit two-frame local execution path for multi-input algorithms such as image registration.</summary>
        public static ValueTask<AlgorithmResult> ExecuteRawPairAsync(
            LocalFlowFrameLease reference,
            LocalFlowFrameLease moving,
            AlgorithmInvocation invocation,
            IProgress<AlgorithmProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => ExecuteRawPairAsync(ImageAlgorithmPlatform.Runtime, reference, moving, invocation, progress, cancellationToken);

        public static ValueTask<AlgorithmResult> ExecuteRawPairAsync(
            AlgorithmRuntime runtime,
            LocalFlowFrameLease reference,
            LocalFlowFrameLease moving,
            AlgorithmInvocation invocation,
            IProgress<AlgorithmProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(reference);
            ArgumentNullException.ThrowIfNull(moving);
            ArgumentNullException.ThrowIfNull(invocation);
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmInput? referenceInput = null;
            AlgorithmInput? movingInput = null;
            try
            {
                referenceInput = CreateRawInput(reference, "reference");
                movingInput = CreateRawInput(moving, "moving");
                return runtime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [referenceInput, movingInput],
                    RequiredCapabilities = AlgorithmHostCapabilities.Flow
                        | AlgorithmHostCapabilities.Headless
                        | AlgorithmHostCapabilities.Local
                        | AlgorithmHostCapabilities.MultiInput,
                    Progress = progress,
                }, cancellationToken);
            }
            catch
            {
                referenceInput?.Image.Dispose();
                movingInput?.Image.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Explicit named multi-frame local path for calibrated algorithms. The dictionary must
        /// contain a <c>source</c> lease; remote MQTT/device algorithms remain on their legacy plane.
        /// </summary>
        public static ValueTask<AlgorithmResult> ExecuteRawSetAsync(
            IReadOnlyDictionary<string, LocalFlowFrameLease> frames,
            AlgorithmInvocation invocation,
            IProgress<AlgorithmProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => ExecuteRawSetAsync(ImageAlgorithmPlatform.Runtime, frames, invocation, progress, cancellationToken);

        public static ValueTask<AlgorithmResult> ExecuteRawSetAsync(
            AlgorithmRuntime runtime,
            IReadOnlyDictionary<string, LocalFlowFrameLease> frames,
            AlgorithmInvocation invocation,
            IProgress<AlgorithmProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(frames);
            ArgumentNullException.ThrowIfNull(invocation);
            cancellationToken.ThrowIfCancellationRequested();
            if (!frames.ContainsKey("source")) throw new ArgumentException("A named 'source' frame is required.", nameof(frames));
            if (frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
            List<AlgorithmInput> inputs = new(frames.Count);
            try
            {
                foreach ((string name, LocalFlowFrameLease frame) in frames.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(name);
                    ArgumentNullException.ThrowIfNull(frame);
                    inputs.Add(CreateRawInput(frame, name));
                }
                return runtime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = inputs,
                    RequiredCapabilities = AlgorithmHostCapabilities.Flow
                        | AlgorithmHostCapabilities.Headless
                        | AlgorithmHostCapabilities.Local
                        | (inputs.Count > 1 ? AlgorithmHostCapabilities.MultiInput : AlgorithmHostCapabilities.None),
                    Progress = progress,
                }, cancellationToken);
            }
            catch
            {
                foreach (AlgorithmInput input in inputs) input.Image.Dispose();
                throw;
            }
        }

        private static AlgorithmInput CreateRawInput(LocalFlowFrameLease frame, string name)
        {
            LocalFrameMetadata metadata = frame.Metadata;
            AlgorithmImageFormat format = ResolveRawFormat(metadata.SourceBpp, metadata.Channels);
            int stride = checked(metadata.Width * format.BytesPerPixel());
            int requiredLength = checked(stride * metadata.Height);
            byte[] raw = frame.CopyRawToArray();
            if (raw.Length < requiredLength)
                throw new InvalidOperationException($"RAW frame {frame.FrameId:N} contains {raw.Length} bytes; {requiredLength} are required by its metadata.");
            if (raw.Length != requiredLength) Array.Resize(ref raw, requiredLength);
            return new AlgorithmInput
            {
                Name = name,
                Image = new AlgorithmImageBuffer(metadata.Width, metadata.Height, stride, format, raw),
                Ownership = AlgorithmInputOwnership.Transferred,
                SourceRevision = frame.FrameId.ToString("N", CultureInfo.InvariantCulture),
                SourceUri = string.IsNullOrWhiteSpace(metadata.SourceFilePath) ? null : metadata.SourceFilePath,
                ColorSpace = "encoded-device-values",
            };
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
