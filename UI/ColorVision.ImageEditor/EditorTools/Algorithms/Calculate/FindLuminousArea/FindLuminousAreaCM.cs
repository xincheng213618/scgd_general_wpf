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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLuminousArea
{
    public record FindLuminousArea(EditorContext Context)
    {
        public void Execute(FindLuminousAreaCorner findLuminousAreaCorner, RoiRect roiRect)
        {
            if (Context.ImageView.HImageCache == null) return;

            string FindLuminousAreajson = findLuminousAreaCorner.ToJsonN();
            Task.Run(() =>
            {
                int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)Context.ImageView.HImageCache, roiRect, FindLuminousAreajson, out IntPtr resultPtr);
                if (length > 0)
                {
                    string result = Marshal.PtrToStringAnsi(resultPtr);
                    OpenCVMediaHelper.FreeResult(resultPtr);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (findLuminousAreaCorner.UseRotatedRect)
                        {
                            var jObj = Newtonsoft.Json.Linq.JObject.Parse(result);
                            var corners = jObj["Corners"].ToObject<List<List<float>>>();
                            if (corners.Count == 4)
                            {
                                List<Point> pts_src = new();
                                pts_src.Add(new Point(roiRect.X + (int)corners[0][0],roiRect.Y + (int)corners[0][1]));
                                pts_src.Add(new Point(roiRect.X + (int)corners[1][0],roiRect.Y + (int)corners[1][1]));
                                pts_src.Add(new Point(roiRect.X + (int)corners[2][0],roiRect.Y + (int)corners[2][1]));
                                pts_src.Add(new Point(roiRect.X + (int)corners[3][0],roiRect.Y + (int)corners[3][1]));
                                List<Point> result1 = Helpers.SortPolyPoints(pts_src);

                                DVPolygon Polygon = new DVPolygon() { IsComple = true };
                                Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / Context.Zoombox.ContentMatrix.M11);
                                Polygon.Attribute.Brush = Brushes.Transparent;
                                Polygon.Attribute.Points.Add(result1[0]);
                                Polygon.Attribute.Points.Add(result1[1]);
                                Polygon.Attribute.Points.Add(result1[2]);
                                Polygon.Attribute.Points.Add(result1[3]);
                                Polygon.Render();
                                Context.ImageView.ImageShow.AddVisualCommand(Polygon);
                            }
                        }
                        else
                        {
                            MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);
                           
                            List<Point> pts_src = new();
                            pts_src.Add(new Point(roiRect.X + rect.X, roiRect.Y + rect.Y));
                            pts_src.Add(new Point(roiRect.X + rect.X + rect.Width, roiRect.Y + rect.Y));
                            pts_src.Add(new Point(roiRect.X + rect.X + rect.Width, roiRect.Y + rect.Y + rect.Height));
                            pts_src.Add(new Point(roiRect.X + rect.X, roiRect.Y + rect.Y + rect.Height));
                            List<Point> result1 = Helpers.SortPolyPoints(pts_src);

                            DVPolygon Polygon = new DVPolygon() { IsComple = true };
                            Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / Context.Zoombox.ContentMatrix.M11);
                            Polygon.Attribute.Brush = Brushes.Transparent;
                            Polygon.Attribute.Points.Add(result1[0]);
                            Polygon.Attribute.Points.Add(result1[1]);
                            Polygon.Attribute.Points.Add(result1[2]);
                            Polygon.Attribute.Points.Add(result1[3]);
                            Polygon.Render();
                            Context.ImageView.ImageShow.AddVisualCommand(Polygon);
                        }

                    });
                }
                else
                {
                    Console.WriteLine("Error occurred, code: " + length);
                }
            });
        }
    }
    public class DVCMFindLuminousArea : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            if (context.ImageView.HImageCache is not HImage hImage) return menuItems;
            double DpiX = context.Config.GetProperties<double>("DpiX");
            double DpiY = context.Config.GetProperties<double>("DpiY");

            double DpiSacleX = DpiX / 96.0;
            double DpiSacleY = DpiY / 96.0; // 每毫米多少像素

            // 图像尺寸
            int imgWidth = hImage.cols;
            int imgHeight = hImage.rows;

            // 用户绘制的矩形
            int x = (int)Math.Round(dvRectangle.Rect.X * DpiSacleX);
            int y = (int)Math.Round(dvRectangle.Rect.Y * DpiSacleY);
            int w = (int)Math.Round(dvRectangle.Rect.Width * DpiSacleX);
            int h = (int)Math.Round(dvRectangle.Rect.Height * DpiSacleY);

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
                PropertyEditorWindow.Submited += (_, _) =>
                {
                    new FindLuminousArea(context).Execute(findLuminousAreaCorner, new RoiRect(roiX, roiY, roiW,roiH));
                };
                PropertyEditorWindow.ShowDialog();
            };
            menuItems.Add(menuItem);
           return menuItems;
        }
    }

    public record class CMFindLuminousArea(EditorContext Context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();

            RelayCommand FindLuminousAreaCommand = new(o =>
            {
                FindLuminousAreaCorner findLuminousAreaCorner = new FindLuminousAreaCorner();
                var PropertyEditorWindow = new PropertyEditorWindow(findLuminousAreaCorner) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                PropertyEditorWindow.Submited += (_, _) =>
                {
                    new FindLuminousArea(Context).Execute(findLuminousAreaCorner, new RoiRect());
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
