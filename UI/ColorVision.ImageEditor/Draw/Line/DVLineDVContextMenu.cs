using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

                    // 2. 检查图像源是否为 WriteableBitmap
                    if (context.ImageView.ImageShow.Source is WriteableBitmap writeableBitmap)
                    {
                        // 3. 提取截面数据
                        List<double> profileData = ExtractProfileData(dVLine, writeableBitmap);

                        if (profileData.Count == 0)
                        {
                            MessageBox.Show("无法从图像中提取有效的截面数据。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // 4. 创建并显示图表窗口
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
        /// 从 WriteableBitmap 中沿着 DVLine 定义的路径提取截面数据。
        /// </summary>
        /// <param name="line">包含点路径的 DVLine 对象。</param>
        /// <param name="bitmap">要从中提取数据的 WriteableBitmap。</param>
        /// <param name="totalSteps">总采样点数，默认为500。</param>
        /// <returns>代表路径上像素值的列表。</returns>
        private List<double> ExtractProfileData(DVLine line, WriteableBitmap bitmap, int totalSteps = 500)
        {
            var profileData = new List<double>();
            var points = line.Points;

            // --- 1. 计算折线的总长度 ---
            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += (points[i + 1] - points[i]).Length;
            }

            if (totalLength <= 0) return profileData;

            // --- 2. 沿路径进行等距采样 ---
            // 这部分逻辑保持不变，它负责在几何上找到采样点
            double stepDistance = totalLength / (totalSteps - 1);
            double accumulatedLength = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Point startPoint = points[i];
                Point endPoint = points[i + 1];
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

            AddPixelValueToList(points.Last(), bitmap, profileData);

            return profileData;
        }
        /// <summary>
        /// 辅助方法：根据位图格式，获取指定点的像素值并添加到数据列表。
        /// </summary>
        private void AddPixelValueToList(Point point, WriteableBitmap bitmap, List<double> dataList)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);

            // 检查坐标是否在图像范围内
            if (x < 0 || x >= bitmap.PixelWidth || y < 0 || y >= bitmap.PixelHeight)
            {
                return;
            }

            // 锁定 WriteableBitmap 以安全地访问像素数据
            bitmap.Lock();
            try
            {
                unsafe
                {
                    // 计算像素的起始地址
                    int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
                    IntPtr pPixel = bitmap.BackBuffer + y * bitmap.BackBufferStride + x * bytesPerPixel;

                    double value = 0;
                    var format = bitmap.Format;

                    // --- 根据不同的像素格式进行处理 ---

                    // 8位单通道 (灰度图或索引图)
                    if (format == PixelFormats.Gray8 || format == PixelFormats.Indexed8)
                    {
                        value = *((byte*)pPixel);
                    }
                    // 16位单通道 (灰度图)
                    else if (format == PixelFormats.Gray16)
                    {
                        value = *((ushort*)pPixel);
                    }
                    // 32位浮点单通道 (灰度图)
                    else if (format == PixelFormats.Gray32Float)
                    {
                        value = *((float*)pPixel);
                    }
                    // 24位彩色 (BGR 或 RGB)
                    else if (format == PixelFormats.Bgr24)
                    {
                        byte* p = (byte*)pPixel;
                        // 内存顺序: B, G, R
                        value = 0.299 * p[2] + 0.587 * p[1] + 0.114 * p[0];
                    }
                    else if (format == PixelFormats.Rgb24)
                    {
                        byte* p = (byte*)pPixel;
                        // 内存顺序: R, G, B
                        value = 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2];
                    }
                    // 32位彩色 (BGRA)
                    else if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32)
                    {
                        byte* p = (byte*)pPixel;
                        // 内存顺序: B, G, R, A
                        value = 0.299 * p[2] + 0.587 * p[1] + 0.114 * p[0];
                    }
                    // 48位彩色 (RGB)
                    else if (format == PixelFormats.Rgb48)
                    {
                        ushort* p = (ushort*)pPixel;
                        // 内存顺序: R, G, B
                        // 将16位的值归一化到0-1范围再计算亮度，以避免数值溢出
                        const double maxVal = ushort.MaxValue;
                        double r = p[0] / maxVal;
                        double g = p[1] / maxVal;
                        double b = p[2] / maxVal;
                        // 结果可以再乘以一个系数（如255或65535）来放大，或直接使用0-1范围的值
                        value = (0.299 * r + 0.587 * g + 0.114 * b) * maxVal;
                    }
                    else
                    {
                        // 如果遇到未处理的格式，可以选择跳过或记录日志
                        // 这里我们选择跳过，不添加任何数据
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
