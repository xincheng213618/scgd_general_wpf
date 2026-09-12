using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Operations
{
    /// <summary>Coordinates algorithm ownership, preview transactions and overlays for one editor session.</summary>
    internal sealed class ImageOperationCoordinator
    {
        private readonly ImageProcessingContext _host;
        private readonly ImageProcessingContextBinding _binding;
        private readonly AlgorithmInvocationCoordinator _algorithmInvocationCoordinator;
        private readonly AlgorithmOverlayManager _algorithmOverlayManager;
        private readonly object _algorithmPreviewSync = new();
        private AlgorithmInvocationClaim? _activeAlgorithmPreviewClaim;
        private Action? _activeAlgorithmPreviewRestore;
        private long _algorithmClaimSequence;
        private long _algorithmPreviewGeneration;

        internal ImageOperationCoordinator(ImageProcessingContext host, AlgorithmRuntime runtime, ImageProcessingContextBinding binding)
        {
            _host = host;
            _binding = binding;
            Runtime = runtime;
            _algorithmInvocationCoordinator = runtime.InvocationCoordinator;
            _algorithmOverlayManager = new AlgorithmOverlayManager(host.ImageShow);
        }

        internal AlgorithmRuntime Runtime { get; }

        internal AlgorithmOverlayStore AlgorithmOverlays => _algorithmOverlayManager.Artifacts;

        internal bool TryBeginAlgorithmPreviewSession(
            Guid sessionId,
            Guid documentInstanceId,
            long sourceRevision,
            out AlgorithmInvocationClaim claim)
            => TryClaimAlgorithmInvocation(
                new AlgorithmInvocationScope(documentInstanceId, sourceRevision),
                sessionId,
                sessionId,
                cancellation: null,
                isPreview: true,
                previewRestore: null,
                onAccepted: null,
                out claim);

        internal bool TryBeginAlgorithmPreviewSession(
            Guid sessionId,
            Guid documentInstanceId,
            long sourceRevision,
            Action previewRestore,
            Action previewPublication,
            out AlgorithmInvocationClaim claim)
        {
            ArgumentNullException.ThrowIfNull(previewRestore);
            ArgumentNullException.ThrowIfNull(previewPublication);
            return TryClaimAlgorithmInvocation(
                new AlgorithmInvocationScope(documentInstanceId, sourceRevision),
                sessionId,
                sessionId,
                cancellation: null,
                isPreview: true,
                previewRestore,
                _ => previewPublication(),
                out claim);
        }

        internal bool TryBeginAlgorithmPreviewInvocation(
            Guid sessionId,
            Guid documentInstanceId,
            long sourceRevision,
            Guid invocationId,
            CancellationTokenSource cancellation,
            out AlgorithmInvocationClaim claim)
            => TryBeginAlgorithmPreviewInvocation(
                sessionId,
                documentInstanceId,
                sourceRevision,
                invocationId,
                cancellation,
                previewRestore: null,
                out claim);

        internal bool TryBeginAlgorithmPreviewInvocation(
            Guid sessionId,
            Guid documentInstanceId,
            long sourceRevision,
            Guid invocationId,
            CancellationTokenSource cancellation,
            Action? previewRestore,
            out AlgorithmInvocationClaim claim)
        {
            ArgumentNullException.ThrowIfNull(cancellation);
            return TryClaimAlgorithmInvocation(
                new AlgorithmInvocationScope(documentInstanceId, sourceRevision),
                sessionId,
                invocationId,
                cancellation,
                isPreview: true,
                previewRestore,
                onAccepted: null,
                out claim);
        }

        internal bool TryBeginAlgorithmAnalysisInvocation(
            Guid ownerId,
            Guid documentInstanceId,
            long sourceRevision,
            Guid invocationId,
            CancellationTokenSource cancellation,
            out AlgorithmInvocationClaim claim)
            => TryBeginAlgorithmAnalysisInvocation(
                ownerId,
                documentInstanceId,
                sourceRevision,
                invocationId,
                cancellation,
                onAccepted: null,
                out claim);

        internal bool TryBeginAlgorithmAnalysisInvocation(
            Guid ownerId,
            Guid documentInstanceId,
            long sourceRevision,
            Guid invocationId,
            CancellationTokenSource cancellation,
            Action<AlgorithmInvocationClaim>? onAccepted,
            out AlgorithmInvocationClaim claim)
        {
            ArgumentNullException.ThrowIfNull(cancellation);
            return TryClaimAlgorithmInvocation(
                new AlgorithmInvocationScope(documentInstanceId, sourceRevision),
                ownerId,
                invocationId,
                cancellation,
                isPreview: false,
                previewRestore: null,
                onAccepted,
                out claim);
        }

        internal bool IsCurrentAlgorithmInvocation(AlgorithmInvocationClaim claim)
            => _algorithmInvocationCoordinator.IsCurrent(claim);

        internal bool CompleteAlgorithmInvocationRun(
            AlgorithmInvocationClaim claim,
            CancellationTokenSource cancellation)
            => _algorithmInvocationCoordinator.CompleteRun(claim, cancellation);

        internal bool TryReleaseAlgorithmInvocation(AlgorithmInvocationClaim claim)
            => _algorithmInvocationCoordinator.TryRelease(claim);

        internal bool HasActiveAlgorithmPreview
        {
            get
            {
                AlgorithmInvocationClaim? claim;
                lock (_algorithmPreviewSync) claim = _activeAlgorithmPreviewClaim;
                return claim.HasValue && _algorithmInvocationCoordinator.IsCurrent(claim.Value);
            }
        }

        internal long AlgorithmPreviewGeneration
        {
            get
            {
                lock (_algorithmPreviewSync) return _algorithmPreviewGeneration;
            }
        }

        internal bool OwnsAlgorithmPreviewClaim(AlgorithmInvocationClaim claim)
        {
            lock (_algorithmPreviewSync) return _activeAlgorithmPreviewClaim == claim;
        }

        internal void SupersedePreviewForDisplaySelection()
        {
            AlgorithmInvocationClaim? claim;
            lock (_algorithmPreviewSync) claim = _activeAlgorithmPreviewClaim;
            if (claim.HasValue) TryCancelAlgorithmPreview(claim.Value);
        }

        internal bool TryPublishAlgorithmPreview(AlgorithmInvocationClaim claim, Action publication)
        {
            ArgumentNullException.ThrowIfNull(publication);
            return InvokeOnDispatcher(() =>
            {
                bool published = false;
                bool current = _algorithmInvocationCoordinator.TryMutateCurrent(claim, () =>
                {
                    long expectedClaimSequence;
                    long expectedGeneration;
                    Action? expectedRestore;
                    lock (_algorithmPreviewSync)
                    {
                        if (_activeAlgorithmPreviewClaim != claim || !IsCurrentScope(claim.Scope)) return;
                        expectedClaimSequence = _algorithmClaimSequence;
                        expectedGeneration = _algorithmPreviewGeneration;
                        expectedRestore = _activeAlgorithmPreviewRestore;
                    }

                    AlgorithmHostState host = CaptureHostState();
                    try
                    {
                        _binding.BeforeAlgorithmPreviewPublication?.Invoke(claim);
                        if (!CanRollbackHostState(claim, expectedClaimSequence, expectedGeneration, claim, expectedRestore))
                            return;
                        publication();
                        published = true;
                    }
                    catch
                    {
                        if (CanRollbackHostState(claim, expectedClaimSequence, expectedGeneration, claim, expectedRestore))
                            RestoreHostState(host);
                        throw;
                    }
                });
                return current && published;
            });
        }

        internal bool TryCompleteAlgorithmPreview(
            AlgorithmInvocationClaim claim,
            Action? publication = null,
            Action? afterConsumption = null)
            => TryConsumeAlgorithmPreview(claim, publication, restoreCanonical: false, afterConsumption);

        internal void BeforeAlgorithmPreviewCommit(AlgorithmInvocationClaim claim)
            => _binding.BeforeAlgorithmPreviewCommit?.Invoke(claim);

        internal bool TryCancelAlgorithmPreview(AlgorithmInvocationClaim claim, Action? cancellationPublication = null)
            => TryConsumeAlgorithmPreview(claim, cancellationPublication, restoreCanonical: true, afterConsumption: null);

        private bool TryConsumeAlgorithmPreview(
            AlgorithmInvocationClaim claim,
            Action? publication,
            bool restoreCanonical,
            Action? afterConsumption)
        {
            return InvokeOnDispatcher(() =>
            {
                Action? restore;
                Action? expectedRestore;
                long expectedClaimSequence;
                long expectedGeneration;
                lock (_algorithmPreviewSync)
                {
                    if (_activeAlgorithmPreviewClaim != claim) return false;
                    expectedRestore = _activeAlgorithmPreviewRestore;
                    restore = restoreCanonical ? expectedRestore : null;
                    expectedClaimSequence = _algorithmClaimSequence;
                    expectedGeneration = _algorithmPreviewGeneration;
                }

                AlgorithmHostState host = CaptureHostState();
                bool mutated = false;
                bool released = _algorithmInvocationCoordinator.TryRelease(claim, () =>
                {
                    lock (_algorithmPreviewSync)
                    {
                        if (_activeAlgorithmPreviewClaim != claim)
                            throw new InvalidOperationException("Preview ownership changed during publication.");
                    }

                    try
                    {
                        restore?.Invoke();
                        publication?.Invoke();
                        lock (_algorithmPreviewSync)
                        {
                            if (_activeAlgorithmPreviewClaim != claim
                                || _algorithmClaimSequence != expectedClaimSequence
                                || _algorithmPreviewGeneration != expectedGeneration)
                            {
                                throw new InvalidOperationException("Preview ownership changed during publication.");
                            }
                            _activeAlgorithmPreviewClaim = null;
                            _activeAlgorithmPreviewRestore = null;
                            _algorithmPreviewGeneration++;
                        }
                        mutated = true;
                    }
                    catch
                    {
                        if (CanRollbackHostState(claim, expectedClaimSequence, expectedGeneration, claim, expectedRestore))
                            RestoreHostState(host);
                        throw;
                    }
                });
                bool consumed = released && mutated;
                if (consumed) afterConsumption?.Invoke();
                return consumed;
            });
        }

        internal void InvalidateForDocumentMutation(
            ImageDocumentMutationKind mutationKind,
            long previousRevision,
            long currentRevision)
        {
            InvokeOnDispatcher(() =>
            {
                Guid documentInstanceId = _host.DocumentInstanceId;
                bool preservesNewerPreview;
                lock (_algorithmPreviewSync)
                {
                    preservesNewerPreview = _activeAlgorithmPreviewClaim is AlgorithmInvocationClaim active
                        && active.Scope.DocumentInstanceId == documentInstanceId
                        && active.Scope.SourceRevision >= currentRevision;
                    if (!preservesNewerPreview)
                    {
                        _algorithmPreviewGeneration++;
                        _activeAlgorithmPreviewClaim = null;
                        _activeAlgorithmPreviewRestore = null;
                    }
                }

                Action finishAnalysisInvalidation = ImageAlgorithmAnalysisSession.DetachForDocumentMutation(
                    _host,
                    documentInstanceId,
                    currentRevision);

                if (mutationKind == ImageDocumentMutationKind.SourcePixelsChanged)
                {
                    // In-place producers (video/realtime/native commit) have already updated
                    // ViewBitmapSource. Publish that canonical source while invalidating the old
                    // revision so a stale preview cannot remain visible or republish later.
                    if (!preservesNewerPreview)
                    {
                        _host.Presentation.RestoreSourceForDocumentMutation();
                    }
                    _algorithmOverlayManager.OnSourceRevisionChanged(documentInstanceId, _host.ImageRevision);
                }
                else
                {
                    _algorithmOverlayManager.ClearDocumentBeforeRevision(documentInstanceId, currentRevision);
                }

                // Remove every old-revision claim only after the host/session state has been
                // invalidated. Cancellation callbacks are synchronous and may legitimately
                // install a claim for the already-advanced source revision; no old-generation
                // cleanup is allowed to run after those callbacks.
                _algorithmInvocationCoordinator.InvalidateDocumentRevisionsBefore(documentInstanceId, currentRevision);
                finishAnalysisInvalidation();
                _host.NotifyDocumentScopeChanged();
                return true;
            });
        }

        internal bool TryRegisterAlgorithmOverlay(
            AlgorithmOverlayArtifact artifact,
            Visual visual,
            Guid documentInstanceId,
            long sourceRevision,
            [NotNullWhen(true)] out IAlgorithmOverlayRegistration? registration)
        {
            _host.Dispatcher.VerifyAccess();
            registration = null;
            if (_host.DocumentInstanceId != documentInstanceId || !_host.IsCurrentImageRevision(sourceRevision) || _host.IsDisposed)
                return false;

            registration = _algorithmOverlayManager.Register(
                artifact,
                visual,
                documentInstanceId,
                sourceRevision);
            return true;
        }

        internal IReadOnlyList<AlgorithmOverlayRegistrationSnapshot> SnapshotAlgorithmOverlayRegistrations()
            => _algorithmOverlayManager.SnapshotRegistrations();

        internal void DisposeAlgorithmOverlays() => _algorithmOverlayManager.Dispose();

        private bool TryClaimAlgorithmInvocation(
            AlgorithmInvocationScope scope,
            Guid ownerId,
            Guid invocationId,
            CancellationTokenSource? cancellation,
            bool isPreview,
            Action? previewRestore,
            Action<AlgorithmInvocationClaim>? onAccepted,
            out AlgorithmInvocationClaim claim)
        {
            if (!_host.Dispatcher.CheckAccess())
            {
                (bool Accepted, AlgorithmInvocationClaim Claim) result = _host.Dispatcher.Invoke(() =>
                {
                    bool accepted = TryClaimAlgorithmInvocation(
                        scope,
                        ownerId,
                        invocationId,
                        cancellation,
                        isPreview,
                        previewRestore,
                        onAccepted,
                        out AlgorithmInvocationClaim dispatcherClaim);
                    return (accepted, dispatcherClaim);
                });
                claim = result.Claim;
                return result.Accepted;
            }

            claim = default;
            if (!IsCurrentScope(scope)) return false;

            bool accepted = _algorithmInvocationCoordinator.TryClaim(
                scope,
                ownerId,
                invocationId,
                cancellation,
                candidate =>
                {
                    if (!IsCurrentScope(scope)) return false;
                    _binding.BeforeAlgorithmClaimStateUpdate?.Invoke(candidate);
                    if (!IsCurrentScope(scope)) return false;

                    AlgorithmInvocationClaim? previousPreviewClaim;
                    Action? previousPreviewRestore;
                    long previousClaimSequence;
                    long previousGeneration;
                    lock (_algorithmPreviewSync)
                    {
                        if (candidate.Sequence <= _algorithmClaimSequence || !IsCurrentScope(scope)) return false;
                        previousPreviewClaim = _activeAlgorithmPreviewClaim;
                        previousPreviewRestore = _activeAlgorithmPreviewRestore;
                        previousClaimSequence = _algorithmClaimSequence;
                        previousGeneration = _algorithmPreviewGeneration;
                    }

                    bool previewOwnerChanged = previousPreviewClaim.HasValue
                        && previousPreviewClaim.Value.Scope == scope
                        && (!isPreview || previousPreviewClaim.Value.OwnerId != ownerId);
                    AlgorithmHostState host = CaptureHostState();
                    try
                    {
                        if (previewOwnerChanged) previousPreviewRestore?.Invoke();
                        onAccepted?.Invoke(candidate);

                        lock (_algorithmPreviewSync)
                        {
                            if (_algorithmClaimSequence != previousClaimSequence
                                || _algorithmPreviewGeneration != previousGeneration
                                || _activeAlgorithmPreviewClaim != previousPreviewClaim
                                || !ReferenceEquals(_activeAlgorithmPreviewRestore, previousPreviewRestore)
                                || !IsCurrentScope(scope))
                            {
                                throw new InvalidOperationException("Algorithm host state changed during claim acceptance.");
                            }

                            _algorithmClaimSequence = candidate.Sequence;
                            if (isPreview)
                            {
                                bool ownerChanged = !previousPreviewClaim.HasValue
                                    || previousPreviewClaim.Value.Scope != scope
                                    || previousPreviewClaim.Value.OwnerId != ownerId;
                                _activeAlgorithmPreviewClaim = candidate;
                                _activeAlgorithmPreviewRestore = previewRestore;
                                if (ownerChanged) _algorithmPreviewGeneration++;
                            }
                            else if (previewOwnerChanged)
                            {
                                _activeAlgorithmPreviewClaim = null;
                                _activeAlgorithmPreviewRestore = null;
                                _algorithmPreviewGeneration++;
                            }
                        }
                    }
                    catch
                    {
                        if (CanRollbackHostState(
                                candidate,
                                previousClaimSequence,
                                previousGeneration,
                                previousPreviewClaim,
                                previousPreviewRestore))
                        {
                            RestoreHostState(host);
                        }
                        throw;
                    }
                    return true;
                },
                out claim);
            if (accepted)
            {
                try
                {
                    ImageAlgorithmAnalysisSession.ObserveClaim(_host, claim);
                    if (isPreview) _binding.AfterAlgorithmPreviewClaimAccepted?.Invoke(claim);
                }
                catch
                {
                    // Post-acceptance notifications are part of the host transaction. Release
                    // only this ticket: a re-entrant callback may already have installed a newer
                    // owner, which must remain untouched.
                    if (isPreview) TryCancelAlgorithmPreview(claim);
                    else _algorithmInvocationCoordinator.TryRelease(claim);
                    throw;
                }
            }
            return accepted;
        }

        private bool CanRollbackHostState(
            AlgorithmInvocationClaim candidate,
            long expectedClaimSequence,
            long expectedGeneration,
            AlgorithmInvocationClaim? expectedPreviewClaim,
            Action? expectedPreviewRestore)
        {
            if (!_algorithmInvocationCoordinator.IsCurrent(candidate)) return false;
            lock (_algorithmPreviewSync)
            {
                return _algorithmClaimSequence == expectedClaimSequence
                    && _algorithmPreviewGeneration == expectedGeneration
                    && _activeAlgorithmPreviewClaim == expectedPreviewClaim
                    && ReferenceEquals(_activeAlgorithmPreviewRestore, expectedPreviewRestore);
            }
        }

        private AlgorithmHostState CaptureHostState()
            => new(_host.ViewBitmapSource, _host.Presentation.DisplaySource, _host.FunctionImage);

        private void RestoreHostState(AlgorithmHostState state)
        {
            _host.ViewBitmapSource = state.ViewBitmapSource!;
            _host.Presentation.Publish(state.DisplaySource, state.FunctionImage);
        }

        private T InvokeOnDispatcher<T>(Func<T> action)
            => _host.Dispatcher.CheckAccess() ? action() : _host.Dispatcher.Invoke(action);

        private bool IsCurrentScope(AlgorithmInvocationScope scope)
            => !_host.IsDisposed
                && _host.DocumentInstanceId == scope.DocumentInstanceId
                && _host.IsCurrentImageRevision(scope.SourceRevision);

        private readonly record struct AlgorithmHostState(
            ImageSource? ViewBitmapSource,
            ImageSource? DisplaySource,
            ImageSource? FunctionImage);
    }
}
