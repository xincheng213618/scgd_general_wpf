using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Line
{
    public class DVLineDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(DVLine);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is DVLine dVLine)
            {
                MenuItem menuItem = new() { Header = ColorVision.ImageEditor.Properties.Resources.SectionalDrawing };
                menuItem.Click += (s, e) =>
                {
                    if (dVLine.Points == null || dVLine.Points.Count < 2)
                    {
                        MessageBox.Show("该线没有足够的点来生成切面图。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 检查图像源是否为 WriteableBitmap
                    if (context.ImageView.ImageShow.Source is WriteableBitmap writeableBitmap)
                    {
                        // 提取截面数据（支持多通道）
                        ProfileData profileData = ProfileDataExtractor.ExtractAlongPath(dVLine.Points, writeableBitmap);

                        if (profileData.SampleCount == 0)
                        {
                            MessageBox.Show("无法从图像中提取有效的截面数据。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // 创建并显示图表窗口
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
