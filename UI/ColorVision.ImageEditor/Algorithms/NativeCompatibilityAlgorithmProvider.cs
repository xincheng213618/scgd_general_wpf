using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    public sealed class NativeCompatibilityAlgorithmProvider : IImageAlgorithmProvider
    {
        private static readonly IReadOnlySet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.native.compatibility",
            "ColorVision Native Compatibility",
            AlgorithmProviderKind.Native,
            AlgorithmExecutionPlane.Local,
            90,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            Formats);

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.RemoveMoire && inputs.Count == 1;
            reason = supported ? null : "algorithm_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmInput input = context.Inputs[0];
            using HImage source = CreateOwnedHImage(input.Image);
            int code = OpenCVMediaHelper.M_RemoveMoire(source, out HImage output);
            if (code != 0)
            {
                output.Dispose();
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Failed,
                    Failures = new[] { new AlgorithmFailure("native_error", $"Native RemoveMoire returned {code}.") },
                });
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                AlgorithmImageBuffer resultImage = ImageAlgorithmInputFactory.Copy(
                    output,
                    input.Image.Format,
                    input.Image.DpiX,
                    input.Image.DpiY);
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = new AlgorithmArtifact[] { new AlgorithmImageArtifact("image", "primary", resultImage) },
                });
            }
            finally
            {
                output.Dispose();
            }
        }

        private static HImage CreateOwnedHImage(AlgorithmImageBuffer image)
        {
            byte[] bytes = image.Data.ToArray();
            IntPtr pointer = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return new HImage
            {
                rows = image.Height,
                cols = image.Width,
                channels = image.Format.Channels(),
                depth = image.Format.BitsPerChannel(),
                stride = image.Stride,
                isDispose = false,
                pData = pointer,
            };
        }

    }
}
