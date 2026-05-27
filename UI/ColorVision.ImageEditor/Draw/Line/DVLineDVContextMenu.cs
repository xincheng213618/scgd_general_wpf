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
                        MessageBox.Show(ColorVision.ImageEditor.Properties.Resources.Annotation_NotEnoughPoints, ColorVision.ImageEditor.Properties.Resources.Draw_Tip, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 检查图像源是否为 WriteableBitmap
                    if (context.DrawCanvas.Source is WriteableBitmap writeableBitmap)
                    {
                        // 提取截面数据（支持多通道）
                        ProfileData profileData = ProfileDataExtractor.ExtractAlongPath(dVLine.Points, writeableBitmap);

                        if (profileData.SampleCount == 0)
                        {
                            MessageBox.Show(ColorVision.ImageEditor.Properties.Resources.Annotation_CannotExtractData, ColorVision.ImageEditor.Properties.Resources.Draw_Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // 创建并显示图表窗口
                        string chartTitle = $"{ColorVision.ImageEditor.Properties.Resources.Annotation_SectionalDrawingTitle} ({profileData.SampleCount} {ColorVision.ImageEditor.Properties.Resources.Annotation_SamplePoints})";
                        ProfileChartWindow profileChartWindow = new ProfileChartWindow(profileData, chartTitle)
                        {
                            Owner = Application.Current.GetActiveWindow()
                        };
                        profileChartWindow.Show();
                    }
                    else
                    {
                        MessageBox.Show(ColorVision.ImageEditor.Properties.Resources.Annotation_SourceNotReadable, ColorVision.ImageEditor.Properties.Resources.Draw_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                MenuItems.Add(menuItem);
            }
            return MenuItems;
        }
    }
}
