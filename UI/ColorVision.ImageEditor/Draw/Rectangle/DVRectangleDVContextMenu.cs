using LocalizedText = global::ColorVision.ImageEditor.DisplayText;
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

            var cropSave = new MenuItem { Header = LocalizedText.Get("裁剪并另存...") };
            cropSave.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(_drawCanvas, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, LocalizedText.Get("提示"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show(LocalizedText.Get("保存成功"), LocalizedText.Get("裁剪"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(LocalizedText.Format($"保存失败: {ex.Message}"), LocalizedText.Get("裁剪"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            menuItems.Add(cropSave);

            var cropClipboard = new MenuItem { Header = LocalizedText.Get("裁剪复制到剪贴板") };
            cropClipboard.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(_drawCanvas, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, LocalizedText.Get("提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try
                {
                    Clipboard.SetImage(cropped);
                    MessageBox.Show(LocalizedText.Get("已复制到剪贴板"), LocalizedText.Get("裁剪"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(LocalizedText.Format($"复制失败: {ex.Message}"), LocalizedText.Get("裁剪"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            menuItems.Add(cropClipboard);

            var cropReplace = new MenuItem { Header = LocalizedText.Get("裁剪并替换当前图像") };
            cropReplace.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(_drawCanvas, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, LocalizedText.Get("提示"), MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (!ShapeGeometry.IsFinite(r) || r.Width <= 0 || r.Height <= 0)
            {
                error = "矩形大小无效";
                return false;
            }

            // Image renders into RenderSize in canvas coordinates. Before layout, use its natural DIP size.
            Size size = drawCanvas.RenderSize;
            if (!drawCanvas.IsArrangeValid || size.Width <= 0 || size.Height <= 0)
                size = new Size(source.Width, source.Height);
            double scaleX = source.PixelWidth / size.Width;
            double scaleY = source.PixelHeight / size.Height;
            int x = (int)Math.Floor(Math.Clamp(r.Left * scaleX, 0, source.PixelWidth));
            int y = (int)Math.Floor(Math.Clamp(r.Top * scaleY, 0, source.PixelHeight));
            int right = (int)Math.Ceiling(Math.Clamp(r.Right * scaleX, 0, source.PixelWidth));
            int bottom = (int)Math.Ceiling(Math.Clamp(r.Bottom * scaleY, 0, source.PixelHeight));
            if (right <= x || bottom <= y)
            {
                error = "矩形超出图像范围";
                return false;
            }

            try
            {
                cropped = new CroppedBitmap(source, new Int32Rect(x, y, right - x, bottom - y));
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
