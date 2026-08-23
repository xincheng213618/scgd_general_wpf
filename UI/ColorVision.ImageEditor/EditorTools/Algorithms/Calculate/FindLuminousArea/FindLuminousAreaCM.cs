#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLuminousArea
{
    public record FindLuminousArea(ImageProcessingContext ImageContext, DrawEditorContext DrawContext)
    {
        public void Execute(FindLuminousAreaCorner findLuminousAreaCorner, RoiRect roiRect)
        {
            ImageFrameLease? lease = ImageContext.AcquireImageFrame();
            if (lease == null) return;

            long revision = lease.Revision;
            _ = Task.Run(() =>
            {
                LuminousAreaDetectionResult detectionResult;
                using (lease)
                {
                    detectionResult = LuminousAreaDetector.Detect(lease.Image, roiRect, findLuminousAreaCorner);
                }

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (!ImageContext.IsCurrentImageRevision(revision)) return;

                    if (!detectionResult.HasValidCorners)
                    {
                        MessageBox.Show(LuminousAreaDetector.GetFailureMessage(detectionResult), "发光区定位", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    DVPolygon polygon = new() { IsComple = true };
                    polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / DrawContext.Zoombox.ContentMatrix.M11);
                    polygon.Attribute.Brush = Brushes.Transparent;
                    double pixelToDipX = LuminousAreaDetector.GetPixelToDipScale(
                        ImageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
                    double pixelToDipY = LuminousAreaDetector.GetPixelToDipScale(
                        ImageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
                    foreach (LuminousAreaPoint corner in detectionResult.Corners)
                    {
                        polygon.Attribute.Points.Add(new Point(corner.X * pixelToDipX, corner.Y * pixelToDipY));
                    }
                    polygon.Render();
                    DrawContext.DrawCanvas.AddVisualCommand(polygon);
                    string warningMessage = LuminousAreaDetector.GetWarningMessage(detectionResult);
                    if (!string.IsNullOrEmpty(warningMessage))
                    {
                        MessageBox.Show(warningMessage, "发光区定位（需复核）", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            });
        }
    }
    public class DVCMFindLuminousArea : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;

        public DVCMFindLuminousArea(ImageProcessingContext imageContext, DrawEditorContext drawContext, ImageViewConfig config)
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
            double DpiScaleX = LuminousAreaDetector.GetDipToPixelScale(
                _config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double DpiScaleY = LuminousAreaDetector.GetDipToPixelScale(
                _config.GetProperties<double>(ImageViewPropertyKeys.DpiY));

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

            var menuItem = new MenuItem { Header = "FindLuminousArea" };
            menuItem.Click += (s, e) =>
            {
                FindLuminousAreaCorner findLuminousAreaCorner = new FindLuminousAreaCorner();
                var PropertyEditorWindow = new PropertyEditorWindow(findLuminousAreaCorner) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submitted += (_, _) =>
                {
                    new FindLuminousArea(_imageContext, _drawContext).Execute(findLuminousAreaCorner, new RoiRect(roiX, roiY, roiW,roiH));
                };
                PropertyEditorWindow.ShowDialog();
            };
            menuItems.Add(menuItem);
           return menuItems;
        }
    }

    public record class CMFindLuminousArea(ImageProcessingContext ImageContext, DrawEditorContext DrawContext) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();

            RelayCommand FindLuminousAreaCommand = new(o =>
            {
                FindLuminousAreaCorner findLuminousAreaCorner = new FindLuminousAreaCorner();
                var PropertyEditorWindow = new PropertyEditorWindow(findLuminousAreaCorner) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submitted += (_, _) =>
                {
                    new FindLuminousArea(ImageContext, DrawContext).Execute(findLuminousAreaCorner, new RoiRect());
                };
                PropertyEditorWindow.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "AlgorithmsCall",
                GuidId = "FindLuminousAreaCorner",
                Order = 1,
                Header = "FindLuminousAreaCorner",
                Command = FindLuminousAreaCommand
            });
            return MenuItemMetadatas;
        }
    }
}
