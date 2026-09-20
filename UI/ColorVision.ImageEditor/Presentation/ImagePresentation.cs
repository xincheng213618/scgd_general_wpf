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

    // Invalidating an old preview is housekeeping, not a new display choice. A frame
    // producer can distinguish it from a synchronous subscriber selecting a new view.
    internal void RestoreSourceForDocumentMutation()
    {
        _canvas.Dispatcher.VerifyAccess();
        if (_isDisposed()) return;
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
