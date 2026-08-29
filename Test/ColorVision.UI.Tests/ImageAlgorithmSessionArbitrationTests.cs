using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageAlgorithmSessionArbitrationTests
{
    [Fact]
    public async Task OlderPreviewCannotWriteHostStateAfterNewerClaimCompletes()
    {
        ImageView view = CreateImageView();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid documentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid olderInvocation = Guid.NewGuid();
            Guid newerInvocation = Guid.NewGuid();
            using ManualResetEventSlim olderReachedStateWrite = new();
            using ManualResetEventSlim releaseOlder = new();
            WpfTestHost.Invoke(() => view.AlgorithmClaimStateUpdateHook = claim =>
            {
                if (claim.InvocationId != olderInvocation) return;
                olderReachedStateWrite.Set();
                releaseOlder.Wait();
            });
            using CancellationTokenSource olderCancellation = new();
            using CancellationTokenSource newerCancellation = new();

            Task<(bool Accepted, AlgorithmInvocationClaim Claim)> older = Task.Run(() =>
            {
                bool accepted = context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(), documentId, revision, olderInvocation, olderCancellation, out AlgorithmInvocationClaim claim);
                return (accepted, claim);
            });
            Assert.True(olderReachedStateWrite.Wait(TimeSpan.FromSeconds(5)));
            Task<(bool Accepted, AlgorithmInvocationClaim Claim)> newer = Task.Run(() =>
            {
                bool accepted = context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(), documentId, revision, newerInvocation, newerCancellation, out AlgorithmInvocationClaim claim);
                return (accepted, claim);
            });
            Assert.False(newer.Wait(TimeSpan.FromMilliseconds(100)));
            releaseOlder.Set();
            (bool olderAccepted, AlgorithmInvocationClaim olderClaim) = await older;
            (bool newerAccepted, AlgorithmInvocationClaim newerClaim) = await newer;

            Assert.True(newerAccepted);
            Assert.True(olderAccepted);
            Assert.True(olderCancellation.IsCancellationRequested);
            Assert.False(newerCancellation.IsCancellationRequested);
            Assert.False(context.IsCurrentAlgorithmInvocation(olderClaim));
            Assert.True(context.IsCurrentAlgorithmInvocation(newerClaim));
            Assert.True(context.OwnsAlgorithmPreviewClaim(newerClaim));
            Assert.True(context.TryCancelAlgorithmPreview(newerClaim));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                view.AlgorithmClaimStateUpdateHook = null;
                view.Dispose();
            });
        }
    }

    [Fact]
    public async Task AnalysisTakeoverRestoresCanonicalHostAndLatePreviewCannotRepublish()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-takeover");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BlockingProvider provider = new(descriptor.Id, providerStarted, releaseProvider, 91);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        CancellationTokenSource? analysis = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            Assert.Same(session.PreviewBitmap, WpfTestHost.Invoke(() => context.FunctionImage));
            Task<AlgorithmResult> preview = session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Guid analysisInvocation = Guid.NewGuid();
            analysis = WpfTestHost.Invoke(() => ImageAlgorithmAnalysisSession.Begin(
                context,
                document,
                revision,
                Guid.NewGuid(),
                analysisInvocation));

            Assert.False(analysis.IsCancellationRequested);
            Assert.False(session.OwnsHostPreview);
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
            releaseProvider.SetResult(true);
            using AlgorithmResult late = await preview.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AlgorithmResultStatus.Superseded, late.Status);
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.Equal(revision, WpfTestHost.Invoke(() => context.ImageRevision));
            ImageAlgorithmAnalysisSession.Release(context, analysisInvocation);
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            analysis?.Dispose();
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task AnalysisClaimAndPreviewCommitPublishAsOneDispatcherOwnershipTransaction()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-commit-race");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        ImmediateProvider provider = new(descriptor.Id, 77);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        CancellationTokenSource? analysis = null;
        using ManualResetEventSlim analysisInsideClaim = new();
        using ManualResetEventSlim releaseAnalysis = new();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            using AlgorithmResult preview = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            Assert.Equal(AlgorithmResultStatus.Succeeded, preview.Status);
            Assert.Same(session.PreviewBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));

            Guid analysisInvocation = Guid.NewGuid();
            WpfTestHost.Invoke(() => view.AlgorithmClaimStateUpdateHook = claim =>
            {
                if (claim.InvocationId != analysisInvocation) return;
                analysisInsideClaim.Set();
                releaseAnalysis.Wait();
            });
            Task<CancellationTokenSource> takeover = Task.Run(() => ImageAlgorithmAnalysisSession.Begin(
                context,
                document,
                revision,
                Guid.NewGuid(),
                analysisInvocation));
            Assert.True(analysisInsideClaim.Wait(TimeSpan.FromSeconds(5)));
            Task<bool> commit = Task.Run(session.Commit);
            Assert.False(commit.Wait(TimeSpan.FromMilliseconds(100)));
            releaseAnalysis.Set();
            analysis = await takeover.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(await commit.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(analysis.IsCancellationRequested);
            Assert.Equal(revision, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, document, revision, analysisInvocation));
            ImageAlgorithmAnalysisSession.Release(context, analysisInvocation);
        }
        finally
        {
            releaseAnalysis.Set();
            WpfTestHost.Invoke(() => view.AlgorithmClaimStateUpdateHook = null);
            analysis?.Dispose();
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task OlderAnalysisBeginCannotOverwriteNewerSessionStateAfterControlledInterleave()
    {
        ImageView view = CreateImageView();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid documentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid olderInvocation = Guid.NewGuid();
            Guid newerInvocation = Guid.NewGuid();
            using ManualResetEventSlim olderClaimed = new();
            using ManualResetEventSlim releaseOlder = new();

            Task<CancellationTokenSource> older = Task.Run(() => ImageAlgorithmAnalysisSession.Begin(
                context,
                documentId,
                revision,
                Guid.NewGuid(),
                olderInvocation,
                _ =>
                {
                    olderClaimed.Set();
                    releaseOlder.Wait();
            }));
            Assert.True(olderClaimed.Wait(TimeSpan.FromSeconds(5)));
            Task<CancellationTokenSource> newerTask = Task.Run(() => ImageAlgorithmAnalysisSession.Begin(
                context,
                documentId,
                revision,
                Guid.NewGuid(),
                newerInvocation));
            Assert.False(newerTask.Wait(TimeSpan.FromMilliseconds(100)));
            releaseOlder.Set();
            using CancellationTokenSource old = await older;
            using CancellationTokenSource newer = await newerTask;

            Assert.True(old.IsCancellationRequested);
            Assert.False(newer.IsCancellationRequested);
            Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(context, documentId, revision, olderInvocation));
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, documentId, revision, newerInvocation));
            ImageAlgorithmAnalysisSession.Release(context, newerInvocation);
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void PreviewAndAnalysisOwnersSupersedeAndCancelEachOtherWithinOneScope()
    {
        ImageView view = CreateImageView();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid documentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid previewOwner = Guid.NewGuid();
            using CancellationTokenSource previewCancellation = new();
            AlgorithmInvocationClaim previewClaim = WpfTestHost.Invoke(() =>
            {
                Assert.True(context.TryBeginAlgorithmPreviewInvocation(
                    previewOwner,
                    documentId,
                    revision,
                    Guid.NewGuid(),
                    previewCancellation,
                    out AlgorithmInvocationClaim claim));
                return claim;
            });

            Guid analysisInvocationId = Guid.NewGuid();
            using CancellationTokenSource analysisCancellation = WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    context,
                    documentId,
                    revision,
                    Guid.NewGuid(),
                    analysisInvocationId));
            Assert.True(previewCancellation.IsCancellationRequested);
            Assert.False(context.IsCurrentAlgorithmInvocation(previewClaim));
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(
                context,
                documentId,
                revision,
                analysisInvocationId));

            using CancellationTokenSource replacementCancellation = new();
            AlgorithmInvocationClaim replacement = WpfTestHost.Invoke(() =>
            {
                Assert.True(context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(),
                    documentId,
                    revision,
                    Guid.NewGuid(),
                    replacementCancellation,
                    out AlgorithmInvocationClaim claim));
                return claim;
            });
            Assert.True(analysisCancellation.IsCancellationRequested);
            Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(
                context,
                documentId,
                revision,
                analysisInvocationId));
            Assert.False(context.TryReleaseAlgorithmInvocation(previewClaim));
            Assert.True(context.IsCurrentAlgorithmInvocation(replacement));
            Assert.True(WpfTestHost.Invoke(() => context.TryCancelAlgorithmPreview(replacement)));
            Assert.True(replacementCancellation.IsCancellationRequested);
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ClaimsInAnotherImageViewDocumentRemainIndependent()
    {
        ImageView first = CreateImageView();
        ImageView second = CreateImageView();
        try
        {
            ImageProcessingContext firstContext = WpfTestHost.Invoke(() => first.EditorContext.ProcessingContext);
            ImageProcessingContext secondContext = WpfTestHost.Invoke(() => second.EditorContext.ProcessingContext);
            Guid firstDocument = WpfTestHost.Invoke(() => firstContext.DocumentInstanceId);
            Guid secondDocument = WpfTestHost.Invoke(() => secondContext.DocumentInstanceId);
            long firstRevision = WpfTestHost.Invoke(() => firstContext.ImageRevision);
            long secondRevision = WpfTestHost.Invoke(() => secondContext.ImageRevision);
            Assert.NotEqual(firstDocument, secondDocument);

            using CancellationTokenSource firstAnalysis = WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    firstContext,
                    firstDocument,
                    firstRevision,
                    Guid.NewGuid(),
                    Guid.NewGuid()));
            Guid secondInvocation = Guid.NewGuid();
            using CancellationTokenSource secondAnalysis = WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    secondContext,
                    secondDocument,
                    secondRevision,
                    Guid.NewGuid(),
                    secondInvocation));

            using CancellationTokenSource firstPreview = new();
            Assert.True(WpfTestHost.Invoke(() => firstContext.TryBeginAlgorithmPreviewInvocation(
                Guid.NewGuid(),
                firstDocument,
                firstRevision,
                Guid.NewGuid(),
                firstPreview,
                out _)));

            Assert.True(firstAnalysis.IsCancellationRequested);
            Assert.False(secondAnalysis.IsCancellationRequested);
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(
                secondContext,
                secondDocument,
                secondRevision,
                secondInvocation));
        }
        finally
        {
            WpfTestHost.Invoke(first.Dispose);
            WpfTestHost.Invoke(second.Dispose);
        }
    }

    [Fact]
    public async Task SupersededPreviewOwnerCanReclaimSameRevisionWithoutPermanentSupersededState()
    {
        ImageView view = CreateImageView();
        ImageAlgorithmPreviewSession? first = null;
        ImageAlgorithmPreviewSession? second = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            first = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            second = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            Assert.False(first.IsCurrent(Guid.Empty));

            AlgorithmInvocation invocation = AlgorithmInvocation.Create(
                StandardAlgorithmIds.Invert,
                new NoAlgorithmParameters());
            using AlgorithmResult result = await first.PreviewAsync(invocation);

            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.True(first.IsCurrent(invocation.InvocationId));
            Assert.False(WpfTestHost.Invoke(second.Cancel));
            Assert.Same(first.PreviewBitmap, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.True(WpfTestHost.Invoke(first.Cancel));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
        }
        finally
        {
            if (first != null) WpfTestHost.Invoke(first.Dispose);
            if (second != null) WpfTestHost.Invoke(second.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void SourcePixelMutationInvalidatesPreviewAndPublishesTheUpdatedCanonicalBitmap()
    {
        ImageView view = CreateImageView();
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            WriteableBitmap canonical = WpfTestHost.Invoke(() => Assert.IsType<WriteableBitmap>(context.ViewBitmapSource));
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            Assert.Same(session.PreviewBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));

            WpfTestHost.Invoke(() =>
            {
                canonical.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
                view.NotifySourcePixelsChanged();
            });

            Assert.Equal(revision + 1, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.Equal((byte)42, WpfTestHost.Invoke(() => Pixel(canonical)));
            Assert.False(session.OwnsHostPreview);
            Assert.False(WpfTestHost.Invoke(session.Cancel));
        }
        finally
        {
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task PreviewDoesNotHoldSessionMonitorAcrossDispatcherPublication()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-lock-order");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 2);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 61)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        using ManualResetEventSlim publicationEntered = new();
        using ManualResetEventSlim releasePublication = new();
        Guid firstInvocationId = Guid.NewGuid();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            WpfTestHost.Invoke(() => view.AlgorithmPreviewPublicationHook = claim =>
            {
                if (claim.InvocationId != firstInvocationId) return;
                publicationEntered.Set();
                Assert.True(releasePublication.Wait(TimeSpan.FromSeconds(5)));
            });

            AlgorithmInvocation firstInvocation = new()
            {
                InvocationId = firstInvocationId,
                AlgorithmId = descriptor.Id,
                ParameterSchemaVersion = 1,
                Parameters = AlgorithmJson.ToElement(new NoAlgorithmParameters()),
            };
            Task<AlgorithmResult> first = session.PreviewAsync(firstInvocation);
            Assert.True(publicationEntered.Wait(TimeSpan.FromSeconds(5)));
            Task<AlgorithmResult> second = Task.Run(() => session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters())));

            Assert.False(second.Wait(TimeSpan.FromMilliseconds(100)));
            releasePublication.Set();
            using AlgorithmResult firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5));
            using AlgorithmResult secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(firstResult.Status, new[] { AlgorithmResultStatus.Succeeded, AlgorithmResultStatus.Superseded });
            Assert.Equal(AlgorithmResultStatus.Succeeded, secondResult.Status);
            Assert.True(session.OwnsHostPreview);
        }
        finally
        {
            releasePublication.Set();
            WpfTestHost.Invoke(() => view.AlgorithmPreviewPublicationHook = null);
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ThrowingClaimAndReleasePublicationsRollBackHostAndOwnership()
    {
        ImageView view = CreateImageView();
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            ImageSource preview = WpfTestHost.Invoke(() => context.ImageShow.Source);
            Assert.Same(session.PreviewBitmap, preview);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            WriteableBitmap rejectedPublication = WpfTestHost.Invoke(() => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            using CancellationTokenSource rejectedCancellation = new();

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                context.TryBeginAlgorithmAnalysisInvocation(
                    Guid.NewGuid(),
                    document,
                    revision,
                    Guid.NewGuid(),
                    rejectedCancellation,
                    _ =>
                    {
                        context.FunctionImage = rejectedPublication;
                        context.ImageShow.Source = rejectedPublication;
                        throw new InvalidOperationException("injected claim publication failure");
                    },
                    out _)));

            Assert.False(rejectedCancellation.IsCancellationRequested);
            Assert.True(session.OwnsHostPreview);
            Assert.Same(preview, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(preview, WpfTestHost.Invoke(() => context.ImageShow.Source));

            AlgorithmInvocationScope scope = new(document, revision);
            Assert.True(context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(scope, out AlgorithmInvocationClaim claim));
            WriteableBitmap failedCommit = WpfTestHost.Invoke(() => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                context.TryCompleteAlgorithmPreview(claim, () =>
                {
                    context.ViewBitmapSource = failedCommit;
                    context.FunctionImage = failedCommit;
                    context.ImageShow.Source = failedCommit;
                    throw new InvalidOperationException("injected release publication failure");
                })));

            Assert.True(context.IsCurrentAlgorithmInvocation(claim));
            Assert.True(session.OwnsHostPreview);
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ViewBitmapSource));
            Assert.Same(preview, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(preview, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(WpfTestHost.Invoke(session.Cancel));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
        }
        finally
        {
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ThrowingAnalysisAcceptancePreservesPriorAnalysisStateAndCanonicalHost()
    {
        ImageView view = CreateImageView();
        CancellationTokenSource? current = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            Guid currentInvocation = Guid.NewGuid();
            current = WpfTestHost.Invoke(() => ImageAlgorithmAnalysisSession.Begin(
                context,
                document,
                revision,
                Guid.NewGuid(),
                currentInvocation));
            WriteableBitmap rejected = WpfTestHost.Invoke(
                () => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    context,
                    document,
                    revision,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _ =>
                    {
                        context.FunctionImage = rejected;
                        context.ImageShow.Source = rejected;
                        throw new InvalidOperationException("injected analysis acceptance failure");
                    })));

            Assert.False(current.IsCancellationRequested);
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, document, revision, currentInvocation));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
            ImageAlgorithmAnalysisSession.Release(context, currentInvocation);
        }
        finally
        {
            current?.Dispose();
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ReentrantClaimFailureCannotRestoreHostOverTheNewerPreviewOwner()
    {
        ImageView view = CreateImageView();
        ImageAlgorithmPreviewSession? original = null;
        ImageAlgorithmPreviewSession? replacement = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            original = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            ImageSource originalPreview = WpfTestHost.Invoke(() => context.ImageShow.Source);

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    context,
                    document,
                    revision,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _ => replacement = ImageAlgorithmPreviewSession.Start(context))));

            Assert.NotNull(replacement);
            Assert.True(replacement!.OwnsHostPreview);
            Assert.Same(replacement.PreviewBitmap, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(replacement.PreviewBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.NotSame(originalPreview, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(
                new AlgorithmInvocationScope(document, revision), out AlgorithmInvocationClaim current));
            Assert.True(context.OwnsAlgorithmPreviewClaim(current));
        }
        finally
        {
            if (replacement != null) WpfTestHost.Invoke(replacement.Dispose);
            if (original != null) WpfTestHost.Invoke(original.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ReentrantReleaseFailureCannotRestoreHostOverTheNewerPreviewOwner()
    {
        ImageView view = CreateImageView();
        ImageAlgorithmPreviewSession? original = null;
        ImageAlgorithmPreviewSession? replacement = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            original = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            Assert.True(context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(
                new AlgorithmInvocationScope(document, revision), out AlgorithmInvocationClaim originalClaim));

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                context.TryCompleteAlgorithmPreview(originalClaim, () =>
                {
                    replacement = ImageAlgorithmPreviewSession.Start(context);
                    throw new InvalidOperationException("injected outer release failure");
                })));

            Assert.NotNull(replacement);
            Assert.True(replacement!.OwnsHostPreview);
            Assert.Same(replacement.PreviewBitmap, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(replacement.PreviewBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(
                new AlgorithmInvocationScope(document, revision), out AlgorithmInvocationClaim current));
            Assert.NotEqual(originalClaim, current);
            Assert.True(context.OwnsAlgorithmPreviewClaim(current));
        }
        finally
        {
            if (replacement != null) WpfTestHost.Invoke(replacement.Dispose);
            if (original != null) WpfTestHost.Invoke(original.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ReentrantDocumentInvalidationCannotResurrectStaleAnalysisState()
    {
        ImageView view = CreateImageView();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid invocation = Guid.NewGuid();

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    context,
                    document,
                    revision,
                    Guid.NewGuid(),
                    invocation,
                    _ => view.NotifySourcePixelsChanged())));

            Assert.Equal(Guid.Empty, ImageAlgorithmAnalysisSession.TrackedInvocationId(context));
            Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(context, document, revision, invocation));
            Assert.False(context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(
                new AlgorithmInvocationScope(document, revision), out _));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(WpfTestHost.Invoke(() => context.ViewBitmapSource), WpfTestHost.Invoke(() => context.ImageShow.Source));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task StaleCommitCannotCancelOrRestoreTheNewerInvocation()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.stale-commit");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 73)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        using ManualResetEventSlim commitSnapshotTaken = new();
        using ManualResetEventSlim releaseCommit = new();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            using AlgorithmResult first = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            Assert.Equal(AlgorithmResultStatus.Succeeded, first.Status);
            Guid firstInvocation = session.LatestInvocationId;
            WpfTestHost.Invoke(() => view.AlgorithmPreviewCommitHook = claim =>
            {
                if (claim.InvocationId != firstInvocation) return;
                commitSnapshotTaken.Set();
                Assert.True(releaseCommit.Wait(TimeSpan.FromSeconds(5)));
            });

            Task<bool> staleCommit = Task.Run(session.Commit);
            Assert.True(commitSnapshotTaken.Wait(TimeSpan.FromSeconds(5)));
            AlgorithmInvocation newerInvocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters());
            using AlgorithmResult newer = await session.PreviewAsync(newerInvocation);
            Assert.Equal(AlgorithmResultStatus.Succeeded, newer.Status);
            releaseCommit.Set();

            Assert.False(await staleCommit.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(newerInvocation.InvocationId, session.LatestInvocationId);
            Assert.True(session.OwnsHostPreview);
            Assert.Same(session.PreviewBitmap, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(session.PreviewBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(WpfTestHost.Invoke(session.Cancel));
        }
        finally
        {
            releaseCommit.Set();
            WpfTestHost.Invoke(() => view.AlgorithmPreviewCommitHook = null);
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task ThrowingPreviewPublicationDisposesTheSuccessfulProviderResult()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-publication-disposal");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        AlgorithmImageBuffer? produced = null;
        TestResultProvider provider = new(descriptor.Id, () =>
        {
            produced = new AlgorithmImageBuffer(4, 2, 4, AlgorithmImageFormat.Gray8, Enumerable.Repeat((byte)61, 8).ToArray());
            return new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("output", "primary", produced)],
            };
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            WpfTestHost.Invoke(() => view.AlgorithmPreviewPublicationHook = _ =>
                throw new InvalidOperationException("injected publication failure"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters())));

            Assert.NotNull(produced);
            Assert.True(produced!.IsDisposed);
        }
        finally
        {
            WpfTestHost.Invoke(() => view.AlgorithmPreviewPublicationHook = null);
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task ThrowingPreviewImageConversionDisposesTheSuccessfulProviderResult()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-conversion-disposal") with
        {
            OutputFormats = new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Bgr96Float },
        };
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        AlgorithmImageBuffer? produced = null;
        TestResultProvider provider = new(descriptor.Id, () =>
        {
            produced = new AlgorithmImageBuffer(
                4,
                2,
                48,
                AlgorithmImageFormat.Bgr96Float,
                new byte[384]);
            return new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("output", "primary", produced)],
            };
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters())));

            Assert.NotNull(produced);
            Assert.True(produced!.IsDisposed);
        }
        finally
        {
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void DocumentMutationCancellationCallbackCanInstallANewRevisionClaimWithoutCreatingAZombie()
    {
        ImageView view = CreateImageView();
        using CancellationTokenSource oldCancellation = new();
        using CancellationTokenSource newCancellation = new();
        CancellationTokenRegistration registration = default;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long oldRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            WriteableBitmap oldPreview = WpfTestHost.Invoke(
                () => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            WriteableBitmap newPreview = WpfTestHost.Invoke(
                () => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            AlgorithmInvocationClaim newClaim = default;
            bool newAccepted = false;

            Assert.True(WpfTestHost.Invoke(() => context.TryBeginAlgorithmPreviewInvocation(
                Guid.NewGuid(),
                document,
                oldRevision,
                Guid.NewGuid(),
                oldCancellation,
                () =>
                {
                    context.ImageShow.Source = canonical;
                    context.FunctionImage = null;
                },
                out _)));
            WpfTestHost.Invoke(() =>
            {
                context.FunctionImage = oldPreview;
                context.ImageShow.Source = oldPreview;
            });
            registration = oldCancellation.Token.Register(() =>
            {
                long newRevision = context.ImageRevision;
                newAccepted = context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(),
                    document,
                    newRevision,
                    Guid.NewGuid(),
                    newCancellation,
                    () =>
                    {
                        context.ImageShow.Source = context.ViewBitmapSource;
                        context.FunctionImage = null;
                    },
                    out newClaim);
                if (newAccepted)
                {
                    context.FunctionImage = newPreview;
                    context.ImageShow.Source = newPreview;
                }
            });

            WpfTestHost.Invoke(view.NotifySourcePixelsChanged);

            Assert.True(oldCancellation.IsCancellationRequested);
            Assert.True(newAccepted);
            Assert.False(newCancellation.IsCancellationRequested);
            Assert.True(context.IsCurrentAlgorithmInvocation(newClaim));
            Assert.True(context.OwnsAlgorithmPreviewClaim(newClaim));
            Assert.True(context.HasActiveAlgorithmPreview);
            Assert.Same(newPreview, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(newPreview, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.Equal(Guid.Empty, ImageAlgorithmAnalysisSession.TrackedInvocationId(context));
            Assert.True(WpfTestHost.Invoke(() => context.TryCancelAlgorithmPreview(newClaim)));
        }
        finally
        {
            registration.Dispose();
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task DelayedOldRevisionCleanupCannotInvalidateANewRevisionPreviewClaim()
    {
        ImageView view = CreateImageView();
        using CancellationTokenSource oldCancellation = new();
        using CancellationTokenSource newCancellation = new();
        using ManualResetEventSlim revisionAdvanced = new();
        using ManualResetEventSlim releaseCleanup = new();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long oldRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            WriteableBitmap newPreview = WpfTestHost.Invoke(
                () => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            AlgorithmInvocationClaim oldClaim = default;
            AlgorithmInvocationClaim newClaim = default;

            Assert.True(WpfTestHost.Invoke(() => context.TryBeginAlgorithmPreviewInvocation(
                Guid.NewGuid(), document, oldRevision, Guid.NewGuid(), oldCancellation, out oldClaim)));
            WpfTestHost.Invoke(() => view.ImageDocumentRevisionAdvancedHook = (_, _) =>
            {
                revisionAdvanced.Set();
                Assert.True(releaseCleanup.Wait(TimeSpan.FromSeconds(5)));
            });

            Task mutation = Task.Run(view.NotifySourcePixelsChanged);
            Assert.True(revisionAdvanced.Wait(TimeSpan.FromSeconds(5)));
            long newRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            Assert.Equal(oldRevision + 1, newRevision);
            Assert.True(WpfTestHost.Invoke(() => context.TryBeginAlgorithmPreviewInvocation(
                Guid.NewGuid(),
                document,
                newRevision,
                Guid.NewGuid(),
                newCancellation,
                () =>
                {
                    context.FunctionImage = null;
                    context.ImageShow.Source = canonical;
                },
                out newClaim)));
            WpfTestHost.Invoke(() =>
            {
                context.FunctionImage = newPreview;
                context.ImageShow.Source = newPreview;
            });

            releaseCleanup.Set();
            await mutation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(oldCancellation.IsCancellationRequested);
            Assert.False(newCancellation.IsCancellationRequested);
            Assert.False(context.IsCurrentAlgorithmInvocation(oldClaim));
            Assert.True(context.IsCurrentAlgorithmInvocation(newClaim));
            Assert.True(context.OwnsAlgorithmPreviewClaim(newClaim));
            Assert.True(context.HasActiveAlgorithmPreview);
            Assert.Same(newPreview, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(newPreview, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(WpfTestHost.Invoke(() => context.TryCancelAlgorithmPreview(newClaim)));
        }
        finally
        {
            releaseCleanup.Set();
            WpfTestHost.Invoke(() =>
            {
                view.ImageDocumentRevisionAdvancedHook = null;
                view.Dispose();
            });
        }
    }

    [Fact]
    public async Task DelayedOldRevisionCleanupCannotDetachANewRevisionAnalysisSession()
    {
        ImageView view = CreateImageView();
        CancellationTokenSource? oldCancellation = null;
        CancellationTokenSource? newCancellation = null;
        using ManualResetEventSlim revisionAdvanced = new();
        using ManualResetEventSlim releaseCleanup = new();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long oldRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid oldInvocation = Guid.NewGuid();
            Guid newInvocation = Guid.NewGuid();
            oldCancellation = WpfTestHost.Invoke(() => ImageAlgorithmAnalysisSession.Begin(
                context, document, oldRevision, Guid.NewGuid(), oldInvocation));
            WpfTestHost.Invoke(() => view.ImageDocumentRevisionAdvancedHook = (_, _) =>
            {
                revisionAdvanced.Set();
                Assert.True(releaseCleanup.Wait(TimeSpan.FromSeconds(5)));
            });

            Task mutation = Task.Run(view.NotifySourcePixelsChanged);
            Assert.True(revisionAdvanced.Wait(TimeSpan.FromSeconds(5)));
            long newRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            newCancellation = WpfTestHost.Invoke(() => ImageAlgorithmAnalysisSession.Begin(
                context, document, newRevision, Guid.NewGuid(), newInvocation));

            releaseCleanup.Set();
            await mutation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(oldCancellation.IsCancellationRequested);
            Assert.False(newCancellation.IsCancellationRequested);
            Assert.Equal(newInvocation, ImageAlgorithmAnalysisSession.TrackedInvocationId(context));
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, document, newRevision, newInvocation));
            ImageAlgorithmAnalysisSession.Release(context, newInvocation);
        }
        finally
        {
            releaseCleanup.Set();
            oldCancellation?.Dispose();
            newCancellation?.Dispose();
            WpfTestHost.Invoke(() =>
            {
                view.ImageDocumentRevisionAdvancedHook = null;
                view.Dispose();
            });
        }
    }

    [Fact]
    public void ThrowingPostAcceptanceNotificationReleasesOnlyItsPreviewClaim()
    {
        ImageView view = CreateImageView();
        using CancellationTokenSource cancellation = new();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            WriteableBitmap rejectedPreview = WpfTestHost.Invoke(
                () => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            WpfTestHost.Invoke(() => view.AlgorithmPreviewClaimAcceptedHook = _ =>
            {
                context.FunctionImage = rejectedPreview;
                context.ImageShow.Source = rejectedPreview;
                throw new InvalidOperationException("injected post-acceptance failure");
            });

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(),
                    document,
                    revision,
                    Guid.NewGuid(),
                    cancellation,
                    () =>
                    {
                        context.FunctionImage = null;
                        context.ImageShow.Source = canonical;
                    },
                    out _)));

            Assert.True(cancellation.IsCancellationRequested);
            Assert.False(context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(
                new AlgorithmInvocationScope(document, revision), out _));
            Assert.False(context.HasActiveAlgorithmPreview);
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(canonical, WpfTestHost.Invoke(() => context.ImageShow.Source));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                view.AlgorithmPreviewClaimAcceptedHook = null;
                view.Dispose();
            });
        }
    }

    [Fact]
    public void ThrowingPostAcceptanceNotificationCannotReleaseAReentrantNewerPreview()
    {
        ImageView view = CreateImageView();
        using CancellationTokenSource rejectedCancellation = new();
        using CancellationTokenSource newerCancellation = new();
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid rejectedInvocation = Guid.NewGuid();
            Guid newerInvocation = Guid.NewGuid();
            AlgorithmInvocationClaim newerClaim = default;
            WriteableBitmap newerPreview = WpfTestHost.Invoke(
                () => new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null));
            WpfTestHost.Invoke(() => view.AlgorithmPreviewClaimAcceptedHook = claim =>
            {
                if (claim.InvocationId != rejectedInvocation) return;
                Assert.True(context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(), document, revision, newerInvocation, newerCancellation, out newerClaim));
                context.FunctionImage = newerPreview;
                context.ImageShow.Source = newerPreview;
                throw new InvalidOperationException("injected reentrant post-acceptance failure");
            });

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(), document, revision, rejectedInvocation, rejectedCancellation, out _)));

            Assert.True(rejectedCancellation.IsCancellationRequested);
            Assert.False(newerCancellation.IsCancellationRequested);
            Assert.True(context.IsCurrentAlgorithmInvocation(newerClaim));
            Assert.True(context.OwnsAlgorithmPreviewClaim(newerClaim));
            Assert.Same(newerPreview, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(newerPreview, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.True(WpfTestHost.Invoke(() => context.TryCancelAlgorithmPreview(newerClaim)));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                view.AlgorithmPreviewClaimAcceptedHook = null;
                view.Dispose();
            });
        }
    }

    [Fact]
    public void ThrowingAnalysisAcceptanceCannotReleaseAReentrantNewerAnalysis()
    {
        ImageView view = CreateImageView();
        CancellationTokenSource? newerCancellation = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid newerInvocation = Guid.NewGuid();

            Assert.Throws<InvalidOperationException>(() => WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    context,
                    document,
                    revision,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _ =>
                    {
                        newerCancellation = ImageAlgorithmAnalysisSession.Begin(
                            context, document, revision, Guid.NewGuid(), newerInvocation);
                        throw new InvalidOperationException("injected outer analysis failure");
                    })));

            Assert.NotNull(newerCancellation);
            Assert.False(newerCancellation!.IsCancellationRequested);
            Assert.Equal(newerInvocation, ImageAlgorithmAnalysisSession.TrackedInvocationId(context));
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, document, revision, newerInvocation));
            ImageAlgorithmAnalysisSession.Release(context, newerInvocation);
        }
        finally
        {
            newerCancellation?.Dispose();
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task StaleCommitDuringContextToSessionClaimHandoffCannotCompleteTheSession()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.claim-handoff-commit");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 84)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            using AlgorithmResult first = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            Assert.Equal(AlgorithmResultStatus.Succeeded, first.Status);
            AlgorithmInvocation secondInvocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters());
            bool? staleCommit = null;
            WpfTestHost.Invoke(() => view.AlgorithmPreviewClaimAcceptedHook = claim =>
            {
                if (claim.InvocationId == secondInvocation.InvocationId)
                    staleCommit = session.Commit();
            });

            using AlgorithmResult second = await session.PreviewAsync(secondInvocation);

            Assert.False(staleCommit);
            Assert.Equal(AlgorithmResultStatus.Succeeded, second.Status);
            Assert.Equal(secondInvocation.InvocationId, session.LatestInvocationId);
            Assert.True(session.OwnsHostPreview);
            Assert.True(context.HasActiveAlgorithmPreview);
            Assert.True(WpfTestHost.Invoke(session.Cancel));
        }
        finally
        {
            WpfTestHost.Invoke(() => view.AlgorithmPreviewClaimAcceptedHook = null);
            if (session != null) WpfTestHost.Invoke(session.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void PreviewClaimSupersededByPreviousCancellationCallbackIsNotReportedAsAccepted()
    {
        ImageView view = CreateImageView();
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource candidateCancellation = new();
        using CancellationTokenSource newerCancellation = new();
        CancellationTokenRegistration registration = default;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid candidateInvocation = Guid.NewGuid();
            Guid newerInvocation = Guid.NewGuid();
            List<Guid> reportedAccepted = [];
            AlgorithmInvocationClaim newerClaim = default;
            bool newerAccepted = false;

            Assert.True(WpfTestHost.Invoke(() => context.TryBeginAlgorithmPreviewInvocation(
                Guid.NewGuid(), document, revision, Guid.NewGuid(), previousCancellation, out _)));
            WpfTestHost.Invoke(() => view.AlgorithmPreviewClaimAcceptedHook = claim => reportedAccepted.Add(claim.InvocationId));
            registration = previousCancellation.Token.Register(() =>
            {
                newerAccepted = context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(), document, revision, newerInvocation, newerCancellation, out newerClaim);
            });

            (bool accepted, AlgorithmInvocationClaim claim) = WpfTestHost.Invoke(() =>
            {
                bool value = context.TryBeginAlgorithmPreviewInvocation(
                    Guid.NewGuid(), document, revision, candidateInvocation, candidateCancellation, out AlgorithmInvocationClaim result);
                return (value, result);
            });

            Assert.False(accepted);
            Assert.Equal(default, claim);
            Assert.True(newerAccepted);
            Assert.True(context.IsCurrentAlgorithmInvocation(newerClaim));
            Assert.True(context.OwnsAlgorithmPreviewClaim(newerClaim));
            Assert.DoesNotContain(candidateInvocation, reportedAccepted);
            Assert.Contains(newerInvocation, reportedAccepted);
            Assert.True(candidateCancellation.IsCancellationRequested);
            Assert.False(newerCancellation.IsCancellationRequested);
            Assert.True(WpfTestHost.Invoke(() => context.TryCancelAlgorithmPreview(newerClaim)));
        }
        finally
        {
            registration.Dispose();
            WpfTestHost.Invoke(() =>
            {
                view.AlgorithmPreviewClaimAcceptedHook = null;
                view.Dispose();
            });
        }
    }

    [Fact]
    public void AnalysisClaimSupersededByPreviousCancellationCallbackIsNotReportedAsAccepted()
    {
        ImageView view = CreateImageView();
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource candidateCancellation = new();
        using CancellationTokenSource newerCancellation = new();
        CancellationTokenRegistration registration = default;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            Guid document = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            AlgorithmInvocationClaim newerClaim = default;
            bool newerAccepted = false;

            Assert.True(WpfTestHost.Invoke(() => context.TryBeginAlgorithmAnalysisInvocation(
                Guid.NewGuid(), document, revision, Guid.NewGuid(), previousCancellation, out _)));
            registration = previousCancellation.Token.Register(() =>
            {
                newerAccepted = context.TryBeginAlgorithmAnalysisInvocation(
                    Guid.NewGuid(), document, revision, Guid.NewGuid(), newerCancellation, out newerClaim);
            });

            (bool accepted, AlgorithmInvocationClaim claim) = WpfTestHost.Invoke(() =>
            {
                bool value = context.TryBeginAlgorithmAnalysisInvocation(
                    Guid.NewGuid(), document, revision, Guid.NewGuid(), candidateCancellation, out AlgorithmInvocationClaim result);
                return (value, result);
            });

            Assert.False(accepted);
            Assert.Equal(default, claim);
            Assert.True(newerAccepted);
            Assert.True(context.IsCurrentAlgorithmInvocation(newerClaim));
            Assert.True(candidateCancellation.IsCancellationRequested);
            Assert.False(newerCancellation.IsCancellationRequested);
            Assert.True(WpfTestHost.Invoke(() => context.TryReleaseAlgorithmInvocation(newerClaim)));
        }
        finally
        {
            registration.Dispose();
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    private static ImageView CreateImageView()
        => CreateImageView(ImageAlgorithmPlatform.Runtime);

    private static ImageView CreateImageView(AlgorithmRuntime runtime)
        => WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new(runtime);
            WriteableBitmap bitmap = new(4, 2, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 4, 2), new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 4, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });

    private static byte Pixel(BitmapSource bitmap)
    {
        byte[] pixel = new byte[1];
        bitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 1, 0);
        return pixel[0];
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private static AlgorithmDescriptor Descriptor(string id) => new(
        new AlgorithmId(id),
        new AlgorithmVersion(1, 0, 0),
        id,
        "test",
        "preview arbitration",
        typeof(NoAlgorithmParameters),
        new AlgorithmParameterSchema(1, Array.Empty<AlgorithmParameterField>(), AlgorithmJson.ToElement(new NoAlgorithmParameters())),
        new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
        AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

    private sealed class ImmediateProvider(AlgorithmId algorithmId, byte output) : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = MetadataFor($"immediate-{algorithmId.Value}");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(Result(output));
    }

    private sealed class BlockingProvider(
        AlgorithmId algorithmId,
        TaskCompletionSource<bool> started,
        TaskCompletionSource<bool> release,
        byte output) : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = MetadataFor($"blocking-{algorithmId.Value}");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public async ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            started.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            return Result(output);
        }
    }

    private sealed class TestResultProvider(AlgorithmId algorithmId, Func<AlgorithmResult> resultFactory) : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = MetadataFor($"result-{algorithmId.Value}");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(resultFactory());
    }

    private static AlgorithmProviderMetadata MetadataFor(string id) => new(
        id,
        id,
        AlgorithmProviderKind.Cpu,
        AlgorithmExecutionPlane.Local,
        1,
        AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

    private static AlgorithmResult Result(byte output) => new()
    {
        Status = AlgorithmResultStatus.Succeeded,
        Artifacts =
        [
            new AlgorithmImageArtifact(
                "output",
                "primary",
                new AlgorithmImageBuffer(4, 2, 4, AlgorithmImageFormat.Gray8, Enumerable.Repeat(output, 8).ToArray())),
        ],
    };
}
