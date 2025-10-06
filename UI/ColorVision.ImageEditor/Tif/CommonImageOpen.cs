#pragma warning disable CS8625
using ColorVision.Core;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".bmp|.jpg|.jpeg|.png|.webp|.ico|gif")]
    public record class CommonImageOpen(EditorContext EditorContext) : IImageOpen
    {
        public async void OpenImage(ImageView imageView, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            BitmapImage? bitmapImage = null;
            await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var tmp = new BitmapImage();
                tmp.BeginInit();
                tmp.CacheOption = BitmapCacheOption.OnLoad;
                tmp.StreamSource = stream;
                tmp.EndInit();
                tmp.Freeze();
                bitmapImage = tmp;
            });
            if (bitmapImage == null) return;
            imageView.SetImageSource(bitmapImage.ToWriteableBitmap());
            imageView.ComboBoxLayers.SelectedIndex = 0;
            imageView.ComboBoxLayers.ItemsSource = new List<string>() { "Src", "R", "G", "B" };
            imageView.AddSelectionChangedHandler(imageView.ComboBoxLayersSelectionChanged);
            imageView.UpdateZoomAndScale();
        }
        public List<MenuItemMetadata> GetContextMenuItems(ImageViewConfig imageView)
        {
            return new List<MenuItemMetadata>();
        }
    }

}
