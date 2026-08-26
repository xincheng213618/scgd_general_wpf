#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using System;
using System.Collections.Generic;
using System.Linq;
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
            AlgorithmResultOverlay.ClearTagged(DrawContext, AlgorithmResultOverlay.FindLuminousAreaTag);
            long requestId = AlgorithmResultOverlay.BeginRequest(DrawContext, AlgorithmResultOverlay.FindLuminousAreaTag);

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
                    if (!ImageContext.IsCurrentImageRevision(revision) ||
                        !AlgorithmResultOverlay.IsCurrentRequest(DrawContext, AlgorithmResultOverlay.FindLuminousAreaTag, requestId)) return;

                    if (!detectionResult.HasValidCorners)
                    {
                        MessageBox.Show(LuminousAreaDetector.GetFailureMessage(detectionResult), "发光区定位", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    double pixelToDipX = LuminousAreaDetector.GetPixelToDipScale(
                        ImageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
                    double pixelToDipY = LuminousAreaDetector.GetPixelToDipScale(
                        ImageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
                    Point[] corners = detectionResult.Corners
                        .Select(corner => new Point(corner.X * pixelToDipX, corner.Y * pixelToDipY))
                        .ToArray();
                    double zoom = AlgorithmResultOverlay.GetZoom(DrawContext);
                    AlgorithmResultOverlay.AddPolygon(
                        DrawContext,
                        corners,
                        new Pen(Brushes.DeepSkyBlue, 1.5 / zoom),
                        AlgorithmResultOverlay.FindLuminousAreaTag);

                    Point center = new(corners.Average(point => point.X), corners.Average(point => point.Y));
                    double topLength = (corners[1] - corners[0]).Length;
                    double leftLength = (corners[3] - corners[0]).Length;
                    double armLength = Math.Clamp(Math.Min(topLength, leftLength) * 0.08, 24 / zoom, 240 / zoom);
                    Vector topDirection = corners[1] - corners[0];
                    if (topDirection.Length > 0)
                    {
                        topDirection.Normalize();
                        Vector verticalDirection = new(-topDirection.Y, topDirection.X);
                        Pen crossPen = new(Brushes.DeepSkyBlue, 1.5 / zoom);
                        AlgorithmResultOverlay.AddLine(DrawContext, center - topDirection * armLength, center + topDirection * armLength, crossPen, AlgorithmResultOverlay.FindLuminousAreaTag);
                        AlgorithmResultOverlay.AddLine(DrawContext, center - verticalDirection * armLength, center + verticalDirection * armLength, crossPen.CloneCurrentValue(), AlgorithmResultOverlay.FindLuminousAreaTag);
                    }

                    double centerPixelX = detectionResult.Corners.Average(point => point.X);
                    double centerPixelY = detectionResult.Corners.Average(point => point.Y);
                    LuminousAreaPoint lt = detectionResult.Corners[0];
                    LuminousAreaPoint rt = detectionResult.Corners[1];
                    double angle = Math.Atan2(rt.Y - lt.Y, rt.X - lt.X) * 180 / Math.PI;
                    string confidence = detectionResult.Confidence.HasValue ? detectionResult.Confidence.Value.ToString("F3") : "N/A";
                    string message = $"发光区  center=({centerPixelX:F3}, {centerPixelY:F3})  angle={angle:F4}°  confidence={confidence}";
                    Brush messageBrush = detectionResult.Warnings.Count == 0 ? Brushes.DeepSkyBlue : Brushes.Orange;
                    AlgorithmResultOverlay.AddLabel(DrawContext, center, message, messageBrush, AlgorithmResultOverlay.FindLuminousAreaTag);

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
            double left = dvRectangle.Rect.Left * DpiScaleX;
            double top = dvRectangle.Rect.Top * DpiScaleY;
            double right = dvRectangle.Rect.Right * DpiScaleX;
            double bottom = dvRectangle.Rect.Bottom * DpiScaleY;
            int x = (int)Math.Floor(left);
            int y = (int)Math.Floor(top);
            int w = (int)Math.Ceiling(right) - x;
            int h = (int)Math.Ceiling(bottom) - y;

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
