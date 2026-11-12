using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor;

public partial class ImageView
{
    private readonly List<IImageContentProvider> _contentProviders = new();
    private CancellationTokenSource? _staticOpenCancellation;
    private IImageOpen? _retainedOpenerDuringProbe;
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

    public IDisposable BeginImageContentPublication() => BeginImageContentPublication(null);

    public IDisposable BeginImageContentPublication(Func<bool>? shouldNotify)
    {
        Dispatcher.VerifyAccess();
        bool previous = _publishingDecodedContent;
        _publishingDecodedContent = true;
        return new PublicationScope(() =>
        {
            _publishingDecodedContent = previous;
            if (!previous && (shouldNotify?.Invoke() ?? true)) NotifyImageSourceLoaded();
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
                WriteableBitmap publishedSource = displaySource is WriteableBitmap writable ? writable : new WriteableBitmap(displaySource);
                long revision = _document.Revision;
                SetImageSource(publishedSource);
                cancellationToken.ThrowIfCancellationRequested();
                // Source publication can synchronously clear or replace this content, even
                // with the same bitmap and path. Only our own single replacement may proceed.
                if (_document.IsDisposed || _document.ContentVersion != version + 1 || _document.Revision != revision + 1
                    || !ReferenceEquals(_document.Source, publishedSource)
                    || !string.Equals(content.FilePath, Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), StringComparison.OrdinalIgnoreCase))
                    throw new OperationCanceledException("The target image content changed during source publication.");
                _document.Content = content.Parameters != null || content.Pixels.Format == System.Windows.Media.PixelFormats.Gray32Float
                    ? content : new DecodedImageContent((BitmapSource)ViewBitmapSource, null, content.FilePath);
                _document.ParameterRecord = content.Parameters;
                IImageContentCapability? publishingCapability = capability;
                capability = null;
                AttachImageContentCapability(publishingCapability);
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
        long installedContentVersion = -1;
        try
        {
            _document.ReplaceCapability(capability, out installedContentVersion);
            if (_document.IsDisposed || !ReferenceEquals(_document.Capability, capability) || _document.ContentVersion != installedContentVersion)
                throw new OperationCanceledException("The image content capability was replaced during retirement.");
            capability?.Activate();
            if (_document.IsDisposed || !ReferenceEquals(_document.Capability, capability) || _document.ContentVersion != installedContentVersion)
                throw new OperationCanceledException("The image content capability was replaced during activation.");
            bool toolsAccepted = IEditorToolFactory.ApplyImageContentTools(capability ?? (object?)EditorContext.IImageOpen);
            if (!toolsAccepted || _document.IsDisposed || !ReferenceEquals(_document.Capability, capability) || _document.ContentVersion != installedContentVersion)
                throw new OperationCanceledException("The image content capability was replaced during tool publication.");
        }
        catch
        {
            if (installedContentVersion < 0) capability?.Dispose();
            else _document.RetireCapabilityIfCurrent(capability, installedContentVersion);
            throw;
        }
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

    private void OpenPreparedFile(IPreparedImageOpen opener, string path)
    {
        var request = new CancellationTokenSource();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ImageOpenCompletion = completion.Task;
        _ = CompletePreparedFileOpenAsync(opener, path, request, completion);
    }

    private async Task CompletePreparedFileOpenAsync(IPreparedImageOpen opener, string path, CancellationTokenSource request, TaskCompletionSource completion)
    {
        CancellationToken token = request.Token;
        Exception? failure = null;
        try
        {
            ReleaseImageContent(opener, request);
            token.ThrowIfCancellationRequested();
            Config.ClearProperties();
            token.ThrowIfCancellationRequested();
            IEditorToolFactory.ApplyImageOpenTools(null);
            token.ThrowIfCancellationRequested();
            SetLayerController(null);
            token.ThrowIfCancellationRequested();
            Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_FilePath);

            ImageFileMetadata file = await Task.Run(() => opener.ReadFileMetadata(path), token);
            BitmapOpenRequests.VerifyTarget(EditorContext, path, token);
            Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, file.Length, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_FileSize);
            IEditorToolFactory.ApplyImageOpenTools(opener);
            BitmapOpenRequests.VerifyTarget(EditorContext, path, token);
            _retainedOpenerDuringProbe = null;
            EditorContext.IImageOpen = opener;
            await opener.OpenPreparedImageAsync(EditorContext, path, file, token);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            if (ReferenceEquals(_staticOpenCancellation, request) && EditorContext.IImageOpen == null)
            {
                _staticOpenCancellation = null;
                var retained = _retainedOpenerDuringProbe;
                _retainedOpenerDuringProbe = null;
                request.Dispose();
                if (retained is IImageOpenContentLifetime lifetime) TrackContentRelease(lifetime, reuseBuffers: false);
            }
        }

        if (failure == null) completion.TrySetResult();
        else if (failure is OperationCanceledException canceled) completion.TrySetCanceled(canceled.CancellationToken);
        else if (token.IsCancellationRequested) completion.TrySetCanceled(token);
        else
        {
            if (failure is not FileNotFoundException) log.Error($"Failed to open image: {path}", failure);
            completion.TrySetException(failure);
        }
    }
}
