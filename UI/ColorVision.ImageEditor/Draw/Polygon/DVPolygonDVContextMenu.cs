using ColorVision.ImageEditor.Draw.Line;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Polygon
{
    public class DVPolygonDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(DVPolygon);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is DVPolygon dvPolygon)
            {
                MenuItem menuItem = new() { Header = ColorVision.ImageEditor.Properties.Resources.SectionalDrawing };
                menuItem.Click += (s, e) =>
                {
                    if (dvPolygon.Points == null || dvPolygon.Points.Count < 2)
                    {
                        MessageBox.Show("该多边形没有足够的点来生成切面图。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Check if image source is WriteableBitmap
                    if (context.ImageView.ImageShow.Source is WriteableBitmap writeableBitmap)
                    {
                        // Extract profile data along the polygon edges (with multi-channel support)
                        ProfileData profileData = ProfileDataExtractor.ExtractAlongPath(
                            dvPolygon.Points, 
                            writeableBitmap, 
                            500, 
                            dvPolygon.IsComple);

                        if (profileData.SampleCount == 0)
                        {
                            MessageBox.Show("无法从图像中提取有效的截面数据。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Create and display the chart window
                        string chartTitle = $"切面图 ({profileData.SampleCount} 个采样点)";
                        ProfileChartWindow profileChartWindow = new ProfileChartWindow(profileData, chartTitle)
                        {
                            Owner = Application.Current.GetActiveWindow()
                        };
                        profileChartWindow.Show();
                    }
                    else
                    {
                        MessageBox.Show("图像源不是可读的 WriteableBitmap 格式。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                MenuItems.Add(menuItem);
            }
            return MenuItems;
        }
    }
}
