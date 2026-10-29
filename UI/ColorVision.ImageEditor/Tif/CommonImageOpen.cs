using ColorVision.ImageEditor.Abstractions;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".bmp|.jpg|.jpeg|.png|.webp|.ico|.gif")]
    public record class CommonImageOpen(EditorContext EditorContext) : IAsyncImageOpen, IImageOpenContentLifetime
    {
        private readonly object _decodeSync = new();
        private Task _decodeTail = Task.CompletedTask;
        private long _latestRequestId;
        private readonly object _requestSync = new();
        private CancellationTokenSource? _pendingRequest;

        public async void OpenImage(EditorContext context, string? filePath)
        {
            try { await OpenImageAsync(context, filePath); }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Legacy fire-and-forget callers have no completion channel. */ }
        }

        public Task ReleaseContentAsync(bool reuseBuffers)
        {
            Interlocked.Increment(ref _latestRequestId);
            lock (_requestSync) _pendingRequest?.Cancel();
            return _decodeTail;
        }

        public async Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Image file does not exist.", filePath);

            string requestedFilePath = filePath;
            long requestId = Interlocked.Increment(ref _latestRequestId);

            // Get file metadata
            FileInfo fileInfo = new FileInfo(requestedFilePath);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, requestedFilePath, nameof(CommonImageOpen), "打开器接收到的源文件路径");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, fileInfo.Name, nameof(CommonImageOpen), "当前文件名");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileInfo.Length, nameof(CommonImageOpen), "当前文件大小（字节）");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, fileInfo.CreationTime, nameof(CommonImageOpen), "当前文件创建时间");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, fileInfo.LastWriteTime, nameof(CommonImageOpen), "当前文件修改时间");

            using CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_requestSync)
            {
                _pendingRequest?.Cancel();
                _pendingRequest = requestCancellation;
            }
            cancellationToken = requestCancellation.Token;

            TaskCompletionSource decodeTurn = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task previousDecode;
            lock (_decodeSync)
            {
                previousDecode = _decodeTail;
                _decodeTail = decodeTurn.Task;
            }

            await previousDecode;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);

                DecodedImage decodedImage = await Task.Run(() => DecodeImage(requestedFilePath), cancellationToken);

                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);

                BitmapSource bitmapSource = decodedImage.BitmapSource;

                // Add image dimensions
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, bitmapSource.PixelWidth, nameof(CommonImageOpen), "位图像素宽度");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, bitmapSource.PixelHeight, nameof(CommonImageOpen), "位图像素高度");

                ApplyMetadata(context, decodedImage.Metadata);

                // Providers may await or cancel before publication; never stage into the current source.
                WriteableBitmap writeableBitmap = new(bitmapSource);

                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);
                await context.ImageView.PublishDecodedImageContentAsync(
                    new DecodedImageContent(decodedImage.Metadata.ColorVisionParameters == null ? writeableBitmap : decodedImage.BitmapSource, decodedImage.Metadata.ColorVisionParameters, requestedFilePath),
                    writeableBitmap, cancellationToken);
                context.ImageView.UpdateZoomAndScale();
            }
            finally
            {
                lock (_requestSync)
                {
                    if (ReferenceEquals(_pendingRequest, requestCancellation)) _pendingRequest = null;
                }
                decodeTurn.TrySetResult();
            }
        }

        private bool IsCurrentRequest(EditorContext context, string requestedFilePath, long requestId)
        {
            if (requestId != Volatile.Read(ref _latestRequestId))
                return false;

            string? activeFilePath = context.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            return string.Equals(activeFilePath, requestedFilePath, StringComparison.OrdinalIgnoreCase);
        }

        private static DecodedImage DecodeImage(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".png")
            {
                PngBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0)
                    throw new InvalidDataException($"PNG 文件不包含可显示帧：{filePath}");

                BitmapFrame frame = decoder.Frames[0];
                ImageMetadata metadata = ReadMetadata(frame.Metadata as BitmapMetadata);
                if (frame.CanFreeze && !frame.IsFrozen)
                    frame.Freeze();
                return new DecodedImage(frame, metadata);
            }

            ImageMetadata fallbackMetadata = ReadMetadata(stream, extension);
            stream.Position = 0;
            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return new DecodedImage(bitmapImage, fallbackMetadata);
        }

        private static ImageMetadata ReadMetadata(Stream stream, string extension)
        {
            try
            {
                BitmapDecoder? decoder = extension switch
                {
                    ".jpg" or ".jpeg" => new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None),
                    ".bmp" => new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None),
                    _ => null,
                };
                return decoder != null && decoder.Frames.Count > 0
                    ? ReadMetadata(decoder.Frames[0].Metadata as BitmapMetadata)
                    : ImageMetadata.Empty;
            }
            catch
            {
                return ImageMetadata.Empty;
            }
        }

        private static ImageMetadata ReadMetadata(BitmapMetadata? metadata)
        {
            if (metadata == null)
                return ImageMetadata.Empty;

            ColorVisionTiffParameters.TryRead(metadata, out ColorVisionTiffParameters? parameters);
            try
            {
                return new ImageMetadata(
                    metadata.CameraModel,
                    metadata.CameraManufacturer,
                    metadata.DateTaken,
                    metadata.ApplicationName,
                    metadata.Title,
                    metadata.Subject,
                    parameters);
            }
            catch
            {
                return new ImageMetadata(null, null, null, null, null, null, parameters);
            }
        }

        private static void ApplyMetadata(EditorContext context, ImageMetadata metadata)
        {
            if (metadata.ColorVisionParameters != null)
                Opentif.ApplyColorVisionMetadata(context.Config, metadata.ColorVisionParameters);
            if (metadata.CameraModel != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.CameraModel, metadata.CameraModel, nameof(CommonImageOpen), "EXIF 相机型号");
            if (metadata.CameraManufacturer != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.CameraManufacturer, metadata.CameraManufacturer, nameof(CommonImageOpen), "EXIF 相机厂商");
            if (metadata.DateTaken != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.DateTaken, metadata.DateTaken, nameof(CommonImageOpen), "EXIF 拍摄时间");
            if (metadata.ApplicationName != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ApplicationName, metadata.ApplicationName, nameof(CommonImageOpen), "EXIF 应用程序名");
            if (metadata.Title != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageTitle, metadata.Title, nameof(CommonImageOpen), "EXIF 标题");
            if (metadata.Subject != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageSubject, metadata.Subject, nameof(CommonImageOpen), "EXIF 主题");
        }

        private sealed record DecodedImage(BitmapSource BitmapSource, ImageMetadata Metadata);

        private sealed record ImageMetadata(
            string? CameraModel,
            string? CameraManufacturer,
            string? DateTaken,
            string? ApplicationName,
            string? Title,
            string? Subject,
            ColorVisionTiffParameters? ColorVisionParameters)
        {
            public static ImageMetadata Empty { get; } = new(null, null, null, null, null, null, null);
        }
    }

}
