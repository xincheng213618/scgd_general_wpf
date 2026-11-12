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
            return await ApplyAsync(session, invocation, Array.Empty<AlgorithmInput>(), cancellationToken);
        }

        public static async Task<AlgorithmResult> ApplyAsync(
            ImageAlgorithmPreviewSession session,
            AlgorithmInvocation invocation,
            IReadOnlyList<AlgorithmInput> additionalInputs,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(additionalInputs);
            try
            {
                ArgumentNullException.ThrowIfNull(session);
                // This one-shot UI path must not reclaim an operation superseded while
                // parameters or reference files were loading. Preview sessions elsewhere may reclaim.
                if (!session.OwnsHostPreview)
                {
                    foreach (AlgorithmInput input in additionalInputs)
                        if (input.Ownership == AlgorithmInputOwnership.Transferred) input.Image.Dispose();
                    return CreateSupersededResult(invocation);
                }
                AlgorithmHostCapabilities required = AlgorithmHostCapabilities.Interactive
                    | AlgorithmHostCapabilities.Local;
                if (additionalInputs.Count > 0) required |= AlgorithmHostCapabilities.MultiInput;
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
                    return CreateSupersededResult(invocation);
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
        }

        private static AlgorithmResult CreateSupersededResult(AlgorithmInvocation invocation) => new()
        {
            InvocationId = invocation.InvocationId,
            AlgorithmId = invocation.AlgorithmId,
            AlgorithmVersion = invocation.AlgorithmVersion ?? default,
            Status = AlgorithmResultStatus.Superseded,
            Failures = new[] { new AlgorithmFailure("commit_superseded", "The document, source revision, or invocation changed before commit.") },
        };

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
