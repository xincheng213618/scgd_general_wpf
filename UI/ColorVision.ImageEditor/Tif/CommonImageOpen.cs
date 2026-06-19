using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".bmp|.jpg|.jpeg|.png|.webp|.ico|.gif")]
    public record class CommonImageOpen(EditorContext EditorContext) : IImageOpen
    {
        public async void OpenImage(EditorContext context, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // Get file metadata
            FileInfo fileInfo = new FileInfo(filePath);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, filePath, nameof(CommonImageOpen), "打开器接收到的源文件路径");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, fileInfo.Name, nameof(CommonImageOpen), "当前文件名");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileInfo.Length, nameof(CommonImageOpen), "当前文件大小（字节）");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, fileInfo.CreationTime, nameof(CommonImageOpen), "当前文件创建时间");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, fileInfo.LastWriteTime, nameof(CommonImageOpen), "当前文件修改时间");

            BitmapImage? bitmapImage = null;
            BitmapMetadata? metadata = null;
            
            await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                // Try to get metadata using appropriate decoder
                BitmapDecoder? decoder = null;
                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                
                try
                {
                    if (ext == ".jpg" || ext == ".jpeg")
                        decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    else if (ext == ".png")
                        decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    else if (ext == ".bmp")
                        decoder = new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    
                    // Extract metadata if decoder was created and has frames
                    if (decoder != null && decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        metadata = frame.Metadata as BitmapMetadata;
                    }
                }
                catch
                {
                    // If decoder fails, fall back to standard BitmapImage loading
                }
                
                // Reset stream position for BitmapImage
                stream.Position = 0;
                var tmp = new BitmapImage();
                tmp.BeginInit();
                tmp.CacheOption = BitmapCacheOption.OnLoad;
                tmp.StreamSource = stream;
                tmp.EndInit();
                tmp.Freeze();
                bitmapImage = tmp;
            });
            
            if (bitmapImage == null) return;
            
            // Add image dimensions
            context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, bitmapImage.PixelWidth, nameof(CommonImageOpen), "位图像素宽度");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, bitmapImage.PixelHeight, nameof(CommonImageOpen), "位图像素高度");
            
            // Add EXIF metadata if available
            if (metadata != null)
            {
                try
                {
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
                catch
                {
                    // Silently ignore metadata extraction errors
                }
            }
            
            context.ImageView.SetImageSource(bitmapImage.ToWriteableBitmap());
            context.ImageView.UpdateZoomAndScale();
        }
    }

}
