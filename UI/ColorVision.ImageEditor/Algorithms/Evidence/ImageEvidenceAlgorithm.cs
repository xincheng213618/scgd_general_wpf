using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms.Evidence;

/// <summary>An evidence adapter borrows the source pixels read-only and returns neutral artifacts.</summary>
public sealed record ImageEvidenceAlgorithm(string Id, string Name, Func<HImage, AlgorithmResult> Evaluate);

public sealed record ImageEvidenceObservation(string AlgorithmId, string AlgorithmName, double ElapsedMs, AlgorithmResult Result);
public sealed record ImageEvidenceComparison(long SourceRevision, int Width, int Height, string PixelFormat, IReadOnlyList<ImageEvidenceObservation> Observations);

public static class ImageEvidenceRunner
{
    public static Task<ImageEvidenceComparison> CompareAsync(ImageProcessingContext source,
        IReadOnlyList<ImageEvidenceAlgorithm> algorithms, CancellationToken cancellationToken = default)
    {
        // Capture at the existing WPF boundary: HImage alone cannot distinguish RGB48 from BGR48.
        // A single canonical snapshot also keeps every comparison independent of viewer edits/closure.
        var (snapshot, revision) = ImageAlgorithmInputFactory.AcquireCurrentFrame(source);
        return Task.Run(() =>
        {
            using (snapshot)
            using (AlgorithmImageMatLease pixels = AlgorithmImageInterop.BorrowReadOnly(snapshot))
            {
                HImage image = new()
                {
                    rows = snapshot.Height, cols = snapshot.Width, channels = snapshot.Format.Channels(),
                    depth = snapshot.Format.BitsPerChannel(), stride = snapshot.Stride, pData = pixels.Mat.Data, isDispose = true
                };
                return new ImageEvidenceComparison(revision, snapshot.Width, snapshot.Height, snapshot.Format.ToString(),
                    Run(image, algorithms, cancellationToken));
            }
        });
    }

    // The caller holds its image lease until this synchronous operation finishes.
    public static IReadOnlyList<ImageEvidenceObservation> Run(
        HImage image, IReadOnlyList<ImageEvidenceAlgorithm> algorithms, CancellationToken cancellationToken = default)
    {
        List<ImageEvidenceObservation> observations = [];
        try
        {
            foreach (ImageEvidenceAlgorithm algorithm in algorithms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Stopwatch timer = Stopwatch.StartNew();
                AlgorithmResult result;
                try { result = algorithm.Evaluate(image); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    result = new AlgorithmResult
                    {
                        AlgorithmId = new AlgorithmId(algorithm.Id),
                        Status = AlgorithmResultStatus.Failed,
                        Failures = [new AlgorithmFailure("ExecutionError", ex.Message)]
                    };
                }
                observations.Add(new(algorithm.Id, algorithm.Name, timer.Elapsed.TotalMilliseconds, result));
            }
            cancellationToken.ThrowIfCancellationRequested();
            return observations;
        }
        catch
        {
            foreach (ImageEvidenceObservation observation in observations) observation.Result.Dispose();
            throw;
        }
    }
}
