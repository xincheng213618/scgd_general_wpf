#pragma warning disable CS8625,CS8602,CS8604,CS8600,CS0103,CS0067
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ColorVision.ImageEditor.Draw
{
    /// <summary>
    /// DVRectangle �Ҽ��˵���ִ�вü�����
    /// ģ�� DVLineDVContextMenu �Ľṹ��
    /// </summary>
    public class DVRectangleDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            // �ü������
            var cropSave = new MenuItem { Header = "�ü������..." };
            cropSave.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(context, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, "��ʾ", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show("����ɹ�", "�ü�", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"����ʧ��: {ex.Message}", "�ü�", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            menuItems.Add(cropSave);

            // �ü������Ƶ�������
            var cropClipboard = new MenuItem { Header = "�ü����Ƶ�������" };
            cropClipboard.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(context, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, "��ʾ", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try
                {
                    Clipboard.SetImage(cropped);
                    MessageBox.Show("�Ѹ��Ƶ�������", "�ü�", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"����ʧ��: {ex.Message}", "�ü�", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            menuItems.Add(cropClipboard);

            // �òü�����滻��ǰͼ��
            var cropReplace = new MenuItem { Header = "�ü����滻��ǰͼ��" };
            cropReplace.Click += (s, e) =>
            {
                if (!TryGetCropBitmap(context, dvRectangle, out BitmapSource? cropped, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show(error, "��ʾ", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                context.ImageView.SetImageSource(new WriteableBitmap(cropped)) ;
                // ������л���Ԫ�أ���ѡ��
                context.ImageView.ImageShow.Clear();
            };
            menuItems.Add(cropReplace);

            return menuItems;
        }

        private static bool TryGetCropBitmap(EditorContext imageViewModel, IRectangle dvRectangle, out BitmapSource? cropped, out string? error)
        {
            cropped = null;
            error = null;
            if (imageViewModel.ImageView.ImageShow.Source is not BitmapSource source)
            {
                error = "��ǰͼ��Դ���ɲü�";
                return false;
            }

            Rect r = dvRectangle.Rect;
            if (r.Width <= 0 || r.Height <= 0)
            {
                error = "���δ�С��Ч";
                return false;
            }

            // ȡ����߽�ü�
            int x = (int)Math.Floor(r.X);
            int y = (int)Math.Floor(r.Y);
            int w = (int)Math.Ceiling(r.Width);
            int h = (int)Math.Ceiling(r.Height);

            // Clamp
            if (x < 0) { w -= (0 - x); x = 0; }
            if (y < 0) { h -= (0 - y); y = 0; }
            if (x >= source.PixelWidth || y >= source.PixelHeight)
            {
                error = "���γ���ͼ��Χ";
                return false;
            }
            if (x + w > source.PixelWidth) w = source.PixelWidth - x;
            if (y + h > source.PixelHeight) h = source.PixelHeight - y;
            if (w <= 0 || h <= 0)
            {
                error = "�ü���Χ��Ч";
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
            BitmapEncoder encoder;
            string ext = Path.GetExtension(path).ToLower();
            encoder = ext switch
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
