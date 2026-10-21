using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.Windows;
using System.Windows.Controls;
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
