using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
                MenuItem menuItem = new() { Header = "切面图" };
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
                        // Extract profile data along the polygon edges
                        List<double> profileData = ExtractProfileData(dvPolygon, writeableBitmap);

                        if (profileData.Count == 0)
                        {
                            MessageBox.Show("无法从图像中提取有效的截面数据。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Create and display the chart window
                        string chartTitle = $"切面图 ({profileData.Count} 个采样点)";
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


        /// <summary>
        /// Extract profile data along the polygon edges from WriteableBitmap.
        /// </summary>
        /// <param name="polygon">DVPolygon object containing the point path.</param>
        /// <param name="bitmap">WriteableBitmap to extract data from.</param>
        /// <param name="totalSteps">Total number of sample points, default is 500.</param>
        /// <returns>List of pixel values along the path.</returns>
        private List<double> ExtractProfileData(DVPolygon polygon, WriteableBitmap bitmap, int totalSteps = 500)
        {
            var profileData = new List<double>();
            var points = polygon.Points;

            if (points == null || points.Count < 2)
                return profileData;

            // Calculate total length of the polygon edges
            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += (points[i + 1] - points[i]).Length;
            }

            // If polygon is complete, add the closing edge
            if (polygon.IsComple && points.Count > 0)
            {
                totalLength += (points[0] - points[points.Count - 1]).Length;
            }

            if (totalLength <= 0) return profileData;

            // Sample along the path at equal distances
            double stepDistance = totalLength / (totalSteps - 1);
            double accumulatedLength = 0;

            // Process all edges
            int edgeCount = polygon.IsComple ? points.Count : points.Count - 1;
            
            for (int i = 0; i < edgeCount; i++)
            {
                Point startPoint = points[i];
                Point endPoint = points[(i + 1) % points.Count];
                Vector segment = endPoint - startPoint;
                double segmentLength = segment.Length;

                if (i == 0)
                {
                    AddPixelValueToList(startPoint, bitmap, profileData);
                }

                while (accumulatedLength + stepDistance < segmentLength)
                {
                    accumulatedLength += stepDistance;
                    double t = accumulatedLength / segmentLength;
                    Point samplePoint = startPoint + t * segment;

                    AddPixelValueToList(samplePoint, bitmap, profileData);
                }

                accumulatedLength -= segmentLength;
            }

            // Add last point if polygon is not complete
            if (!polygon.IsComple && points.Count > 0)
            {
                AddPixelValueToList(points.Last(), bitmap, profileData);
            }

            return profileData;
        }

        /// <summary>
        /// Helper method: Get pixel value at specified point and add to data list based on bitmap format.
        /// </summary>
        private void AddPixelValueToList(Point point, WriteableBitmap bitmap, List<double> dataList)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);

            // Check if coordinates are within image bounds
            if (x < 0 || x >= bitmap.PixelWidth || y < 0 || y >= bitmap.PixelHeight)
            {
                return;
            }

            // Lock WriteableBitmap to safely access pixel data
            bitmap.Lock();
            try
            {
                unsafe
                {
                    // Calculate starting address of the pixel
                    int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
                    IntPtr pPixel = bitmap.BackBuffer + y * bitmap.BackBufferStride + x * bytesPerPixel;

                    double value = 0;
                    var format = bitmap.Format;

                    // Process based on different pixel formats
                    // 8-bit single channel (grayscale or indexed)
                    if (format == PixelFormats.Gray8 || format == PixelFormats.Indexed8)
                    {
                        value = *((byte*)pPixel);
                    }
                    // 16-bit single channel (grayscale)
                    else if (format == PixelFormats.Gray16)
                    {
                        value = *((ushort*)pPixel);
                    }
                    // 32-bit float single channel (grayscale)
                    else if (format == PixelFormats.Gray32Float)
                    {
                        value = *((float*)pPixel);
                    }
                    // 24-bit color (BGR or RGB)
                    else if (format == PixelFormats.Bgr24)
                    {
                        byte* p = (byte*)pPixel;
                        // Memory order: B, G, R
                        value = 0.299 * p[2] + 0.587 * p[1] + 0.114 * p[0];
                    }
                    else if (format == PixelFormats.Rgb24)
                    {
                        byte* p = (byte*)pPixel;
                        // Memory order: R, G, B
                        value = 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2];
                    }
                    // 32-bit color (BGRA)
                    else if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32)
                    {
                        byte* p = (byte*)pPixel;
                        // Memory order: B, G, R, A
                        value = 0.299 * p[2] + 0.587 * p[1] + 0.114 * p[0];
                    }
                    // 48-bit color (RGB)
                    else if (format == PixelFormats.Rgb48)
                    {
                        ushort* p = (ushort*)pPixel;
                        // Memory order: R, G, B
                        // Normalize 16-bit values to 0-1 range before calculating luminance to avoid overflow
                        const double maxVal = ushort.MaxValue;
                        double r = p[0] / maxVal;
                        double g = p[1] / maxVal;
                        double b = p[2] / maxVal;
                        // Result can be multiplied by a coefficient (like 255 or 65535) to scale, or use 0-1 range directly
                        value = (0.299 * r + 0.587 * g + 0.114 * b) * maxVal;
                    }
                    else
                    {
                        // If encountering an unhandled format, skip or log
                        // Here we choose to skip and not add any data
                        return;
                    }

                    dataList.Add(value);
                }
            }
            finally
            {
                bitmap.Unlock();
            }
        }

    }
}
