#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        public int GetChannelCount(BitmapSource source)
        {
            PixelFormat format = source.Format;

            if (format == PixelFormats.Bgr24)
            {
                return 3; // BGR
            }
            else if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32)
            {
                return 4; // BGRA
            }
            else if (format == PixelFormats.Gray8)
            {
                return 1; // 灰度
            }
            else if (format == PixelFormats.Gray16)
            {
                return 1; // 灰度
            }
            else if (format == PixelFormats.Gray32Float)
            {
                return 1; // 灰度
            }
            else if (format == PixelFormats.Rgb24)
            {
                return 3; // RGB
            }
            else if (format == PixelFormats.Rgb48)
            {
                return 3; // RGB
            }
            else if (format == PixelFormats.Rgba64)
            {
                return 4; // RGBA 16位
            }
            else
            {
                throw new NotSupportedException("Unsupported pixel format");
            }
        }
        public async void OpenImage(ImageView imageView, string? filePath)
        {
            if (imageView.Config.IsShowLoadImage)
            {
                imageView.WaitControl.Visibility = Visibility.Visible;
                await Task.Delay(30);
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var data = TiffReader.ReadTiff(filePath);
                        int channel = GetChannelCount(data);
                        if (channel == 1)
                        {
                            imageView.ComboBoxLayers.SelectedIndex = 0;
                            imageView.ComboBoxLayers.ItemsSource = new List<string>() { "Src" };
                            imageView.AddSelectionChangedHandler(imageView.ComboBoxLayers_SelectionChanged);
                        }
                        else
                        {
                            imageView.ComboBoxLayers.SelectedIndex = 0;
                            imageView.ComboBoxLayers.ItemsSource = new List<string>() { "Src", "R", "G", "B" };
                            imageView.AddSelectionChangedHandler(imageView.ComboBoxLayers_SelectionChanged);
                        }


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

        public List<MenuItemMetadata> GetContextMenuItems(ImageView imageView)
        {
            return new List<MenuItemMetadata>();
        }


    }

}
