#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    public class Opentif : IImageViewOpen, IFileProcessor
    {
        public string GetExtension() => "图像文件 (*.tif)|*.tif";

        public List<string> Extension => new List<string>() { ".tif", ".tiff" };

        public int Order => -1;

        public bool CanExport(string filePath)
        {
            return false;
        }

        public bool CanProcess(string filePath)
        {
            return Extension.Contains(System.IO.Path.GetExtension(filePath).ToLower());
        }

        public void Process(string filePath)
        {
            ImageView imageView = new ImageView();
            Window window = new() { Title = "快速预览" };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(filePath);
            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageEditViewMode.ClearImage();
                }));
            }
        }

        public void Export(string filePath)
        {

        }

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

        public List<MenuItem> GetContextMenuItems(ImageView imageView)
        {
            return new List<MenuItem>();
        }


    }

}
