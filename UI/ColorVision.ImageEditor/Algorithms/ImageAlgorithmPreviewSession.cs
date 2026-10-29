#pragma warning disable CS8625
using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Owns one ImageView algorithm preview and enforces document/revision/invocation latest-wins semantics.</summary>
    internal sealed class ImageAlgorithmPreviewSession : IDisposable
    {
        private readonly ImageProcessingContext? _image;
        private readonly Guid _sessionId;
        private readonly Guid _documentInstanceId;
        private readonly long _sourceRevision;
        private readonly AlgorithmImageBuffer? _source;
        private readonly BitmapSource _originalSource;
        private readonly object _sync = new();
        private WriteableBitmap? _previewBitmap;
        private AlgorithmInvocationClaim? _previewBitmapClaim;
        private AlgorithmInvocationClaim? _latestClaim;
        private Guid _latestInvocationId;
        private long _previewEpoch;
        private bool _isCompleted;
        private bool _needsRestore;
        private bool _disposed;

        private ImageAlgorithmPreviewSession(
            ImageProcessingContext image,
            AlgorithmImageBuffer source,
            BitmapSource originalSource,
            WriteableBitmap? previewBitmap,
            Guid documentInstanceId,
            long sourceRevision,
            Guid sessionId,
            AlgorithmInvocationClaim claim)
        {
            _image = image;
            _sessionId = sessionId;
            _documentInstanceId = documentInstanceId;
            _sourceRevision = sourceRevision;
            _source = source;
            _originalSource = originalSource;
            _latestClaim = claim;
            _previewBitmap = previewBitmap;
            _previewBitmapClaim = previewBitmap == null ? null : claim;
        }

        // Kept for the low-copy preview regression tests. Holding the BitmapSource avoids
        // a pixel-sized managed snapshot while still allowing exact repeated restoration.
        private ImageAlgorithmPreviewSession(
            ImageProcessingContext image,
            BitmapSource originalSource,
            WriteableBitmap previewBitmap)
        {
            _image = image;
            _sessionId = Guid.Empty;
            _documentInstanceId = image?.DocumentInstanceId ?? Guid.Empty;
            _sourceRevision = image?.ImageRevision ?? 0;
            _originalSource = originalSource ?? throw new ArgumentNullException(nameof(originalSource));
            _previewBitmap = previewBitmap ?? throw new ArgumentNullException(nameof(previewBitmap));
        }

        public WriteableBitmap PreviewBitmap => _previewBitmap
            ?? throw new InvalidOperationException("This session has not produced a bitmap preview.");

        public Guid LatestInvocationId
        {
            get
            {
                lock (_sync) return _latestInvocationId;
            }
        }

        public long SourceRevision => _sourceRevision;

        public bool OwnsHostPreview
        {
            get
            {
                AlgorithmInvocationClaim? claim;
                lock (_sync)
                {
                    if (_disposed || _isCompleted) return false;
                    claim = _latestClaim;
                }
                return claim.HasValue
                    && _image?.OwnsAlgorithmPreviewClaim(claim.Value) == true
                    && _image.IsCurrentAlgorithmInvocation(claim.Value);
            }
        }

        public static ImageAlgorithmPreviewSession Start(ImageProcessingContext image)
            => StartCore(image, createInitialBitmap: true);

        /// <summary>Claims the host and captures input without copying an unused placeholder bitmap.</summary>
        public static ImageAlgorithmPreviewSession StartForCatalogPreview(ImageProcessingContext image)
            => StartCore(image, createInitialBitmap: false);

        private static ImageAlgorithmPreviewSession StartCore(ImageProcessingContext image, bool createInitialBitmap)
        {
            ArgumentNullException.ThrowIfNull(image);
            BitmapSource originalSource = image.ViewBitmapSource as BitmapSource
                ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
            Guid sessionId = Guid.NewGuid();
            Guid documentInstanceId = image.DocumentInstanceId;
            AlgorithmImageBuffer? source = null;
            AlgorithmInvocationClaim? claim = null;
            try
            {
                // The catalog always receives the canonical image representation. The WPF
                // source is retained separately for low-copy, pixel-exact preview restoration.
                (source, long revision) = ImageAlgorithmInputFactory.AcquireCurrentFrame(image);
                if (image.DocumentInstanceId != documentInstanceId || !ReferenceEquals(image.ViewBitmapSource, originalSource))
                    throw new OperationCanceledException("The image changed while the preview session was being captured.");
                WriteableBitmap? preview = createInitialBitmap ? new WriteableBitmap(originalSource) : null;
                if (!image.TryBeginAlgorithmPreviewSession(
                        sessionId,
                        documentInstanceId,
                        revision,
                        previewRestore: () =>
                        {
                            if (image.IsDisposed || image.DocumentInstanceId != documentInstanceId) return;
                            image.Presentation.RestoreSource();
                        },
                        previewPublication: () =>
                        {
                            if (preview == null) image.Presentation.RestoreSource();
                            else image.Presentation.Publish(preview, preview);
                        },
                        out AlgorithmInvocationClaim initialClaim))
                {
                    throw new OperationCanceledException("The image changed before the preview session could start.");
                }
                claim = initialClaim;
                ImageAlgorithmPreviewSession session = new(
                    image,
                    source,
                    originalSource,
                    preview,
                    documentInstanceId,
                    revision,
                    sessionId,
                    initialClaim);
                source = null;
                return session;
            }
            catch
            {
                source?.Dispose();
                if (claim.HasValue) image.TryCancelAlgorithmPreview(claim.Value);
                throw;
            }
        }

        /// <summary>Guards a deferred parameter-window refresh against intervening host operations.</summary>
        public Action GuardDeferredPreview(Action preview)
        {
            ArgumentNullException.ThrowIfNull(preview);
            ImageProcessingContext image = _image
                ?? throw new InvalidOperationException("Deferred previews require an image host.");
            Guid documentId = image.DocumentInstanceId;
            long revision = image.ImageRevision;
            long claimSequence = image.AlgorithmClaimSequence;
            long previewGeneration = image.AlgorithmPreviewGeneration;
            return () =>
            {
                lock (_sync)
                {
                    if (_disposed || _isCompleted) return;
                }
                // Called and executed on the parameter window's Dispatcher. A retained sequence
                // also detects an analysis that accepted a claim and then released it.
                if (!image.IsDisposed && image.DocumentInstanceId == documentId && image.ImageRevision == revision
                    && image.AlgorithmClaimSequence == claimSequence && image.AlgorithmPreviewGeneration == previewGeneration)
                    preview();
            };
        }

        public async Task<AlgorithmResult> PreviewAsync(
            AlgorithmInvocation invocation,
            AlgorithmHostCapabilities requiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            CancellationToken cancellationToken = default)
            => await PreviewWithInputsAsync(invocation, Array.Empty<AlgorithmInput>(), requiredCapabilities, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Executes a source-image preview with additional named inputs. Transferred additional
        /// buffers are consumed on entry, including superseded-before-run paths.
        /// </summary>
        public async Task<AlgorithmResult> PreviewWithInputsAsync(
            AlgorithmInvocation invocation,
            IReadOnlyList<AlgorithmInput> additionalInputs,
            AlgorithmHostCapabilities requiredCapabilities,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(additionalInputs);
            requiredCapabilities |= AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local;
            AlgorithmInput[] additional = additionalInputs.ToArray();
            if (_image == null || _source == null)
            {
                DisposeTransferred(additional);
                throw new InvalidOperationException("This compatibility session has no ImageView catalog context.");
            }
            if (additional.Any(value => string.Equals(value.Name, "source", StringComparison.Ordinal)))
            {
                DisposeTransferred(additional);
                throw new ArgumentException("Additional preview inputs cannot use the reserved 'source' role.", nameof(additionalInputs));
            }

            CancellationTokenSource? linked = null;
            AlgorithmImageBuffer? input = null;
            AlgorithmInvocationClaim claim = default;
            bool claimInstalled = false;
            try
            {
                long previewEpoch;
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (_isCompleted) throw new InvalidOperationException("The preview session has already completed.");
                    previewEpoch = _previewEpoch;
                    // Inputs are immutable. Each run owns a handle that survives session disposal,
                    // while the private canonical pixels are captured only once at Start.
                    input = _source.RetainReadOnly();
                }

                // Never retain the session monitor while entering Dispatcher, Runner, or a host
                // callback. Publication can synchronously query this session from the UI thread.
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                // Claim installation and session handoff share the UI transaction with ShowOriginal.
                // A request prepared before Preview was disabled cannot claim afterwards.
                bool acceptedBySession = _image.Dispatcher.Invoke(() =>
                {
                    lock (_sync)
                    {
                        if (_disposed || _isCompleted || previewEpoch != _previewEpoch) return false;
                    }
                    if (!_image.TryBeginAlgorithmPreviewInvocation(
                            _sessionId,
                            _documentInstanceId,
                            _sourceRevision,
                            invocation.InvocationId,
                            linked,
                            RestoreCanonicalHost,
                            out claim)) return false;
                    claimInstalled = true;
                    lock (_sync)
                    {
                        if (_disposed || _isCompleted || previewEpoch != _previewEpoch
                            || (_latestClaim.HasValue && claim.Sequence <= _latestClaim.Value.Sequence)) return false;
                        _latestClaim = claim;
                        _latestInvocationId = invocation.InvocationId;
                    }
                    return true;
                });
                if (!acceptedBySession)
                {
                    if (claimInstalled) _image.TryCancelAlgorithmPreview(claim);
                    input.Dispose();
                    linked.Dispose();
                    input = null;
                    linked = null;
                    DisposeTransferred(additional);
                    return CreateSupersededResult(invocation);
                }
            }
            catch
            {
                if (claimInstalled) _image.TryCancelAlgorithmPreview(claim);
                input?.Dispose();
                linked?.Dispose();
                DisposeTransferred(additional);
                throw;
            }

            AlgorithmResult? result = null;
            AlgorithmBitmapWriteTarget? target = null;
            bool runnerSubmitted = false;
            try
            {
                try
                {
                    try
                    {
                        AlgorithmInput[] inputs = new AlgorithmInput[additional.Length + 1];
                        inputs[0] = new AlgorithmInput
                        {
                            Name = "source",
                            Image = input!,
                            Ownership = AlgorithmInputOwnership.Transferred,
                            SourceRevision = _sourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ColorSpace = "encoded-device-values",
                        };
                        Array.Copy(additional, 0, inputs, 1, additional.Length);
                        runnerSubmitted = true;
                        result = await _image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
                        {
                            Invocation = invocation,
                            Inputs = inputs,
                            RequiredCapabilities = requiredCapabilities,
                        }, linked!.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        if (!runnerSubmitted)
                        {
                            input?.Dispose();
                            DisposeTransferred(additional);
                        }
                        _image.TryCancelAlgorithmPreview(claim);
                        throw;
                    }
                    if (!IsCurrent(invocation.InvocationId))
                    {
                        AlgorithmResult superseded = CreateSupersededResult(invocation, result.AlgorithmVersion, result.Diagnostics);
                        result.Dispose();
                        return superseded;
                    }
                    if (result.Status != AlgorithmResultStatus.Succeeded) return result;
                    if (!_image.AlgorithmRuntime.Catalog.TryResolve(invocation.AlgorithmId, out AlgorithmDescriptor? descriptor)
                        || descriptor == null)
                    {
                        AlgorithmResult invalid = CreateInvalidPrimaryResult(invocation, result,
                            new AlgorithmPrimaryImageSelection(AlgorithmPrimaryImageSelectionStatus.None, null, 0, 0));
                        result.Dispose();
                        return invalid;
                    }
                    AlgorithmPrimaryImageSelection primary = AlgorithmArtifactSelection.SelectPrimaryImage(result.Artifacts);
                    if (primary.Status != AlgorithmPrimaryImageSelectionStatus.Selected)
                    {
                        if (descriptor.ResultSemantics == AlgorithmResultSemantics.Analysis
                            && primary.Status is AlgorithmPrimaryImageSelectionStatus.None or AlgorithmPrimaryImageSelectionStatus.Missing)
                        {
                            return result;
                        }
                        AlgorithmResult invalid = CreateInvalidPrimaryResult(invocation, result, primary);
                        result.Dispose();
                        return invalid;
                    }
                    AlgorithmImageArtifact imageArtifact = primary.Artifact!;

                    linked!.Token.ThrowIfCancellationRequested();
                    target = _image.Dispatcher.Invoke(() =>
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        return new AlgorithmBitmapWriteTarget(imageArtifact.Image);
                    });
                    await target.FillAsync(linked.Token).ConfigureAwait(false);

                    bool wrotePreview = false;
                    bool displayed = _image.TryPublishAlgorithmPreview(claim, () =>
                    {
                        if (!IsCurrent(invocation.InvocationId)) return;
                        WriteableBitmap preview = target.TakeBitmap(linked.Token);
                        lock (_sync)
                        {
                            _previewBitmap = preview;
                            _previewBitmapClaim = null;
                            _needsRestore = true;
                        }
                        _image.Presentation.Publish(preview, preview);
                        lock (_sync)
                        {
                            if (_latestClaim == claim) _previewBitmapClaim = claim;
                        }
                        wrotePreview = true;
                    });
                    displayed &= wrotePreview;
                    if (!displayed)
                    {
                        AlgorithmResult superseded = CreateSupersededResult(invocation, result.AlgorithmVersion, result.Diagnostics);
                        result.Dispose();
                        return superseded;
                    }
                    return result;
                }
                finally
                {
                    try
                    {
                        if (target != null) await target.DisposeAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        _image.CompleteAlgorithmInvocationRun(claim, linked!);
                        linked!.Dispose();
                    }
                }
            }
            catch (OperationCanceledException) when (linked!.IsCancellationRequested)
            {
                AlgorithmResult cancelled = !IsCurrent(invocation.InvocationId)
                    ? CreateSupersededResult(invocation, result?.AlgorithmVersion ?? default, result?.Diagnostics)
                    : CreateCancelledResult(invocation, result?.AlgorithmVersion ?? default, result?.Diagnostics);
                result?.Dispose();
                return cancelled;
            }
            catch
            {
                result?.Dispose();
                throw;
            }
        }

        private static void DisposeTransferred(IEnumerable<AlgorithmInput> inputs)
        {
            foreach (AlgorithmInput input in inputs)
                if (input.Ownership == AlgorithmInputOwnership.Transferred) input.Image.Dispose();
        }

        public bool Commit()
        {
            if (_image == null) return false;
            AlgorithmInvocationClaim? claim;
            Guid invocationId;
            WriteableBitmap? preview;
            lock (_sync)
            {
                if (_disposed || _isCompleted) return false;
                claim = _latestClaim;
                invocationId = _latestInvocationId;
                preview = _previewBitmap;
                // A newer run may own the host while the previous image is still displayed.
                // Reject that state without cancelling the pending run or copying a placeholder.
                if (preview == null || _previewBitmapClaim != claim) return false;
            }
            if (!claim.HasValue || !IsSnapshotCurrent(claim.Value, invocationId))
            {
                if (claim.HasValue) CancelClaimIfLatest(claim.Value);
                return false;
            }

            _image.BeforeAlgorithmPreviewCommit(claim.Value);

            bool committed = _image.TryCompleteAlgorithmPreview(claim.Value, () =>
            {
                _image.ViewBitmapSource = preview;
                _image.Presentation.Publish(preview, null);
                lock (_sync)
                {
                    if (_latestClaim != claim)
                        throw new InvalidOperationException("Preview session state changed during commit.");
                    _isCompleted = true;
                }
            }, _image.NotifySourcePixelsChanged);
            if (committed) return true;
            CancelClaimIfLatest(claim.Value);
            return false;
        }

        private bool IsSnapshotCurrent(AlgorithmInvocationClaim claim, Guid invocationId)
            => claim.InvocationId == invocationId
                && claim.Scope.DocumentInstanceId == _documentInstanceId
                && claim.Scope.SourceRevision == _sourceRevision
                && _image != null
                && !_image.IsDisposed
                && _image.DocumentInstanceId == _documentInstanceId
                && _image.IsCurrentImageRevision(_sourceRevision)
                && _image.OwnsAlgorithmPreviewClaim(claim)
                && _image.IsCurrentAlgorithmInvocation(claim);

        private bool CancelClaimIfLatest(AlgorithmInvocationClaim claim)
        {
            lock (_sync)
            {
                if (_disposed || _isCompleted || _latestClaim != claim) return false;
            }
            bool cancelled = _image?.TryCancelAlgorithmPreview(claim) == true;
            if (cancelled || IsDocumentInvalidated())
            {
                lock (_sync)
                {
                    if (_latestClaim == claim) _isCompleted = true;
                }
            }
            return cancelled;
        }

        // Synchronous compatibility path. New UI code uses PreviewAsync with a catalog invocation.
        public void Apply(Action<Mat> apply)
        {
            ArgumentNullException.ThrowIfNull(apply);
            if (!TryContinue()) return;
            RestoreOriginal();

            PreviewBitmap.Lock();
            try
            {
                _needsRestore = true;
                PreviewBitmap.AddDirtyRect(new Int32Rect(0, 0, PreviewBitmap.PixelWidth, PreviewBitmap.PixelHeight));
                using Mat mat = Mat.FromPixelData(
                    PreviewBitmap.PixelHeight,
                    PreviewBitmap.PixelWidth,
                    GetPreviewMatType(PreviewBitmap.Format),
                    PreviewBitmap.BackBuffer,
                    PreviewBitmap.BackBufferStride);
                apply(mat);
            }
            finally
            {
                PreviewBitmap.Unlock();
            }
        }

        public void ShowOriginal()
        {
            if (_image == null || _sessionId == Guid.Empty)
            {
                if (TryContinue()) RestoreOriginal();
                return;
            }
            _image.Dispatcher.Invoke(() =>
            {
                AlgorithmInvocationClaim? claim;
                lock (_sync)
                {
                    if (_disposed || _isCompleted) return;
                    _previewEpoch++;
                    claim = _latestClaim;
                }
                // Revoke only this run, without completing the reusable session. Host cancellation
                // restores canonical pixels and cancels its token; callbacks may install a new owner.
                if (claim.HasValue) _image.TryCancelAlgorithmPreview(claim.Value);
            });
        }

        public bool Cancel()
        {
            AlgorithmInvocationClaim? claim;
            lock (_sync)
            {
                if (_isCompleted || _disposed) return false;
                claim = _latestClaim;
            }
            bool ownedHostPreview = claim.HasValue && _image?.TryCancelAlgorithmPreview(claim.Value) == true;
            if (ownedHostPreview || IsDocumentInvalidated())
            {
                lock (_sync)
                {
                    if (_latestClaim == claim) _isCompleted = true;
                }
            }
            return ownedHostPreview;
        }

        private bool IsDocumentInvalidated()
            => _image == null
                || _image.IsDisposed
                || _image.DocumentInstanceId != _documentInstanceId
                || !_image.IsCurrentImageRevision(_sourceRevision);

        public void CancelIfActive()
        {
            bool active;
            lock (_sync) active = !_isCompleted && !_disposed;
            if (active) Cancel();
        }

        public bool IsCurrent(Guid invocationId)
        {
            AlgorithmInvocationClaim? claim;
            Guid latestInvocationId;
            bool inactive;
            lock (_sync)
            {
                if (_image == null || _sessionId == Guid.Empty) return false;
                claim = _latestClaim;
                latestInvocationId = _latestInvocationId;
                inactive = _disposed || _isCompleted;
            }
            if (!claim.HasValue) return false;
            return ImageAlgorithmPreviewValidity.IsCurrent(
                _documentInstanceId,
                _sourceRevision,
                invocationId,
                _image.DocumentInstanceId,
                _image.ImageRevision,
                latestInvocationId,
                inactive || _image.IsDisposed)
                && _image.IsCurrentAlgorithmInvocation(claim.Value);
        }

        private bool TryContinue()
        {
            AlgorithmInvocationClaim? claim;
            bool inactive;
            lock (_sync)
            {
                inactive = _isCompleted || _disposed;
                claim = _latestClaim;
            }
            if (inactive) return false;
            if (_image == null || _sessionId == Guid.Empty) return true;
            if (IsSameDocument()
                && _image.IsCurrentImageRevision(_sourceRevision)
                && claim.HasValue
                && _image.IsCurrentAlgorithmInvocation(claim.Value))
            {
                return true;
            }

            Cancel();
            return false;
        }

        private bool IsSameDocument()
            => _image != null && !_image.IsDisposed && _image.DocumentInstanceId == _documentInstanceId;

        private void RestoreCanonicalHost()
        {
            if (_image == null || _image.IsDisposed || _image.DocumentInstanceId != _documentInstanceId) return;
            _image.Presentation.RestoreSource();
        }

        private void RestoreOriginal()
        {
            if (!_needsRestore) return;

            if (PreviewBitmap.PixelWidth != _originalSource.PixelWidth
                || PreviewBitmap.PixelHeight != _originalSource.PixelHeight
                || PreviewBitmap.Format != _originalSource.Format)
            {
                _previewBitmap = new WriteableBitmap(_originalSource);
            }
            else
            {
                PreviewBitmap.Lock();
                try
                {
                    int bufferSize = checked(PreviewBitmap.BackBufferStride * PreviewBitmap.PixelHeight);
                    _originalSource.CopyPixels(Int32Rect.Empty, PreviewBitmap.BackBuffer, bufferSize, PreviewBitmap.BackBufferStride);
                    PreviewBitmap.AddDirtyRect(new Int32Rect(0, 0, PreviewBitmap.PixelWidth, PreviewBitmap.PixelHeight));
                }
                finally
                {
                    PreviewBitmap.Unlock();
                }
            }

            _needsRestore = false;
            if (_image != null && IsCurrent(_latestInvocationId))
            {
                _image.Presentation.Publish(PreviewBitmap, PreviewBitmap);
            }
        }

        private static MatType GetPreviewMatType(PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormats.Indexed8) return MatType.CV_8UC1;
            if (pixelFormat == PixelFormats.Prgba64) return MatType.CV_16UC4;
            return AlgorithmImageInterop.ToMatType(ImageAlgorithmInputFactory.FromPixelFormat(pixelFormat));
        }

        private static AlgorithmResult CreateSupersededResult(
            AlgorithmInvocation invocation,
            AlgorithmVersion algorithmVersion = default,
            AlgorithmExecutionDiagnostics? diagnostics = null)
        {
            return new AlgorithmResult
            {
                InvocationId = invocation.InvocationId,
                AlgorithmId = invocation.AlgorithmId,
                AlgorithmVersion = algorithmVersion,
                Status = AlgorithmResultStatus.Superseded,
                Failures = new[]
                {
                    new AlgorithmFailure("preview_superseded", "A newer invocation, document, or source revision replaced this preview."),
                },
                Diagnostics = diagnostics ?? new AlgorithmExecutionDiagnostics(),
            };
        }

        private static AlgorithmResult CreateCancelledResult(
            AlgorithmInvocation invocation,
            AlgorithmVersion algorithmVersion,
            AlgorithmExecutionDiagnostics? diagnostics)
            => new()
            {
                InvocationId = invocation.InvocationId,
                AlgorithmId = invocation.AlgorithmId,
                AlgorithmVersion = algorithmVersion,
                Status = AlgorithmResultStatus.Cancelled,
                Failures = new[] { new AlgorithmFailure("cancelled", "Algorithm execution was cancelled.") },
                Diagnostics = diagnostics ?? new AlgorithmExecutionDiagnostics(),
            };

        private static AlgorithmResult CreateInvalidPrimaryResult(
            AlgorithmInvocation invocation,
            AlgorithmResult providerResult,
            AlgorithmPrimaryImageSelection selection)
            => new()
            {
                InvocationId = invocation.InvocationId,
                AlgorithmId = invocation.AlgorithmId,
                AlgorithmVersion = providerResult.AlgorithmVersion,
                Status = AlgorithmResultStatus.Failed,
                Failures =
                [
                    new AlgorithmFailure(
                        "primary_image_contract_violation",
                        selection.Status switch
                        {
                            AlgorithmPrimaryImageSelectionStatus.None => "The image-transform result contains no image artifact; exactly one Role=primary image is required.",
                            AlgorithmPrimaryImageSelectionStatus.Missing => $"The result contains {selection.ImageArtifactCount} image artifact(s), but none has Role=primary.",
                            _ => $"The result contains {selection.PrimaryArtifactCount} image artifacts with Role=primary; exactly one is required.",
                        }),
                ],
                Diagnostics = providerResult.Diagnostics,
            };

        public void Dispose()
        {
            CancelIfActive();
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _source?.Dispose();
        }
    }
}
