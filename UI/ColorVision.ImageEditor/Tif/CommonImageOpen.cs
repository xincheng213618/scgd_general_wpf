using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".bmp|.jpg|.jpeg|.png|.webp|.ico|gif")]
    public record class CommonImageOpen(EditorContext EditorContext) : IImageOpen
    {
        public async void OpenImage(EditorContext context, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // Get file metadata
            FileInfo fileInfo = new FileInfo(filePath);
            context.Config.AddProperties("FileSource", filePath);
            context.Config.AddProperties("FileName", fileInfo.Name);
            context.Config.AddProperties("FileSize", fileInfo.Length);
            context.Config.AddProperties("FileCreationTime", fileInfo.CreationTime);
            context.Config.AddProperties("FileModifiedTime", fileInfo.LastWriteTime);

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
            context.Config.AddProperties("ImageWidth", bitmapImage.PixelWidth);
            context.Config.AddProperties("ImageHeight", bitmapImage.PixelHeight);
            
            // Add EXIF metadata if available
            if (metadata != null)
            {
                try
                {
                    if (metadata.CameraModel != null)
                        context.Config.AddProperties("CameraModel", metadata.CameraModel);
                    if (metadata.CameraManufacturer != null)
                        context.Config.AddProperties("CameraManufacturer", metadata.CameraManufacturer);
                    if (metadata.DateTaken != null)
                        context.Config.AddProperties("DateTaken", metadata.DateTaken);
                    if (metadata.ApplicationName != null)
                        context.Config.AddProperties("ApplicationName", metadata.ApplicationName);
                    if (metadata.Title != null)
                        context.Config.AddProperties("ImageTitle", metadata.Title);
                    if (metadata.Subject != null)
                        context.Config.AddProperties("ImageSubject", metadata.Subject);
                }
                catch
                {
                    // Silently ignore metadata extraction errors
                }
            }
            
            context.ImageView.SetImageSource(bitmapImage.ToWriteableBitmap());
            context.ImageView.ComboBoxLayers.SelectedIndex = 0;
            context.ImageView.ComboBoxLayers.ItemsSource = new List<string>() { "Src", "R", "G", "B" };
            context.ImageView.AddSelectionChangedHandler(context.ImageView.ComboBoxLayersSelectionChanged);
            context.ImageView.UpdateZoomAndScale();
        }
    }

}
