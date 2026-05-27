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
                        MessageBox.Show(ColorVision.ImageEditor.Properties.Resources.Annotation_NotEnoughPoints, ColorVision.ImageEditor.Properties.Resources.Draw_Tip, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Check if image source is WriteableBitmap
                    if (context.DrawCanvas.Source is WriteableBitmap writeableBitmap)
                    {
                        // Extract profile data along the polygon edges (with multi-channel support)
                        ProfileData profileData = ProfileDataExtractor.ExtractAlongPath(
                            dvPolygon.Points, 
                            writeableBitmap, 
                            500, 
                            dvPolygon.IsComple);

                        if (profileData.SampleCount == 0)
                        {
                            MessageBox.Show(ColorVision.ImageEditor.Properties.Resources.Annotation_CannotExtractData, ColorVision.ImageEditor.Properties.Resources.Draw_Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Create and display the chart window
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
