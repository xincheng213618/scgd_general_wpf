#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    public class Opentif : IImageOpen, IFileProcessor
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
            return Extension.Contains(System.IO.Path.GetExtension(filePath).ToLower(System.Globalization.CultureInfo.CurrentCulture));
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
                    imageView.ImageViewModel.ClearImage();
                }));
            }
        }

        public void Export(string filePath)
        {

        }

        public static int GetChannelCount(BitmapSource source)
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


        public static WriteableBitmap ConvertGray32FloatToBitmapSource(BitmapSource bitmapSource)
        {
            // 确保图像数据已加载
            bitmapSource.Freeze();

            // 获取图像的宽度和高度
            int width = bitmapSource.PixelWidth;
            int height = bitmapSource.PixelHeight;

            // 创建一个新的32位浮点数组来存储像素数据
            float[] floatPixels = new float[width * height];

            // 从BitmapSource中读取像素数据
            bitmapSource.CopyPixels(floatPixels, width * 4, 0);


            // 创建一个新的16位整数数组来存储转换后的像素值
            ushort[] ushortPixels = new ushort[width * height];

            // 将浮点值转换为0-65535范围的16位整数
            for (int i = 0; i < floatPixels.Length; i++)
            {
                ushortPixels[i] = (ushort)(floatPixels[i] * 65535);
            }
            // 创建一个新的WriteableBitmap对象
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray16, null);
            // 写入像素数据
            int stride = width * 2; // 每行像素数据的字节数（16位，即2字节）
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), ushortPixels, stride, 0);

            return writeableBitmap;
        }

        public async void OpenImage(ImageView imageView, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
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
                            imageView.AddSelectionChangedHandler(imageView.ComboBoxLayersSelectionChanged);
                        }
                        else
                        {
                            imageView.ComboBoxLayers.SelectedIndex = 0;
                            imageView.ComboBoxLayers.ItemsSource = new List<string>() { "Src", "R", "G", "B" };
                            imageView.AddSelectionChangedHandler(imageView.ComboBoxLayersSelectionChanged);
                        }

                        //if (data.Format == PixelFormats.Gray32Float)
                        //{
                        //    WriteableBitmap writeableBitmap = new WriteableBitmap(data);
                        //    HImage hImage = writeableBitmap.ToHImage();
                        //    int i = OpenCVMediaHelper.M_ConvertGray32Float(hImage, out HImage hImage1);
                        //    imageView.SetImageSource(hImage1.ToWriteableBitmap());
                        //    OpenCVMediaHelper.M_FreeHImageData(hImage1.pData);
                        //    hImage.Dispose();
                        //}
                        //else
                        //{
                        //}
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

        public List<MenuItemMetadata> GetContextMenuItems(ImageViewConfig imageView)
        {
            return new List<MenuItemMetadata>();
        }
    }

}
