#pragma warning disable CS8625
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public interface IImageViewOpen
    {
        public List<string> Extension { get; }

        public void OpenImage(ImageView imageView, string? filePath);
    }


    public class Opentif: IImageViewOpen
    {
        public List<string> Extension =>new List<string>() { ".tif", ".tiff" };

        public async void OpenImage(ImageView imageView, string? filePath)
        {
            if (imageView.Config.IsShowLoadImage)
            {

                imageView.ComboBoxLayers.SelectedIndex = 0;
                imageView.ComboBoxLayers.ItemsSource = imageView.ComboBoxLayerItems;
                imageView.AddSelectionChangedHandler(imageView.ComboBoxLayers_SelectionChanged);

                imageView.WaitControl.Visibility = Visibility.Visible;
                await Task.Delay(30);
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var data = TiffReader.ReadTiff(filePath);
                        imageView.SetImageSource(new WriteableBitmap(data));
                        imageView.UpdateZoomAndScale();
                        imageView.WaitControl.Visibility = Visibility.Collapsed;
                    });
                });
            }
            else
            {
                var data = TiffReader.ReadTiff(filePath);
                imageView.SetImageSource(new WriteableBitmap(data));
                imageView.UpdateZoomAndScale();
            };

        }
    }

}
