using ColorVision.Algorithms;
using OpenCvSharp;
using System;
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
            AlgorithmResult result = await session.PreviewAsync(invocation, AlgorithmHostCapabilities.Interactive, cancellationToken);
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
