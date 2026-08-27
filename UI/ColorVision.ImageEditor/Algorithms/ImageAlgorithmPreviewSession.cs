#pragma warning disable CS8625
using ColorVision.Algorithms;
using OpenCvSharp;
using System;
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
        private CancellationTokenSource? _previewCancellation;
        private Guid _latestInvocationId;
        private bool _isCompleted;
        private bool _needsRestore;
        private bool _disposed;

        private ImageAlgorithmPreviewSession(
            ImageProcessingContext image,
            AlgorithmImageBuffer source,
            BitmapSource originalSource,
            WriteableBitmap previewBitmap,
            long sourceRevision,
            Guid sessionId)
        {
            _image = image;
            _sessionId = sessionId;
            _documentInstanceId = image.DocumentInstanceId;
            _sourceRevision = sourceRevision;
            _source = source;
            _originalSource = originalSource;
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

        public Guid LatestInvocationId => _latestInvocationId;

        public long SourceRevision => _sourceRevision;

        public static ImageAlgorithmPreviewSession Start(ImageProcessingContext image)
        {
            ArgumentNullException.ThrowIfNull(image);
            BitmapSource originalSource = image.ViewBitmapSource as BitmapSource
                ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
            Guid sessionId = Guid.NewGuid();
            image.BeginAlgorithmPreviewSession(sessionId);
            AlgorithmImageBuffer? source = null;
            try
            {
                // The catalog always receives the canonical image representation. The WPF
                // source is retained separately for low-copy, pixel-exact preview restoration.
                (source, long revision) = ImageAlgorithmInputFactory.AcquireCurrentFrame(image);
                WriteableBitmap preview = new(originalSource);
                image.FunctionImage = preview;
                image.ImageShow.Source = preview;
                ImageAlgorithmPreviewSession session = new(image, source, originalSource, preview, revision, sessionId);
                source = null;
                return session;
            }
            catch
            {
                source?.Dispose();
                if (image.TryCancelAlgorithmPreview(sessionId) && !image.IsDisposed)
                {
                    image.ImageShow.Source = image.ViewBitmapSource;
                    image.FunctionImage = null;
                }
                throw;
            }
        }

        public async Task<AlgorithmResult> PreviewAsync(
            AlgorithmInvocation invocation,
            AlgorithmHostCapabilities requiredCapabilities = AlgorithmHostCapabilities.Interactive,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            if (_image == null || _source == null)
                throw new InvalidOperationException("This compatibility session has no ImageView catalog context.");

            CancellationTokenSource linked;
            AlgorithmImageBuffer input;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_isCompleted) throw new InvalidOperationException("The preview session has already completed.");
                _previewCancellation?.Cancel();
                _previewCancellation?.Dispose();
                _previewCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked = _previewCancellation;
                _latestInvocationId = invocation.InvocationId;
                if (!_image.SetLatestAlgorithmPreviewInvocation(_sessionId, invocation.InvocationId))
                {
                    _previewCancellation.Dispose();
                    _previewCancellation = null;
                    return CreateSupersededResult(invocation);
                }
                input = _source.Clone();
            }

            AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = invocation,
                Inputs = new[]
                {
                    new AlgorithmInput
                    {
                        Name = "source",
                        Image = input,
                        Ownership = AlgorithmInputOwnership.Transferred,
                        SourceRevision = _sourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ColorSpace = "encoded-device-values",
                    },
                },
                RequiredCapabilities = requiredCapabilities,
            }, linked.Token).ConfigureAwait(false);

            if (!IsCurrent(invocation.InvocationId))
            {
                AlgorithmResult superseded = CreateSupersededResult(invocation, result.AlgorithmVersion, result.Diagnostics);
                result.Dispose();
                return superseded;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded) return result;
            AlgorithmImageArtifact? imageArtifact = result.GetArtifact<AlgorithmImageArtifact>();
            if (imageArtifact == null) return result;

            bool displayed = false;
            await _image.Dispatcher.InvokeAsync(() =>
            {
                if (!IsCurrent(invocation.InvocationId)) return;
                PreviewBitmap = ImageAlgorithmInputFactory.ToWriteableBitmap(imageArtifact.Image);
                _needsRestore = true;
                _image.FunctionImage = PreviewBitmap;
                _image.ImageShow.Source = PreviewBitmap;
                displayed = true;
            });
            if (!displayed)
            {
                AlgorithmResult superseded = CreateSupersededResult(invocation, result.AlgorithmVersion, result.Diagnostics);
                result.Dispose();
                return superseded;
            }
            return result;
        }

        public bool Commit()
        {
            if (_image == null || !TryContinue()) return false;
            Guid invocationId = _latestInvocationId;
            if (!IsCurrent(invocationId) || !_image.TryCompleteAlgorithmPreview(_sessionId, invocationId))
            {
                Cancel();
                return false;
            }

            CancelPending();
            _image.ViewBitmapSource = PreviewBitmap;
            _image.ImageShow.Source = PreviewBitmap;
            _image.FunctionImage = null;
            _isCompleted = true;
            _image.NotifySourcePixelsChanged();
            return true;
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
            if (_isCompleted || _disposed) return false;
            bool ownedHostPreview = _sessionId != Guid.Empty && _image?.TryCancelAlgorithmPreview(_sessionId) == true;
            CancelPending();
            _isCompleted = true;
            if (ownedHostPreview && _image != null && IsSameDocument())
            {
                _image.ImageShow.Source = _image.ViewBitmapSource;
                _image.FunctionImage = null;
            }
            return ownedHostPreview;
        }

        public void CancelIfActive()
        {
            if (!_isCompleted) Cancel();
        }

        public bool IsCurrent(Guid invocationId)
        {
            lock (_sync)
            {
                if (_image == null || _sessionId == Guid.Empty) return false;
                return ImageAlgorithmPreviewValidity.IsCurrent(
                    _documentInstanceId,
                    _sourceRevision,
                    invocationId,
                    _image.DocumentInstanceId,
                    _image.ImageRevision,
                    _latestInvocationId,
                    _disposed || _isCompleted || _image.IsDisposed)
                    && _image.IsCurrentAlgorithmPreview(_sessionId, invocationId);
            }
        }

        private bool TryContinue()
        {
            if (_isCompleted || _disposed) return false;
            if (_image == null || _sessionId == Guid.Empty) return true;
            if (IsSameDocument()
                && _image.IsCurrentImageRevision(_sourceRevision)
                && _image.IsCurrentAlgorithmPreview(_sessionId, _latestInvocationId))
            {
                return true;
            }

            Cancel();
            return false;
        }

        private bool IsSameDocument()
            => _image != null && !_image.IsDisposed && _image.DocumentInstanceId == _documentInstanceId;

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

        private void CancelPending()
        {
            lock (_sync)
            {
                _previewCancellation?.Cancel();
                _previewCancellation?.Dispose();
                _previewCancellation = null;
            }
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

        public void Dispose()
        {
            if (_disposed) return;
            CancelIfActive();
            _disposed = true;
            CancelPending();
            _source?.Dispose();
        }
    }
}
