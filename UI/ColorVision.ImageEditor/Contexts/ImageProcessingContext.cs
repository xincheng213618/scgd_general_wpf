using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Operations;
using ColorVision.ImageEditor.Presentation;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor
{
    internal enum ImageDocumentMutationKind
    {
        SourcePixelsChanged,
        ImageSourceReplaced,
        ImageCleared,
    }

    public sealed class ImageProcessingContext
    {
        private readonly ImageProcessingContextBinding _binding;
        private readonly ImageOperationCoordinator _operations;

        internal ImageProcessingContext(
            ImageViewConfig config,
            DrawCanvas imageShow,
            Dispatcher dispatcher,
            ImageProcessingContextBinding binding)
            : this(config, imageShow, dispatcher, binding, ImageAlgorithmPlatform.Runtime)
        {
        }

        internal ImageProcessingContext(
            ImageViewConfig config,
            DrawCanvas imageShow,
            Dispatcher dispatcher,
            ImageProcessingContextBinding binding,
            AlgorithmRuntime algorithmRuntime,
            ImagePresentation? presentation = null)
        {
            ArgumentNullException.ThrowIfNull(algorithmRuntime);
            Config = config;
            ImageShow = imageShow;
            Dispatcher = dispatcher;
            _binding = binding;
            Presentation = presentation ?? new ImagePresentation(imageShow, binding);
            _operations = new ImageOperationCoordinator(this, algorithmRuntime, binding);
            DisplayEffects = new ImageDisplayEffects(this);
            StreamPresentation = new ImageStreamPresentation(this);
        }

        public ImagePresentation Presentation { get; }

        public ImageDisplayEffects DisplayEffects { get; }

        public ImageStreamPresentation StreamPresentation { get; }

        public ImageViewConfig Config { get; }

        public DrawCanvas ImageShow { get; }

        public Dispatcher Dispatcher { get; }

        public AlgorithmRuntime AlgorithmRuntime => _operations.Runtime;

        public AlgorithmOverlayStore AlgorithmOverlays => _operations.AlgorithmOverlays;

        public bool IsInitialized => _binding.IsInitialized();

        public Guid DocumentInstanceId => _binding.GetDocumentInstanceId();

        public bool IsDisposed => _binding.IsDisposed();

        public long ImageRevision => _binding.GetImageRevision();

        internal event EventHandler? DocumentScopeChanged;

        public ImageFrameLease? AcquireImageFrame()
        {
            return _binding.AcquireImageFrame();
        }

        public bool IsCurrentImageRevision(long revision)
        {
            return _binding.IsCurrentImageRevision(revision);
        }

        public void NotifySourcePixelsChanged()
        {
            _binding.NotifySourcePixelsChanged();
        }

        /// <summary>Commits replacement pixels without reopening the source or resetting view preferences.</summary>
        public void CommitSourcePixels(ImageSource source)
        {
            _binding.CommitSourcePixels(source);
        }

        public ImageSource FunctionImage
        {
            get => Presentation.FunctionImage!;
            [param: AllowNull]
            set => Presentation.FunctionImage = value;
        }

        public ImageSource ViewBitmapSource
        {
            get => _binding.GetViewBitmapSource()!;
            [param: AllowNull]
            set => _binding.SetViewBitmapSource(value);
        }

        public int GetSelectedLayerSourceChannelIndex()
        {
            return _binding.GetSelectedLayerSourceChannelIndex();
        }

        public void SetImageSource(ImageSource imageSource)
        {
            _binding.SetImageSource(imageSource);
        }

        internal bool TryBeginAlgorithmPreviewSession(
            Guid sessionId, Guid documentInstanceId, long sourceRevision, out AlgorithmInvocationClaim claim)
            => _operations.TryBeginAlgorithmPreviewSession(sessionId, documentInstanceId, sourceRevision, out claim);

        internal bool TryBeginAlgorithmPreviewSession(
            Guid sessionId, Guid documentInstanceId, long sourceRevision,
            Action previewRestore, Action previewPublication, out AlgorithmInvocationClaim claim)
            => _operations.TryBeginAlgorithmPreviewSession(sessionId, documentInstanceId, sourceRevision, previewRestore, previewPublication, out claim);

        internal bool TryBeginAlgorithmPreviewInvocation(
            Guid sessionId, Guid documentInstanceId, long sourceRevision, Guid invocationId,
            CancellationTokenSource cancellation, out AlgorithmInvocationClaim claim)
            => _operations.TryBeginAlgorithmPreviewInvocation(sessionId, documentInstanceId, sourceRevision, invocationId, cancellation, out claim);

        internal bool TryBeginAlgorithmPreviewInvocation(
            Guid sessionId, Guid documentInstanceId, long sourceRevision, Guid invocationId,
            CancellationTokenSource cancellation, Action? previewRestore, out AlgorithmInvocationClaim claim)
            => _operations.TryBeginAlgorithmPreviewInvocation(sessionId, documentInstanceId, sourceRevision, invocationId, cancellation, previewRestore, out claim);

        internal bool TryBeginAlgorithmAnalysisInvocation(
            Guid ownerId, Guid documentInstanceId, long sourceRevision, Guid invocationId,
            CancellationTokenSource cancellation, out AlgorithmInvocationClaim claim)
            => _operations.TryBeginAlgorithmAnalysisInvocation(ownerId, documentInstanceId, sourceRevision, invocationId, cancellation, out claim);

        internal bool TryBeginAlgorithmAnalysisInvocation(
            Guid ownerId, Guid documentInstanceId, long sourceRevision, Guid invocationId,
            CancellationTokenSource cancellation, Action<AlgorithmInvocationClaim>? onAccepted, out AlgorithmInvocationClaim claim)
            => _operations.TryBeginAlgorithmAnalysisInvocation(ownerId, documentInstanceId, sourceRevision, invocationId, cancellation, onAccepted, out claim);

        internal bool IsCurrentAlgorithmInvocation(AlgorithmInvocationClaim claim)
            => _operations.IsCurrentAlgorithmInvocation(claim);

        internal bool CompleteAlgorithmInvocationRun(AlgorithmInvocationClaim claim, CancellationTokenSource cancellation)
            => _operations.CompleteAlgorithmInvocationRun(claim, cancellation);

        internal bool TryReleaseAlgorithmInvocation(AlgorithmInvocationClaim claim)
            => _operations.TryReleaseAlgorithmInvocation(claim);

        internal bool HasActiveAlgorithmPreview => _operations.HasActiveAlgorithmPreview;

        internal void SupersedePreviewForDisplaySelection()
        {
            DisplayEffects.Invalidate();
            _operations.SupersedePreviewForDisplaySelection();
        }

        internal long AlgorithmPreviewGeneration => _operations.AlgorithmPreviewGeneration;

        internal bool OwnsAlgorithmPreviewClaim(AlgorithmInvocationClaim claim)
            => _operations.OwnsAlgorithmPreviewClaim(claim);

        internal bool TryPublishAlgorithmPreview(AlgorithmInvocationClaim claim, Action publication)
            => _operations.TryPublishAlgorithmPreview(claim, publication);

        internal bool TryCompleteAlgorithmPreview(
            AlgorithmInvocationClaim claim, Action? publication = null, Action? afterConsumption = null)
            => _operations.TryCompleteAlgorithmPreview(claim, publication, afterConsumption);

        internal void BeforeAlgorithmPreviewCommit(AlgorithmInvocationClaim claim)
            => _operations.BeforeAlgorithmPreviewCommit(claim);

        internal bool TryCancelAlgorithmPreview(AlgorithmInvocationClaim claim, Action? cancellationPublication = null)
            => _operations.TryCancelAlgorithmPreview(claim, cancellationPublication);

        internal void InvalidateForDocumentMutation(ImageDocumentMutationKind mutationKind, long previousRevision, long currentRevision)
            => _operations.InvalidateForDocumentMutation(mutationKind, previousRevision, currentRevision);

        internal void NotifyDocumentScopeChanged() => DocumentScopeChanged?.Invoke(this, EventArgs.Empty);

        internal bool TryRegisterAlgorithmOverlay(
            AlgorithmOverlayArtifact artifact, Visual visual, Guid documentInstanceId, long sourceRevision,
            [NotNullWhen(true)] out IAlgorithmOverlayRegistration? registration)
            => _operations.TryRegisterAlgorithmOverlay(artifact, visual, documentInstanceId, sourceRevision, out registration);

        internal IReadOnlyList<AlgorithmOverlayRegistrationSnapshot> SnapshotAlgorithmOverlayRegistrations()
            => _operations.SnapshotAlgorithmOverlayRegistrations();

        internal void DisposeAlgorithmOverlays() => _operations.DisposeAlgorithmOverlays();

        public void UpdateZoomAndScale()
        {
            _binding.UpdateZoomAndScale();
        }
    }

    internal sealed class ImageProcessingContextBinding
    {
        public required Func<bool> IsInitialized { get; init; }

        public required Func<Guid> GetDocumentInstanceId { get; init; }

        public required Func<bool> IsDisposed { get; init; }

        public required Func<long> GetImageRevision { get; init; }

        public required Func<ImageFrameLease?> AcquireImageFrame { get; init; }

        public required Func<long, bool> IsCurrentImageRevision { get; init; }

        public required Action NotifySourcePixelsChanged { get; init; }

        public required Action<ImageSource> CommitSourcePixels { get; init; }

        public required Func<ImageSource?> GetViewBitmapSource { get; init; }

        public required Action<ImageSource?> SetViewBitmapSource { get; init; }

        public required Func<int> GetSelectedLayerSourceChannelIndex { get; init; }

        public required Action<ImageSource> SetImageSource { get; init; }

        public required Action UpdateZoomAndScale { get; init; }

        internal Action<AlgorithmInvocationClaim>? BeforeAlgorithmClaimStateUpdate { get; init; }

        internal Action<AlgorithmInvocationClaim>? BeforeAlgorithmPreviewPublication { get; init; }

        internal Action<AlgorithmInvocationClaim>? BeforeAlgorithmPreviewCommit { get; init; }

        internal Action<AlgorithmInvocationClaim>? AfterAlgorithmPreviewClaimAccepted { get; init; }
    }
}
