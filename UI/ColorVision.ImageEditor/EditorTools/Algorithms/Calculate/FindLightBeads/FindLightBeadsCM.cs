using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLightBeads
{
    public record FindLightBeads(EditorContext Context)
    {
        public void Execute(FindLightBeadsConfig config, RoiRect roiRect)
        {
            if (Context.ImageView.HImageCache == null) return;

            string configJson = config.ToJsonN();
            Task.Run(() =>
            {
                int length = OpenCVMediaHelper.M_FindLightBeads((HImage)Context.ImageView.HImageCache, roiRect, configJson, out IntPtr resultPtr);
                if (length > 0)
                {
                    string result = Marshal.PtrToStringAnsi(resultPtr);
                    OpenCVMediaHelper.FreeResult(resultPtr);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(result);
                        
                        // 获取检测到的灯珠中心点
                        var centers = jObj["Centers"].ToObject<List<List<int>>>();
                        int centerCount = jObj["CenterCount"].ToObject<int>();

                        // 获取缺失的灯珠位置
                        var blackCenters = jObj["BlackCenters"].ToObject<List<List<int>>>();
                        int blackCenterCount = jObj["BlackCenterCount"].ToObject<int>();

                        // 绘制检测到的灯珠（蓝色小圆圈）
                        foreach (var center in centers)
                        {
                            int cx = roiRect.X + center[0];
                            int cy = roiRect.Y + center[1];

                            DVCircle circle = new DVCircle();
                            circle.Attribute.Center = new Point(cx, cy);
                            circle.Attribute.Radius = 4;
                            circle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Context.Zoombox.ContentMatrix.M11);
                            circle.Attribute.Brush = Brushes.Transparent;
                            circle.IsComple = true;
                            circle.Render();
                            Context.ImageView.ImageShow.AddVisualCommand(circle);
                        }

                        // 绘制缺失的灯珠（红色小矩形）
                        foreach (var blackCenter in blackCenters)
                        {
                            int cx = roiRect.X + blackCenter[0];
                            int cy = roiRect.Y + blackCenter[1];

                            DVRectangle rect = new DVRectangle();
                            rect.Attribute.Rect = new Rect(cx - 5, cy - 5, 10, 10);
                            rect.Attribute.Pen = new Pen(Brushes.Red, 1 / Context.Zoombox.ContentMatrix.M11);
                            rect.Attribute.Brush = Brushes.Transparent;
                            rect.IsComple = true;
                            rect.Render();
                            Context.ImageView.ImageShow.AddVisualCommand(rect);
                        }

                        // 显示统计信息
                        int expectedCount = jObj["ExpectedCount"].ToObject<int>();
                        int missingCount = jObj["MissingCount"].ToObject<int>();
                        
                        MessageBox.Show(
                            $"检测结果:\n" +
                            $"检测到的灯珠: {centerCount}\n" +
                            $"缺失的灯珠: {blackCenterCount}\n" +
                            $"预期总数: {expectedCount}\n" +
                            $"实际缺失: {missingCount}",
                            "灯珠检测结果",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"灯珠检测失败，错误代码: {length}\n请检查图像格式和参数设置。",
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            });
        }
    }

    public class DVCMFindLightBeads : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            if (context.ImageView.HImageCache is not HImage hImage) return menuItems;
            double DpiX = context.Config.GetProperties<double>("DpiX");
            double DpiY = context.Config.GetProperties<double>("DpiY");

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

            var menuItem = new MenuItem { Header = "FindLightBeads" };
            menuItem.Click += (s, e) =>
            {
                FindLightBeadsConfig config = new FindLightBeadsConfig();
                var PropertyEditorWindow = new PropertyEditorWindow(config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submited += (_, _) =>
                {
                    new FindLightBeads(context).Execute(config, new RoiRect(roiX, roiY, roiW, roiH));
                };
                PropertyEditorWindow.ShowDialog();
            };
            menuItems.Add(menuItem);
            return menuItems;
        }
    }

    public record class CMFindLightBeads(EditorContext Context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();

            RelayCommand FindLightBeadsCommand = new(o =>
            {
                FindLightBeadsConfig config = new FindLightBeadsConfig();
                var PropertyEditorWindow = new PropertyEditorWindow(config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submited += (_, _) =>
                {
                    new FindLightBeads(Context).Execute(config, new RoiRect());
                };
                PropertyEditorWindow.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "AlgorithmsCall",
                GuidId = "FindLightBeads",
                Order = 2,
                Header = "FindLightBeads",
                Command = FindLightBeadsCommand
            });
            return MenuItemMetadatas;
        }
    }
}
