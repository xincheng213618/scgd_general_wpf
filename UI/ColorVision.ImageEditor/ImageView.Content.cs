using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor;

public partial class ImageView
{
    private readonly List<IImageContentProvider> _contentProviders = new();
    private CancellationTokenSource? _staticOpenCancellation;
    private bool _publishingDecodedContent;
    public Task ImageOpenCompletion { get; private set; } = Task.CompletedTask;
    public IImageContentCapability? ImageContentCapability => _document.Capability;
    public DecodedImageContent? ImageContent => _document.Content;
    public long ContentVersion => _document.ContentVersion;

    /// <summary>Publishes provenance without granting any capability to the current pixels.</summary>
    public void SetImageParameterRecord(ColorVisionTiffParameters parameters)
    {
        Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(parameters);
        ObjectDisposedException.ThrowIf(_document.IsDisposed, this);
        _document.ParameterRecord = parameters;
        Opentif.ApplyColorVisionMetadata(Config, parameters);
    }

    public IDisposable BeginImageContentPublication()
    {
        Dispatcher.VerifyAccess();
        bool previous = _publishingDecodedContent;
        _publishingDecodedContent = true;
        return new PublicationScope(() =>
        {
            _publishingDecodedContent = previous;
            if (!previous) NotifyImageSourceLoaded();
        });
    }

    private sealed class PublicationScope(Action complete) : IDisposable
    {
        private Action? _complete = complete;
        public void Dispose() => Interlocked.Exchange(ref _complete, null)?.Invoke();
    }

    public void RegisterImageContentProvider(IImageContentProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_contentProviders.Exists(item => item.GetType() == provider.GetType())) _contentProviders.Add(provider);
    }

    public async Task PublishDecodedImageContentAsync(DecodedImageContent content, BitmapSource displaySource, CancellationToken cancellationToken = default)
    {
        Dispatcher.VerifyAccess();
        long version = _document.ContentVersion;
        IImageContentCapability? capability = null;
        try
        {
            foreach (var provider in _contentProviders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { capability = await provider.PrepareAsync(EditorContext, content, cancellationToken); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { log.Warn("Image measurement metadata is unavailable; retaining decoded pixels.", ex); }
                if (capability != null) break;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (version != _document.ContentVersion || !string.Equals(content.FilePath, Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), StringComparison.OrdinalIgnoreCase))
                throw new OperationCanceledException("The target image content has changed.");
            _publishingDecodedContent = true;
            try
            {
                SetImageSource(displaySource is WriteableBitmap writable ? writable : new WriteableBitmap(displaySource));
                _document.Content = content.Parameters != null || content.Pixels.Format == System.Windows.Media.PixelFormats.Gray32Float
                    ? content : new DecodedImageContent((BitmapSource)ViewBitmapSource, null, content.FilePath);
                _document.ParameterRecord = content.Parameters;
                AttachImageContentCapability(capability);
                capability = null;
            }
            finally { _publishingDecodedContent = false; }
            UpdateZoomAndScale();
            NotifyImageSourceLoaded();
        }
        finally { capability?.Dispose(); }
    }

    public void AttachImageContentCapability(IImageContentCapability? capability)
    {
        Dispatcher.VerifyAccess();
        if (!ReferenceEquals(_document.Capability, capability))
        {
            _document.ReplaceCapability(capability);
        }
        capability?.Activate();
        IEditorToolFactory.ApplyImageContentTools(capability ?? (object?)EditorContext.IImageOpen);
    }

    /// <summary>Changes the selected display/algorithm layer without changing acquisition identity.</summary>
    public void SetDisplayLayerSource(ImageSource source)
    {
        SetImageSourceCore(source, EnableEditorImageServices, false, ImageDocumentMutationKind.DisplayLayerChanged);
    }

    private async Task OpenStaticImageAsync(IAsyncImageOpen opener, string path, CancellationToken token)
    {
        try { await opener.OpenImageAsync(EditorContext, path, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { log.Error($"Failed to open image: {path}", ex); throw; }
    }
}
