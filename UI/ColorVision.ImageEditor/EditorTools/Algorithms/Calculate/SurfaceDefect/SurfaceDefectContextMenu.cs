#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SurfaceDefect
{
    public sealed class SurfaceDefectEditorTool
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;

        public SurfaceDefectEditorTool(ImageProcessingContext imageContext, DrawEditorContext drawContext)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
        }

        public void Execute()
        {
            if (_imageContext.HImageCache is not HImage hImage)
            {
                return;
            }

            OpenWindow(new RoiRect(0, 0, hImage.cols, hImage.rows));
        }

        public void Execute(RoiRect roi)
        {
            OpenWindow(roi);
        }

        private void OpenWindow(RoiRect roi)
        {
            SurfaceDefectDebugWindow window = new(_imageContext, _drawContext, roi)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }
    }

    public sealed record class CMSurfaceDefect(ImageProcessingContext ImageContext, DrawEditorContext DrawContext) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand command = new(o =>
            {
                SurfaceDefectEditorTool tool = new(ImageContext, DrawContext);
                tool.Execute();
            });

            return new List<MenuItemMetadata>
            {
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "SurfaceDefectMura",
                    Order = 4,
                    Header = "表面缺陷/Mura 检测",
                    Command = command
                }
            };
        }
    }

    public sealed class SurfaceDefectIDVContextMenu : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;

        public SurfaceDefectIDVContextMenu(ImageProcessingContext imageContext, DrawEditorContext drawContext, ImageViewConfig config)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
            _config = config;
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle rectangle || _imageContext.HImageCache is not HImage image)
            {
                return menuItems;
            }

            if (!TryBuildRoi(rectangle, image, out RoiRect roi))
            {
                return menuItems;
            }

            MenuItem item = new() { Header = "表面缺陷/Mura 检测" };
            item.Click += (_, _) => new SurfaceDefectEditorTool(_imageContext, _drawContext).Execute(roi);
            menuItems.Add(item);
            return menuItems;
        }

        private bool TryBuildRoi(IRectangle rectangle, HImage image, out RoiRect roi)
        {
            roi = new RoiRect();

            double dpiScaleX = _config.GetProperties<double>("DpiX") / 96.0;
            double dpiScaleY = _config.GetProperties<double>("DpiY") / 96.0;
            int x = (int)Math.Round(rectangle.Rect.X * dpiScaleX);
            int y = (int)Math.Round(rectangle.Rect.Y * dpiScaleY);
            int w = (int)Math.Round(rectangle.Rect.Width * dpiScaleX);
            int h = (int)Math.Round(rectangle.Rect.Height * dpiScaleY);

            if (w <= 0 || h <= 0)
            {
                return false;
            }

            int roiX = Math.Max(0, x);
            int roiY = Math.Max(0, y);
            int roiX2 = Math.Min(image.cols, x + w);
            int roiY2 = Math.Min(image.rows, y + h);
            int roiW = roiX2 - roiX;
            int roiH = roiY2 - roiY;

            if (roiW <= 0 || roiH <= 0)
            {
                return false;
            }

            roi = new RoiRect(roiX, roiY, roiW, roiH);
            return true;
        }
    }
}
