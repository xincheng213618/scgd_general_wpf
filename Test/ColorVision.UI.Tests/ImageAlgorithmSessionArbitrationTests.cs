using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageAlgorithmSessionArbitrationTests
{
    [Theory]
    [InlineData("source-replaced")]
    [InlineData("revision-changed")]
    [InlineData("newer-session")]
    public async Task OneShotParameterConfirmationCannotRetargetOrReclaimSupersededOperation(string change)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.one-shot-parameter-binding");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 77)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        ImageAlgorithmPreviewSession? newer = null;
        TaskCompletionSource<bool> parametersOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> confirmParameters = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters());
            Task<AlgorithmResult> operation = WpfTestHost.Invoke(async () =>
            {
                parametersOpened.TrySetResult(true);
                await confirmParameters.Task;
                return await ImageAlgorithmApplier.ApplyAsync(session, invocation, Array.Empty<AlgorithmInput>());
            });
            await parametersOpened.Task.WaitAsync(TimeSpan.FromSeconds(5));
            (ImageSource source, ImageSource displayed, ImageSource? functionImage, long revision, byte pixel) snapshot = WpfTestHost.Invoke(() =>
            {
                switch (change)
                {
                    case "source-replaced":
                        WriteableBitmap replacement = new(4, 2, 96, 96, PixelFormats.Gray8, null);
                        replacement.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
                        view.SetImageSource(replacement, enableEditorImageServices: false, configureDefaultLayerController: false);
                        break;
                    case "revision-changed":
                        view.NotifySourcePixelsChanged();
                        break;
                    case "newer-session":
                        newer = ImageAlgorithmPreviewSession.Start(context);
                        break;
                }
                return (context.ViewBitmapSource, context.ImageShow.Source, context.FunctionImage,
                    context.ImageRevision, Pixel((BitmapSource)context.ViewBitmapSource));
            });

            confirmParameters.TrySetResult(true);
            using AlgorithmResult result = await operation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(AlgorithmResultStatus.Superseded, result.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(snapshot.source, context.ViewBitmapSource);
                Assert.Same(snapshot.displayed, context.ImageShow.Source);
                Assert.Same(snapshot.functionImage, context.FunctionImage);
                Assert.Equal(snapshot.revision, context.ImageRevision);
                Assert.Equal(snapshot.pixel, Pixel((BitmapSource)context.ViewBitmapSource));
                if (newer != null) Assert.True(newer.OwnsHostPreview);
            });
        }
        finally
        {
            confirmParameters.TrySetResult(true);
            WpfTestHost.Invoke(() => { session?.Dispose(); newer?.Dispose(); view.Dispose(); });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OneShotSingleInputApplierCommitsExactlyOnce(bool callerOwnsSession)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.one-shot-single-input");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 77)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters());
            if (callerOwnsSession) session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));

            using AlgorithmResult result = await WpfTestHost.Invoke(() => callerOwnsSession
                ? ImageAlgorithmApplier.ApplyAsync(session!, invocation, Array.Empty<AlgorithmInput>())
                : ImageAlgorithmApplier.ApplyAsync(context, invocation));

            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(revision + 1, context.ImageRevision);
                Assert.Equal((byte)77, Pixel((BitmapSource)context.ViewBitmapSource));
                Assert.Same(context.ViewBitmapSource, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                if (session != null) Assert.False(session.Commit());
                Assert.Equal(revision + 1, context.ImageRevision);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { session?.Dispose(); view.Dispose(); });
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CommitCannotUseThePreviousSuccessfulBitmapWhileANewerInvocationIsPending(bool catalogPreview, bool reuseInvocationId)
    {
        AlgorithmDescriptor firstDescriptor = Descriptor("test.commit-previous-success");
        AlgorithmDescriptor pendingDescriptor = Descriptor("test.commit-pending-image");
        AlgorithmCatalog catalog = new();
        catalog.Register(firstDescriptor);
        catalog.Register(pendingDescriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BlockingProvider pendingProvider = new(pendingDescriptor.Id, providerStarted, releaseProvider, 91);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(firstDescriptor.Id, 77), pendingProvider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        Task<AlgorithmResult>? pending = null;
        AlgorithmResult? completed = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => catalogPreview
                ? ImageAlgorithmPreviewSession.StartForCatalogPreview(context)
                : ImageAlgorithmPreviewSession.Start(context));
            AlgorithmInvocation firstInvocation = AlgorithmInvocation.Create(firstDescriptor.Id, new NoAlgorithmParameters());
            using AlgorithmResult first = await session.PreviewAsync(firstInvocation);
            Assert.Equal(AlgorithmResultStatus.Succeeded, first.Status);
            WriteableBitmap previousPreview = WpfTestHost.Invoke(() => session.PreviewBitmap);
            AlgorithmInvocation nextInvocation = new()
            {
                InvocationId = reuseInvocationId ? firstInvocation.InvocationId : Guid.NewGuid(),
                AlgorithmId = pendingDescriptor.Id,
                Parameters = firstInvocation.Parameters,
            };
            long previousSequence = WpfTestHost.Invoke(() => context.AlgorithmClaimSequence);
            pending = session.PreviewAsync(nextInvocation);
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(WpfTestHost.Invoke(() => context.AlgorithmClaimSequence) > previousSequence);

            Assert.False(WpfTestHost.Invoke(session.Commit));
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Same(previousPreview, context.ImageShow.Source);
                Assert.Same(previousPreview, context.FunctionImage);
                Assert.Equal(revision, context.ImageRevision);
                Assert.Equal((byte)77, Pixel(previousPreview));
            });

            releaseProvider.TrySetResult(true);
            completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AlgorithmResultStatus.Succeeded, completed.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.NotSame(previousPreview, session.PreviewBitmap);
                Assert.True(session.Commit());
                Assert.Equal(revision + 1, context.ImageRevision);
                Assert.Equal((byte)91, Pixel((BitmapSource)context.ViewBitmapSource));
                Assert.Same(context.ViewBitmapSource, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                Assert.False(session.Commit());
                Assert.Equal(revision + 1, context.ImageRevision);
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            try
            {
                if (completed == null && pending != null) completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                completed?.Dispose();
                WpfTestHost.Invoke(() => { session?.Dispose(); view.Dispose(); });
            }
        }
    }

    [Fact]
    public async Task CatalogPreviewClaimsTheCanonicalDisplayAndUsesItsPrivateSnapshotUntilOneMutableCommit()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.catalog-first-image");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[]? observedInput = null;
        BlockingProvider provider = new(descriptor.Id, providerStarted, releaseProvider, 91,
            input => observedInput = input.Image.Data.ToArray());
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        Task<AlgorithmResult>? pending = null;
        AlgorithmResult? result = null;
        DependencyPropertyDescriptor? sourceProperty = null;
        EventHandler? sourceChanged = null;
        bool? committedDuringPublication = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            WriteableBitmap canonical = WpfTestHost.Invoke(() => Assert.IsType<WriteableBitmap>(context.ViewBitmapSource));
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.StartForCatalogPreview(context));
            WpfTestHost.Invoke(() =>
            {
                Assert.True(session.OwnsHostPreview);
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Same(canonical, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                Assert.Equal(revision, context.ImageRevision);
                Assert.Throws<InvalidOperationException>(() => _ = session.PreviewBitmap);
                Assert.False(session.Commit());
                Assert.True(session.OwnsHostPreview);
                Assert.Throws<InvalidOperationException>(() => _ = session.PreviewBitmap);
                // The session must already own private input before the mutable source can be used again.
                canonical.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
            });
            WpfTestHost.Invoke(() =>
            {
                sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(DrawCanvas))!;
                sourceChanged = (_, _) =>
                {
                    if (ReferenceEquals(context.ImageShow.Source, canonical) || committedDuringPublication.HasValue) return;
                    // Source changes synchronously inside Publish, before its transaction is ready to commit.
                    committedDuringPublication = session.Commit();
                };
                sourceProperty.AddValueChanged(context.ImageShow, sourceChanged);
            });
            pending = session.PreviewAsync(AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseProvider.TrySetResult(true);
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.False(committedDuringPublication);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, observedInput);
            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap preview = session.PreviewBitmap;
                Assert.NotSame(canonical, preview);
                Assert.False(preview.IsFrozen);
                Assert.Same(preview, context.ImageShow.Source);
                Assert.Same(preview, context.FunctionImage);
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Equal(revision, context.ImageRevision);
                Assert.Equal((byte)42, Pixel(canonical));
                preview.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 211 }, 1, 0);
                Assert.Equal((byte)42, Pixel(canonical));
                Assert.True(session.Commit());
                Assert.Same(preview, context.ViewBitmapSource);
                Assert.Same(preview, context.ImageShow.Source);
                Assert.Equal((byte)211, Pixel(preview));
                Assert.Null(context.FunctionImage);
                Assert.Equal(revision + 1, context.ImageRevision);
                Assert.False(session.Commit());
                Assert.Equal(revision + 1, context.ImageRevision);
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            try
            {
                if (result == null && pending != null) result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                result?.Dispose();
                WpfTestHost.Invoke(() =>
                {
                    if (sourceProperty != null && sourceChanged != null)
                        sourceProperty.RemoveValueChanged(view.ImageShow, sourceChanged);
                    session?.Dispose();
                    view.Dispose();
                });
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogPreviewCannotCommitWhenItsFirstResultFailsOrHasNoPrimaryImage(bool invalidPrimary)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.catalog-no-first-image");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        AlgorithmImageBuffer? invalidImage = invalidPrimary
            ? new AlgorithmImageBuffer(4, 2, 4, AlgorithmImageFormat.Gray8, Enumerable.Repeat((byte)77, 8).ToArray())
            : null;
        ImmediateProvider provider = new(descriptor.Id, 77, () => invalidPrimary
            ? new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("image", "secondary", invalidImage!)],
            }
            : new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure("test_provider_failed", "The provider rejected this request.")],
            });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.StartForCatalogPreview(context));

            using AlgorithmResult result = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));

            Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
            if (invalidPrimary)
            {
                Assert.Contains(result.Failures, failure => failure.Code == "primary_image_contract_violation");
                Assert.True(invalidImage!.IsDisposed);
            }
            WpfTestHost.Invoke(() =>
            {
                Assert.False(session.Commit());
                Assert.Throws<InvalidOperationException>(() => _ = session.PreviewBitmap);
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Same(canonical, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                Assert.Equal(revision, context.ImageRevision);
            });
        }
        finally
        {
            invalidImage?.Dispose();
            WpfTestHost.Invoke(() => { session?.Dispose(); view.Dispose(); });
        }
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("analysis")]
    [InlineData("revision")]
    public async Task CatalogFirstResultCannotPublishAfterCancellationOrTakeover(string change)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.catalog-first-image-superseded");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BlockingProvider provider = new(descriptor.Id, providerStarted, releaseProvider, 91);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
        Guid analysisInvocation = Guid.NewGuid();
        ImageAlgorithmPreviewSession? session = null;
        CancellationTokenSource? analysis = null;
        Task<AlgorithmResult>? pending = null;
        AlgorithmResult? result = null;
        try
        {
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.StartForCatalogPreview(context));
            pending = session.PreviewAsync(AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            (ImageSource source, ImageSource displayed, ImageSource? functionImage, long revision, long sequence) expected = WpfTestHost.Invoke(() =>
            {
                switch (change)
                {
                    case "cancel":
                        Assert.True(session.Cancel());
                        break;
                    case "analysis":
                        analysis = ImageAlgorithmAnalysisSession.Begin(context, context.DocumentInstanceId,
                            context.ImageRevision, Guid.NewGuid(), analysisInvocation);
                        break;
                    case "revision":
                        WriteableBitmap source = Assert.IsType<WriteableBitmap>(context.ViewBitmapSource);
                        source.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
                        view.NotifySourcePixelsChanged();
                        break;
                }
                return (context.ViewBitmapSource, context.ImageShow.Source, context.FunctionImage, context.ImageRevision, context.AlgorithmClaimSequence);
            });
            releaseProvider.TrySetResult(true);
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(AlgorithmResultStatus.Superseded, result.Status);
            Assert.NotNull(provider.ProducedImage);
            Assert.True(provider.ProducedImage.IsDisposed);
            WpfTestHost.Invoke(() =>
            {
                Assert.False(session.Commit());
                Assert.Throws<InvalidOperationException>(() => _ = session.PreviewBitmap);
                Assert.Same(expected.source, context.ViewBitmapSource);
                Assert.Same(expected.displayed, context.ImageShow.Source);
                Assert.Same(expected.functionImage, context.FunctionImage);
                Assert.Equal(expected.revision, context.ImageRevision);
                Assert.Equal(expected.sequence, context.AlgorithmClaimSequence);
                if (analysis != null)
                {
                    Assert.False(analysis.IsCancellationRequested);
                    Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, context.DocumentInstanceId, context.ImageRevision, analysisInvocation));
                }
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            try
            {
                if (result == null && pending != null) result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                result?.Dispose();
                WpfTestHost.Invoke(() =>
                {
                    ImageAlgorithmAnalysisSession.Release(context, analysisInvocation);
                    session?.Dispose();
                    view.Dispose();
                });
                analysis?.Dispose();
            }
        }
    }

    [Fact]
    public async Task PreviewOffRestoresCanonicalAndRejectsLateInFlightResult()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-off-late-result");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new BlockingProvider(descriptor.Id, providerStarted, releaseProvider, 91)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        Task<AlgorithmResult>? preview = null;
        AlgorithmResult? late = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            preview = session.PreviewAsync(AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            bool restoredImmediately = WpfTestHost.Invoke(() =>
            {
                session.ShowOriginal();
                return ReferenceEquals(canonical, context.ImageShow.Source) && context.FunctionImage == null;
            });
            releaseProvider.TrySetResult(true);
            late = await preview.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(restoredImmediately, "Turning preview off must restore the canonical host before the in-flight provider returns.");
            Assert.Equal(AlgorithmResultStatus.Superseded, late.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Same(canonical, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                Assert.Equal(revision, context.ImageRevision);
                Assert.Equal((byte)0, Pixel((BitmapSource)canonical));
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            if (late == null && preview != null) late = await preview.WaitAsync(TimeSpan.FromSeconds(5));
            late?.Dispose();
            WpfTestHost.Invoke(() => { session?.Dispose(); view.Dispose(); });
        }
    }

    [Fact]
    public async Task DisposedPreviewRetainsInFlightInputUntilRunnerExitsWithoutChangingTheNewSource()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.disposed-preview-input-lifetime");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AlgorithmInput? submittedInput = null;
        byte[]? observedPixels = null;
        BlockingProvider provider = new(descriptor.Id, providerStarted, releaseProvider, 91,
            input =>
            {
                submittedInput = input;
                observedPixels = input.Image.Data.ToArray();
            });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        Task<AlgorithmResult>? preview = null;
        AlgorithmResult? late = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long sourceRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            preview = session.PreviewAsync(AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            (ImageSource source, ImageSource displayed, ImageSource? functionImage, long revision) snapshot = WpfTestHost.Invoke(() =>
            {
                session.Dispose();
                WriteableBitmap replacement = new(4, 2, 96, 96, PixelFormats.Gray8, null);
                replacement.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
                view.SetImageSource(replacement, enableEditorImageServices: false, configureDefaultLayerController: false);
                return (context.ViewBitmapSource, context.ImageShow.Source, context.FunctionImage, context.ImageRevision);
            });

            releaseProvider.TrySetResult(true);
            late = await preview.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, observedPixels);
            Assert.NotNull(submittedInput);
            Assert.Equal("source", submittedInput.Name);
            Assert.Equal(sourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), submittedInput.SourceRevision);
            Assert.Equal("encoded-device-values", submittedInput.ColorSpace);
            Assert.Equal(AlgorithmInputOwnership.Transferred, submittedInput.Ownership);
            Assert.True(submittedInput.Image.IsDisposed);
            Assert.NotNull(provider.ProducedImage);
            Assert.True(provider.ProducedImage.IsDisposed);
            Assert.Equal(AlgorithmResultStatus.Superseded, late.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(snapshot.source, context.ViewBitmapSource);
                Assert.Same(snapshot.displayed, context.ImageShow.Source);
                Assert.Same(snapshot.functionImage, context.FunctionImage);
                Assert.Equal(snapshot.revision, context.ImageRevision);
                Assert.Equal((byte)42, Pixel((BitmapSource)context.ViewBitmapSource));
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            if (late == null && preview != null) late = await preview.WaitAsync(TimeSpan.FromSeconds(5));
            late?.Dispose();
            WpfTestHost.Invoke(() => { session?.Dispose(); view.Dispose(); });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterBitmapFillRejectsPublicationAndDisposesTheProviderImage(bool analysisTakeover)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-cancel-after-fill");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TaskCompletionSource<bool> providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BlockingProvider provider = new(descriptor.Id, providerStarted, releaseProvider, 91);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
        using CancellationTokenSource callerCancellation = new();
        ImageAlgorithmPreviewSession? session = null;
        CancellationTokenSource? analysis = null;
        Guid analysisInvocation = Guid.NewGuid();
        Task<AlgorithmResult>? preview = null;
        AlgorithmResult? result = null;
        int publicationAttempts = 0;
        try
        {
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            WriteableBitmap initialPreview = session.PreviewBitmap;
            WpfTestHost.Invoke(() => view.AlgorithmPreviewPublicationHook = _ =>
            {
                // The real publication boundary follows the completed worker fill and precedes
                // TakeBitmap. Cancellation must remain linked through this boundary.
                publicationAttempts++;
                if (analysisTakeover)
                    analysis = ImageAlgorithmAnalysisSession.Begin(context, context.DocumentInstanceId,
                        context.ImageRevision, Guid.NewGuid(), analysisInvocation);
                else
                    callerCancellation.Cancel();
            });
            preview = session.PreviewAsync(AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()),
                cancellationToken: callerCancellation.Token);
            await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseProvider.TrySetResult(true);
            result = await preview.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, publicationAttempts);
            Assert.Equal(analysisTakeover ? AlgorithmResultStatus.Superseded : AlgorithmResultStatus.Cancelled, result.Status);
            Assert.NotNull(provider.ProducedImage);
            Assert.True(provider.ProducedImage.IsDisposed);
            Assert.Empty(result.Artifacts);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Equal(revision, context.ImageRevision);
                Assert.Equal((byte)0, Pixel((BitmapSource)canonical));
                Assert.Same(initialPreview, session.PreviewBitmap);
                Assert.Same(analysisTakeover ? canonical : initialPreview, context.ImageShow.Source);
                if (analysisTakeover)
                {
                    Assert.Null(context.FunctionImage);
                    Assert.NotNull(analysis);
                    Assert.False(analysis.IsCancellationRequested);
                    Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, context.DocumentInstanceId, revision, analysisInvocation));
                }
                else
                {
                    Assert.Same(initialPreview, context.FunctionImage);
                }
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            if (result == null && preview != null) result = await preview.WaitAsync(TimeSpan.FromSeconds(5));
            result?.Dispose();
            WpfTestHost.Invoke(() =>
            {
                view.AlgorithmPreviewPublicationHook = null;
                if (analysis != null) ImageAlgorithmAnalysisSession.Release(context, analysisInvocation);
                session?.Dispose();
                view.Dispose();
            });
            analysis?.Dispose();
        }
    }

    [Fact]
    public async Task PreviewOffAllowsSameSessionToPreviewAgainAndRecalculateBeforeOneCommit()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-off-resume-apply");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 77)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            using AlgorithmResult first = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            Assert.Equal(AlgorithmResultStatus.Succeeded, first.Status);

            WpfTestHost.Invoke(session.ShowOriginal);
            using AlgorithmResult resumed = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            Assert.Equal(AlgorithmResultStatus.Succeeded, resumed.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(session.PreviewBitmap, context.ImageShow.Source);
                Assert.Equal((byte)77, Pixel(session.PreviewBitmap));
                Assert.Equal(revision, context.ImageRevision);
                session.ShowOriginal();
            });

            // Apply always recomputes with the current parameters, including while Preview is off.
            AlgorithmInvocation applyInvocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters());
            using AlgorithmResult apply = await session.PreviewAsync(applyInvocation);
            Assert.Equal(AlgorithmResultStatus.Succeeded, apply.Status);
            Assert.True(session.IsCurrent(applyInvocation.InvocationId));
            WpfTestHost.Invoke(() =>
            {
                Assert.True(session.Commit());
                Assert.Equal(revision + 1, context.ImageRevision);
                Assert.Equal((byte)77, Pixel((BitmapSource)context.ViewBitmapSource));
                Assert.Same(context.ViewBitmapSource, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                Assert.False(session.Commit());
                Assert.Equal(revision + 1, context.ImageRevision);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { session?.Dispose(); view.Dispose(); });
        }
    }

    [Theory]
    [InlineData("newer-session")]
    [InlineData("source-replaced")]
    [InlineData("revision-changed")]
    public async Task PreviewOffFromSupersededSessionCannotRestoreOverCurrentImage(string change)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-off-superseded-session");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 77)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        ImageAlgorithmPreviewSession? newer = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            using AlgorithmResult preview = await session.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));
            Assert.Equal(AlgorithmResultStatus.Succeeded, preview.Status);

            WpfTestHost.Invoke(() =>
            {
                switch (change)
                {
                    case "newer-session":
                        newer = ImageAlgorithmPreviewSession.Start(context);
                        break;
                    case "source-replaced":
                        WriteableBitmap replacement = new(4, 2, 96, 96, PixelFormats.Gray8, null);
                        replacement.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
                        view.SetImageSource(replacement, enableEditorImageServices: false, configureDefaultLayerController: false);
                        break;
                    case "revision-changed":
                        WriteableBitmap canonical = Assert.IsType<WriteableBitmap>(context.ViewBitmapSource);
                        canonical.WritePixels(new Int32Rect(0, 0, 4, 2), Enumerable.Repeat((byte)42, 8).ToArray(), 4, 0);
                        view.NotifySourcePixelsChanged();
                        break;
                }
                ImageSource source = context.ViewBitmapSource;
                ImageSource displayed = context.ImageShow.Source;
                ImageSource? functionImage = context.FunctionImage;
                Guid document = context.DocumentInstanceId;
                long revision = context.ImageRevision;
                byte pixel = Pixel((BitmapSource)source);

                session.ShowOriginal();

                Assert.Same(source, context.ViewBitmapSource);
                Assert.Same(displayed, context.ImageShow.Source);
                Assert.Same(functionImage, context.FunctionImage);
                Assert.Equal(document, context.DocumentInstanceId);
                Assert.Equal(revision, context.ImageRevision);
                Assert.Equal(pixel, Pixel((BitmapSource)source));
                if (newer != null) Assert.True(newer.OwnsHostPreview);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { session?.Dispose(); newer?.Dispose(); view.Dispose(); });
        }
    }

    [Fact]
    public async Task PreviewOffDuringHostClaimAcceptanceRejectsInvocationBeforeSessionReceivesClaim()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.preview-off-claim-handoff");
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ImmediateProvider(descriptor.Id, 77)], scheduler);
        ImageView view = CreateImageView(runtime);
        ImageAlgorithmPreviewSession? session = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            ImageSource canonical = WpfTestHost.Invoke(() => context.ViewBitmapSource);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            session = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters());
            int previewOffCount = 0;
            WpfTestHost.Invoke(() => view.AlgorithmPreviewClaimAcceptedHook = claim =>
            {
                if (claim.InvocationId != invocation.InvocationId) return;
                // The host owns this claim, but PreviewAsync has not yet received and stored it.
                previewOffCount++;
                session.ShowOriginal();
            });

            using AlgorithmResult result = await Task.Run(() => session.PreviewAsync(invocation));

            Assert.Equal(1, previewOffCount);
            Assert.Equal(AlgorithmResultStatus.Superseded, result.Status);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(canonical, context.ViewBitmapSource);
                Assert.Same(canonical, context.ImageShow.Source);
                Assert.Null(context.FunctionImage);
                Assert.Equal(revision, context.ImageRevision);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { view.AlgorithmPreviewClaimAcceptedHook = null; session?.Dispose(); view.Dispose(); });
        }
    }

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

    private sealed class ImmediateProvider(AlgorithmId algorithmId, byte output, Func<AlgorithmResult>? createResult = null) : IImageAlgorithmProvider
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
            => ValueTask.FromResult(createResult?.Invoke() ?? Result(output));
    }

    private sealed class BlockingProvider(
        AlgorithmId algorithmId,
        TaskCompletionSource<bool> started,
        TaskCompletionSource<bool> release,
        byte output,
        Action<AlgorithmInput>? inspectInputAfterRelease = null) : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = MetadataFor($"blocking-{algorithmId.Value}");

        public AlgorithmImageBuffer? ProducedImage { get; private set; }

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
            inspectInputAfterRelease?.Invoke(context.Inputs[0]);
            AlgorithmResult result = Result(output);
            ProducedImage = result.Artifacts.OfType<AlgorithmImageArtifact>().Single().Image;
            return result;
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
