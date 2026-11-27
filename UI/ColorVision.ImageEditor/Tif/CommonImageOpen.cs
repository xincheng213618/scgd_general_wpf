using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Presentation;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".bmp|.jpg|.jpeg|.png|.webp|.ico|.gif")]
    public record class CommonImageOpen(EditorContext EditorContext) : IPreparedImageOpen, IImageOpenContentLifetime
    {
        private readonly BitmapOpenRequests _requests = new();

        public async void OpenImage(EditorContext context, string? filePath)
        {
            try { await OpenImageAsync(context, filePath); }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Legacy fire-and-forget callers have no completion channel. */ }
        }

        public Task ReleaseContentAsync(bool reuseBuffers) => _requests.CancelAndDrain();

        public Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default)
            => OpenImageAsync(context, filePath, null, cancellationToken);

        Task IPreparedImageOpen.OpenPreparedImageAsync(EditorContext context, string filePath, ImageFileMetadata file, CancellationToken cancellationToken)
            => OpenImageAsync(context, filePath, file, cancellationToken);

        private async Task OpenImageAsync(EditorContext context, string? filePath, ImageFileMetadata? file, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            string requestedFilePath = filePath;
            using var request = _requests.Start(cancellationToken);
            cancellationToken = request.Token;
            ImageFileMetadata metadata = file ?? await Task.Run(() => ImageFileMetadata.Read(requestedFilePath), cancellationToken);
            BitmapOpenRequests.VerifyTarget(context, requestedFilePath, cancellationToken);
            metadata.Apply(context, requestedFilePath, nameof(CommonImageOpen));

            var (original, decodedMetadata, writeableBitmap) = await _requests.PrepareAsync(async () =>
            {
                BitmapOpenRequests.VerifyTarget(context, requestedFilePath, cancellationToken);
                using DecodedImage decoded = await Task.Run(() => DecodeImage(requestedFilePath), cancellationToken);
                BitmapOpenRequests.VerifyTarget(context, requestedFilePath, cancellationToken);

                // Stage a private, editable copy before publishing any metadata for this request.
                BitmapSource source = decoded.BitmapSource;
                WriteableBitmap display = decoded.SourceStream != null || (source.IsFrozen && EditableBitmapCopy.ShouldCopyInBackground(source))
                    ? await EditableBitmapCopy.CreateAsync(source, cancellationToken)
                    : new WriteableBitmap(source);
                return (decoded.Metadata.ColorVisionParameters == null ? display : source, decoded.Metadata, display);
            }, cancellationToken);

            BitmapOpenRequests.VerifyTarget(context, requestedFilePath, cancellationToken);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, writeableBitmap.PixelWidth, nameof(CommonImageOpen), "位图像素宽度");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, writeableBitmap.PixelHeight, nameof(CommonImageOpen), "位图像素高度");
            ApplyMetadata(context, decodedMetadata);

            BitmapOpenRequests.VerifyTarget(context, requestedFilePath, cancellationToken);
            await context.ImageView.PublishDecodedImageContentAsync(
                new DecodedImageContent(original, decodedMetadata.ColorVisionParameters, requestedFilePath),
                writeableBitmap, cancellationToken);
        }

        private static DecodedImage DecodeImage(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".png")
                return DecodePng(filePath);

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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

        private static DecodedImage DecodePng(string filePath)
        {
            FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                BitmapFrame frame = ReadPngFrame(stream, filePath, BitmapCacheOption.None);
                ImageMetadata metadata = ReadMetadata(frame.Metadata as BitmapMetadata);
                // Ordinary PNGs can decode into the editable bitmap while the file is still open.
                if (metadata.ColorVisionParameters == null && frame.IsFrozen)
                    return new DecodedImage(frame, metadata, stream);

                // Measurement content must keep the original pixels available after closing the file.
                stream.Position = 0;
                frame = ReadPngFrame(stream, filePath, BitmapCacheOption.OnLoad);
                stream.Dispose();
                return new DecodedImage(frame, metadata);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static BitmapFrame ReadPngFrame(FileStream stream, string filePath, BitmapCacheOption cacheOption)
        {
            PngBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, cacheOption);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException($"PNG 文件不包含可显示帧：{filePath}");

            BitmapFrame frame = decoder.Frames[0];
            if (frame.CanFreeze && !frame.IsFrozen)
                frame.Freeze();
            return frame;
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

        private sealed record DecodedImage(BitmapSource BitmapSource, ImageMetadata Metadata, FileStream? SourceStream = null) : IDisposable
        {
            public void Dispose() => SourceStream?.Dispose();
        }

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
