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
        private AlgorithmInvocationClaim? _latestClaim;
        private Guid _latestInvocationId;
        private bool _isCompleted;
        private bool _needsRestore;
        private bool _disposed;

        private ImageAlgorithmPreviewSession(
            ImageProcessingContext image,
            AlgorithmImageBuffer source,
            BitmapSource originalSource,
            WriteableBitmap previewBitmap,
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
            PreviewBitmap = previewBitmap;
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
            PreviewBitmap = previewBitmap ?? throw new ArgumentNullException(nameof(previewBitmap));
        }

        public WriteableBitmap PreviewBitmap { get; private set; }

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
                WriteableBitmap preview = new(originalSource);
                if (!image.TryBeginAlgorithmPreviewSession(
                        sessionId,
                        documentInstanceId,
                        revision,
                        previewRestore: () =>
                        {
                            if (image.IsDisposed || image.DocumentInstanceId != documentInstanceId) return;
                            image.ImageShow.Source = image.ViewBitmapSource;
                            image.FunctionImage = null;
                        },
                        previewPublication: () =>
                        {
                            image.FunctionImage = preview;
                            image.ImageShow.Source = preview;
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
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (_isCompleted) throw new InvalidOperationException("The preview session has already completed.");
                }

                // Never retain the session monitor while entering Dispatcher, Runner, or a host
                // callback. Publication can synchronously query this session from the UI thread.
                input = _source.Clone();
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (!_image.TryBeginAlgorithmPreviewInvocation(
                        _sessionId,
                        _documentInstanceId,
                        _sourceRevision,
                        invocation.InvocationId,
                        linked,
                        RestoreCanonicalHost,
                        out claim))
                {
                    input.Dispose();
                    linked.Dispose();
                    input = null;
                    linked = null;
                    DisposeTransferred(additional);
                    return CreateSupersededResult(invocation);
                }
                claimInstalled = true;

                bool acceptedBySession;
                lock (_sync)
                {
                    acceptedBySession = !_disposed
                        && !_isCompleted
                        && (!_latestClaim.HasValue || claim.Sequence > _latestClaim.Value.Sequence);
                    if (acceptedBySession)
                    {
                        _latestClaim = claim;
                        _latestInvocationId = invocation.InvocationId;
                    }
                }
                if (!acceptedBySession)
                {
                    _image.TryCancelAlgorithmPreview(claim);
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

            AlgorithmResult result;
            bool runnerSubmitted = false;
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
            finally
            {
                _image.CompleteAlgorithmInvocationRun(claim, linked!);
                linked.Dispose();
            }

            try
            {
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

                bool wrotePreview = false;
                bool displayed = _image.TryPublishAlgorithmPreview(claim, () =>
                {
                    if (!IsCurrent(invocation.InvocationId)) return;
                    PreviewBitmap = ImageAlgorithmInputFactory.ToWriteableBitmap(imageArtifact.Image);
                    _needsRestore = true;
                    _image.FunctionImage = PreviewBitmap;
                    _image.ImageShow.Source = PreviewBitmap;
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
            catch
            {
                result.Dispose();
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
            WriteableBitmap preview;
            lock (_sync)
            {
                if (_disposed || _isCompleted) return false;
                claim = _latestClaim;
                invocationId = _latestInvocationId;
                preview = PreviewBitmap;
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
                _image.ImageShow.Source = preview;
                _image.FunctionImage = null;
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
            if (!TryContinue()) return;
            RestoreOriginal();
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
            _image.ImageShow.Source = _image.ViewBitmapSource;
            _image.FunctionImage = null;
        }

        private void RestoreOriginal()
        {
            if (!_needsRestore) return;

            if (PreviewBitmap.PixelWidth != _originalSource.PixelWidth
                || PreviewBitmap.PixelHeight != _originalSource.PixelHeight
                || PreviewBitmap.Format != _originalSource.Format)
            {
                PreviewBitmap = new WriteableBitmap(_originalSource);
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
                _image.FunctionImage = PreviewBitmap;
                _image.ImageShow.Source = PreviewBitmap;
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
