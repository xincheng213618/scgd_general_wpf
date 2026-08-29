using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    internal static class ImageAlgorithmApplier
    {
        public static async Task<AlgorithmResult> ApplyAsync(
            ImageProcessingContext image,
            AlgorithmInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            using ImageAlgorithmPreviewSession session = ImageAlgorithmPreviewSession.Start(image);
            AlgorithmResult result = await session.PreviewAsync(
                invocation,
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                cancellationToken);
            AlgorithmPrimaryImageSelection primary = AlgorithmArtifactSelection.SelectPrimaryImage(result.Artifacts);
            if (result.Status == AlgorithmResultStatus.Succeeded
                && primary.Status != AlgorithmPrimaryImageSelectionStatus.Selected)
            {
                session.Cancel();
                return result;
            }
            if (result.Status == AlgorithmResultStatus.Succeeded && !session.Commit())
            {
                result.Dispose();
                return new AlgorithmResult
                {
                    InvocationId = invocation.InvocationId,
                    AlgorithmId = invocation.AlgorithmId,
                    AlgorithmVersion = invocation.AlgorithmVersion ?? default,
                    Status = AlgorithmResultStatus.Superseded,
                    Failures = new[] { new AlgorithmFailure("commit_superseded", "The document, source revision, or invocation changed before commit.") },
                };
            }
            if (result.Status != AlgorithmResultStatus.Succeeded) session.Cancel();
            return result;
        }

        public static async Task<AlgorithmResult> ApplyAsync(
            ImageProcessingContext image,
            AlgorithmInvocation invocation,
            IReadOnlyList<AlgorithmInput> additionalInputs,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(additionalInputs);
            ImageAlgorithmPreviewSession? session = null;
            try
            {
                session = ImageAlgorithmPreviewSession.Start(image);
                AlgorithmHostCapabilities required = AlgorithmHostCapabilities.Interactive
                    | AlgorithmHostCapabilities.Local
                    | AlgorithmHostCapabilities.MultiInput;
                AlgorithmResult result = await session.PreviewWithInputsAsync(invocation, additionalInputs, required, cancellationToken);
                AlgorithmPrimaryImageSelection primary = AlgorithmArtifactSelection.SelectPrimaryImage(result.Artifacts);
                if (result.Status == AlgorithmResultStatus.Succeeded
                    && primary.Status != AlgorithmPrimaryImageSelectionStatus.Selected)
                {
                    session.Cancel();
                    return result;
                }
                if (result.Status == AlgorithmResultStatus.Succeeded && !session.Commit())
                {
                    result.Dispose();
                    return new AlgorithmResult
                    {
                        InvocationId = invocation.InvocationId,
                        AlgorithmId = invocation.AlgorithmId,
                        AlgorithmVersion = invocation.AlgorithmVersion ?? default,
                        Status = AlgorithmResultStatus.Superseded,
                        Failures = new[] { new AlgorithmFailure("commit_superseded", "The document, source revision, or invocation changed before commit.") },
                    };
                }
                if (result.Status != AlgorithmResultStatus.Succeeded) session.Cancel();
                return result;
            }
            catch
            {
                foreach (AlgorithmInput input in additionalInputs)
                    if (input.Ownership == AlgorithmInputOwnership.Transferred) input.Image.Dispose();
                throw;
            }
            finally
            {
                session?.Dispose();
            }
        }

        // Compatibility façade for existing callers outside the migrated menu paths.
        public static void Apply(ImageProcessingContext image, Action<Mat> apply)
        {
            using ImageAlgorithmPreviewSession session = ImageAlgorithmPreviewSession.Start(image);
            try
            {
                session.Apply(apply);
                session.Commit();
            }
            catch
            {
                session.Cancel();
                throw;
            }
        }
    }
}
