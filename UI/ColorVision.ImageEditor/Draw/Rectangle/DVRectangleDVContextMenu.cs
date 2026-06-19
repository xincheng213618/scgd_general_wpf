#pragma warning disable CS8625,CS8602,CS8604,CS8600,CS0103,CS0067
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw
{
    public class DVRectangleDVContextMenu : IDVContextMenu
    {
        private readonly DrawCanvas _drawCanvas;
        private readonly ImageProcessingContext _imageContext;

        public DVRectangleDVContextMenu(DrawCanvas drawCanvas, ImageProcessingContext imageContext)
        {
            _drawCanvas = drawCanvas;
            _imageContext = imageContext;
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            var cropSave = new MenuItem { Header = "裁剪并另存..." };
            cropSave.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(_drawCanvas, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog dlg = new()
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|BMP Image|*.bmp|TIFF Image|*.tif;*.tiff",
                    FileName = $"Crop_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        EncodeAndSave(cropped, dlg.FileName);
                        MessageBox.Show("保存成功", "裁剪", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败: {ex.Message}", "裁剪", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            menuItems.Add(cropSave);

            var cropClipboard = new MenuItem { Header = "裁剪复制到剪贴板" };
            cropClipboard.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(_drawCanvas, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try
                {
                    Clipboard.SetImage(cropped);
                    MessageBox.Show("已复制到剪贴板", "裁剪", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"复制失败: {ex.Message}", "裁剪", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            menuItems.Add(cropClipboard);

            var cropReplace = new MenuItem { Header = "裁剪并替换当前图像" };
            cropReplace.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(_drawCanvas, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _imageContext.SetImageSource(new WriteableBitmap(cropped));
                _drawCanvas.Clear();
            };
            menuItems.Add(cropReplace);

            return menuItems;
        }

        private static bool TryGetCropBitmap(DrawCanvas drawCanvas, IRectangle dvRectangle, out BitmapSource? cropped, out string? error)
        {
            cropped = null;
            error = null;
            if (drawCanvas.Source is not BitmapSource source)
            {
                error = "当前图像源不可裁剪";
                return false;
            }

            Rect r = dvRectangle.Rect;
            if (r.Width <= 0 || r.Height <= 0)
            {
                error = "矩形大小无效";
                return false;
            }

            int x = (int)Math.Floor(r.X);
            int y = (int)Math.Floor(r.Y);
            int w = (int)Math.Ceiling(r.Width);
            int h = (int)Math.Ceiling(r.Height);

            if (x < 0) { w -= (0 - x); x = 0; }
            if (y < 0) { h -= (0 - y); y = 0; }
            if (x >= source.PixelWidth || y >= source.PixelHeight)
            {
                error = "矩形超出图像范围";
                return false;
            }
            if (x + w > source.PixelWidth) w = source.PixelWidth - x;
            if (y + h > source.PixelHeight) h = source.PixelHeight - y;
            if (w <= 0 || h <= 0)
            {
                error = "裁剪范围无效";
                return false;
            }

            try
            {
                cropped = new CroppedBitmap(source, new Int32Rect(x, y, w, h));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void EncodeAndSave(BitmapSource bitmap, string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            BitmapEncoder encoder = ext switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".bmp" => new BmpBitmapEncoder(),
                ".tif" or ".tiff" => new TiffBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }
    }
}
