using ColorVision.ImageEditor.Documents;
using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ColorVision.ImageEditor.Presentation;

public readonly record struct ImagePresentationRequest(Guid DocumentId, long SourceRevision, long Generation);

/// <summary>The sole publication boundary for image display. Pixel commits belong to the document.</summary>
public sealed class ImagePresentation
{
    private readonly DrawCanvas _canvas;
    private readonly Func<Guid> _documentId;
    private readonly Func<long> _revision;
    private readonly Func<bool> _isDisposed;
    private readonly Func<ImageSource?> _source;
    private ImageSource? _functionImage;
    private long _generation;
    private long _selectionVersion;
    private SourceRestoreDeferral? _sourceRestoreDeferral;

    private sealed class SourceRestoreDeferral : IDisposable
    {
        private readonly ImagePresentation _owner;
        private readonly SourceRestoreDeferral? _previous;
        private readonly ImageSource _source;
        private readonly Guid _documentId;
        private readonly long _revision;
        private readonly long _selectionVersion;
        private bool _restoreRequested;
        private bool _disposed;

        public SourceRestoreDeferral(ImagePresentation owner, ImageSource source, long revision)
        {
            _owner = owner;
            _previous = owner._sourceRestoreDeferral;
            _source = source;
            _documentId = owner._documentId();
            _revision = revision;
            _selectionVersion = owner._selectionVersion;
            owner._sourceRestoreDeferral = this;
        }

        private bool MatchesSource()
            => !_owner._isDisposed() && _owner._documentId() == _documentId
                && _owner._revision() == _revision && ReferenceEquals(_owner._source(), _source);

        public bool TryDeferRestore()
        {
            if (!MatchesSource()) return false;
            // Revision subscribers can already have selected a display or requested work
            // for the new source. Housekeeping must preserve that newer intent entirely.
            if (_owner._selectionVersion == _selectionVersion)
            {
                Interlocked.Increment(ref _owner._generation);
                _owner.FunctionImage = null;
                _restoreRequested = true;
            }
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner._sourceRestoreDeferral = _previous;
            if (_restoreRequested && _owner._selectionVersion == _selectionVersion && MatchesSource())
                _owner.PublishCore(_source, null);
        }
    }

    internal ImagePresentation(ImageDocument document, DrawCanvas canvas)
    {
        _canvas = canvas;
        _documentId = () => document.Id;
        _revision = () => document.Revision;
        _isDisposed = () => document.IsDisposed;
        _source = () => document.Source;
    }

    internal ImagePresentation(DrawCanvas canvas, ImageProcessingContextBinding binding)
    {
        _canvas = canvas;
        _documentId = binding.GetDocumentInstanceId;
        _revision = binding.GetImageRevision;
        _isDisposed = binding.IsDisposed;
        _source = binding.GetViewBitmapSource;
    }

    public ImageSource? DisplaySource => _canvas.Source;
    internal long SelectionVersion => _selectionVersion;
    public Effect? SceneEffect => _canvas.Effect;

    internal void SetSceneEffect(Effect? effect)
    {
        _canvas.Dispatcher.VerifyAccess();
        _canvas.Effect = effect;
    }
    public ImageSource? FunctionImage
    {
        get => _functionImage;
        internal set
        {
            _canvas.Dispatcher.VerifyAccess();
            ObjectDisposedException.ThrowIf(_isDisposed(), this);
            _functionImage = value;
        }
    }

    public ImagePresentationRequest BeginRequest()
    {
        _canvas.Dispatcher.VerifyAccess();
        ++_selectionVersion;
        return new(_documentId(), _revision(), Interlocked.Increment(ref _generation));
    }

    public bool IsCurrent(ImagePresentationRequest request)
        => !_isDisposed() && request.DocumentId == _documentId()
            && request.SourceRevision == _revision() && request.Generation == Volatile.Read(ref _generation);

    public bool TryPublish(ImagePresentationRequest request, ImageSource? displaySource, ImageSource? functionImage)
    {
        _canvas.Dispatcher.VerifyAccess();
        if (!IsCurrent(request)) return false;
        ++_selectionVersion;
        PublishCore(displaySource, functionImage);
        return true;
    }

    public void Publish(ImageSource? displaySource, ImageSource? functionImage)
    {
        _canvas.Dispatcher.VerifyAccess();
        if (_isDisposed()) return;
        Interlocked.Increment(ref _generation);
        ++_selectionVersion;
        PublishCore(displaySource, functionImage);
    }

    public void RestoreSource() => Publish(_source(), null);

    internal IDisposable DeferSourceRestoreForFrame(ImageSource source, long expectedRevision)
    {
        _canvas.Dispatcher.VerifyAccess();
        return new SourceRestoreDeferral(this, source, expectedRevision);
    }

    // Invalidating an old preview is housekeeping, not a new display choice. A frame
    // producer can distinguish it from a synchronous subscriber selecting a new view.
    internal void RestoreSourceForDocumentMutation()
    {
        _canvas.Dispatcher.VerifyAccess();
        if (_isDisposed()) return;
        if (_sourceRestoreDeferral?.TryDeferRestore() == true) return;
        Interlocked.Increment(ref _generation);
        PublishCore(_source(), null);
    }

    internal void Invalidate()
    {
        Interlocked.Increment(ref _generation);
    }

    private void PublishCore(ImageSource? displaySource, ImageSource? functionImage)
    {
        FunctionImage = functionImage;
        _canvas.Source = displaySource;
    }
}
