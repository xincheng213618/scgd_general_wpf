using LocalizedText = global::ColorVision.ImageEditor.DisplayText;
using ColorVision.ImageEditor.Algorithms;
#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLightBeads
{
    public record FindLightBeads(ImageProcessingContext ImageContext, DrawEditorContext DrawContext)
    {
        public async Task ExecuteAsync(FindLightBeadsConfig config, RoiRect roiRect)
        {
            long revision = ImageContext.ImageRevision;
            int threshold = config.Threshold, minSize = config.MinSize, maxSize = Math.Max(0, config.MaxSize);
            int rows = Math.Max(0, config.Rows), columns = Math.Max(0, config.Cols), radius = config.Radius;
            bool inspectDefects = config.InspectDefects;
            double dimRatio = config.DimRatio;
            try
            {
                if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(config), LocalizedText.Get("标注半径必须大于零。"));
                BitmapSource source = ImageContext.ViewBitmapSource as BitmapSource
                    ?? throw new InvalidOperationException(LocalizedText.Get("当前没有可检测的图像。"));
                // Reuse the format-aware boundary: RGB48 must not be passed as BGR48.
                // Copy the WPF source once instead of creating another full-sized cached HImage.
                using AlgorithmImageBuffer snapshot = ImageAlgorithmInputFactory.Copy(source);
                var (result, inspection) = await Task.Run(() =>
                    InspectSnapshot(snapshot, roiRect, threshold, minSize, maxSize, rows, columns, dimRatio, inspectDefects));
                if (!ImageContext.IsCurrentImageRevision(revision)) return;
                var visual = inspection == null
                    ? new DensePointVisual(result.Centers.Select(point => new Point(point.X, point.Y)),
                        result.DarkCandidates.Select(point => new Point(point.X, point.Y)), radius, DrawContext.DrawCanvas.Scale)
                    : new DensePointVisual(inspection.Points.Where(point => point.Status == LightBeadStatus.Normal).Select(point => new Point(point.X, point.Y)),
                        inspection.Points.Where(point => point.Status != LightBeadStatus.Normal).Select(point => new Point(point.X, point.Y)),
                        radius, DrawContext.DrawCanvas.Scale, Brushes.LimeGreen, Brushes.Red);
                DrawContext.DrawCanvas.AddVisualCommand(visual);
                if (inspection != null) new LightBeadsResultWindow(inspection)
                {
                    Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.Show();
            }
            catch (Exception ex)
            {
                if (ImageContext.IsCurrentImageRevision(revision))
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, LocalizedText.Get("灯珠定位"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static unsafe (LightBeadsDetectionResult, LightBeadsInspectionResult?) InspectSnapshot(AlgorithmImageBuffer snapshot,
            RoiRect roi, int threshold, int minSize, int maxSize, int rows, int columns, double dimRatio, bool inspect)
        {
            using var pin = snapshot.Data.Pin();
            var image = new HImage { cols = snapshot.Width, rows = snapshot.Height, channels = snapshot.Format.Channels(),
                depth = snapshot.Format.BitsPerChannel(), stride = snapshot.Stride, pData = (IntPtr)pin.Pointer, isDispose = true };
            LightBeadsDetectionResult detection = LightBeadsDetector.Detect(image, roi, threshold, minSize, maxSize);
            return (detection, inspect ? LightBeadsInspector.Inspect(image, detection.Centers, rows, columns, dimRatio, threshold) : null);
        }
    }
    public class DVCMFindLightBeads : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;

        public DVCMFindLightBeads(ImageProcessingContext imageContext, DrawEditorContext drawContext, ImageViewConfig config)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
            _config = config;
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            using ImageFrameLease? lease = _imageContext.AcquireImageFrame();
            if (lease == null) return menuItems;
            HImage hImage = lease.Image;
            double DpiX = _config.GetProperties<double>("DpiX");
            double DpiY = _config.GetProperties<double>("DpiY");

            double DpiScaleX = DpiX / 96.0;
            double DpiScaleY = DpiY / 96.0;

            // 图像尺寸
            int imgWidth = hImage.cols;
            int imgHeight = hImage.rows;

            // 用户绘制的矩形
            int x = (int)Math.Round(dvRectangle.Rect.X * DpiScaleX);
            int y = (int)Math.Round(dvRectangle.Rect.Y * DpiScaleY);
            int w = (int)Math.Round(dvRectangle.Rect.Width * DpiScaleX);
            int h = (int)Math.Round(dvRectangle.Rect.Height * DpiScaleY);

            // 先保证宽高为正
            if (w <= 0 || h <= 0)
            {
                return menuItems;
            }

            // 与图像交集：裁剪到 [0, imgWidth/Height)
            int x2 = x + w;
            int y2 = y + h;

            int roiX = Math.Max(0, x);
            int roiY = Math.Max(0, y);
            int roiX2 = Math.Min(imgWidth, x2);
            int roiY2 = Math.Min(imgHeight, y2);

            int roiW = roiX2 - roiX;
            int roiH = roiY2 - roiY;

            // 如果没有交集或太小，则直接提示
            if (roiW <= 0 || roiH <= 0)
            {
                return menuItems;
            }

            var menuItem = new MenuItem { Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_FindLightBeads + "..." };
            menuItem.Click += (s, e) =>
            {
                FindLightBeadsConfig config = new FindLightBeadsConfig();
                var PropertyEditorWindow = new PropertyEditorWindow(config) { Title = ColorVision.ImageEditor.Properties.Resources.Algorithm_FindLightBeads, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submitted += async (_, _) =>
                {
                    await new FindLightBeads(_imageContext, _drawContext).ExecuteAsync(config, new RoiRect(roiX, roiY, roiW, roiH));
                };
                PropertyEditorWindow.ShowDialog();
            };
            menuItems.Add(menuItem);
            return menuItems;
        }
    }

    public record class CMFindLightBeads(ImageProcessingContext ImageContext, DrawEditorContext DrawContext) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();

            RelayCommand FindLightBeadsCommand = new(o =>
            {
                FindLightBeadsConfig config = new FindLightBeadsConfig();
                var PropertyEditorWindow = new PropertyEditorWindow(config) { Title = ColorVision.ImageEditor.Properties.Resources.Algorithm_FindLightBeads, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submitted += async (_, _) =>
                {
                    await new FindLightBeads(ImageContext, DrawContext).ExecuteAsync(config, new RoiRect());
                };
                PropertyEditorWindow.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = AlgorithmMenuGroups.Localization.Id,
                GuidId = "FindLightBeads",
                Order = 3,
                Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_FindLightBeads + "...",
                Command = FindLightBeadsCommand
            });
            return MenuItemMetadatas;
        }
    }
}
