using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Line
{
    /// <summary>
    /// Helper class for extracting profile data from images along paths.
    /// </summary>
    public static class ProfileDataExtractor
    {
        /// <summary>
        /// 检查图像格式是否为多通道彩色图像
        /// </summary>
        public static bool IsMultiChannelFormat(PixelFormat format)
        {
            return format == PixelFormats.Bgr24 ||
                   format == PixelFormats.Rgb24 ||
                   format == PixelFormats.Bgr32 ||
                   format == PixelFormats.Bgra32 ||
                   format == PixelFormats.Pbgra32 ||
                   format == PixelFormats.Rgb48;
        }

        /// <summary>
        /// Extract profile data along a path defined by points.
        /// </summary>
        public static ProfileData ExtractAlongPath(IList<Point> points, WriteableBitmap bitmap, int totalSteps = 500, bool closePath = false)
        {
            bool isMultiChannel = IsMultiChannelFormat(bitmap.Format);

            // Calculate total length
            double totalLength = 0;
            int segmentCount = closePath ? points.Count : points.Count - 1;
            
            for (int i = 0; i < segmentCount; i++)
            {
                Point p1 = points[i];
                Point p2 = points[(i + 1) % points.Count];
                totalLength += (p2 - p1).Length;
            }

            if (totalLength <= 0)
                return ProfileData.CreateSingleChannel(new List<double>());

            if (isMultiChannel)
            {
                return ExtractMultiChannelData(points, bitmap, totalSteps, totalLength, closePath);
            }
            else
            {
                return ExtractSingleChannelData(points, bitmap, totalSteps, totalLength, closePath);
            }
        }

        private static ProfileData ExtractMultiChannelData(IList<Point> points, WriteableBitmap bitmap, 
            int totalSteps, double totalLength, bool closePath)
        {
            var redData = new List<double>();
            var greenData = new List<double>();
            var blueData = new List<double>();
            var grayData = new List<double>();

            double stepDistance = totalLength / (totalSteps - 1);
            double accumulatedLength = 0;
            int segmentCount = closePath ? points.Count : points.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                Point startPoint = points[i];
                Point endPoint = points[(i + 1) % points.Count];
                Vector segment = endPoint - startPoint;
                double segmentLength = segment.Length;

                if (i == 0)
                {
                    AddMultiChannelPixelValue(startPoint, bitmap, redData, greenData, blueData, grayData);
                }

                while (accumulatedLength + stepDistance < segmentLength)
                {
                    accumulatedLength += stepDistance;
                    double t = accumulatedLength / segmentLength;
                    Point samplePoint = startPoint + t * segment;

                    AddMultiChannelPixelValue(samplePoint, bitmap, redData, greenData, blueData, grayData);
                }

                accumulatedLength -= segmentLength;
            }

            if (!closePath)
            {
                AddMultiChannelPixelValue(points.Last(), bitmap, redData, greenData, blueData, grayData);
            }

            return ProfileData.CreateMultiChannel(redData, greenData, blueData, grayData);
        }

        private static ProfileData ExtractSingleChannelData(IList<Point> points, WriteableBitmap bitmap, 
            int totalSteps, double totalLength, bool closePath)
        {
            var grayData = new List<double>();

            double stepDistance = totalLength / (totalSteps - 1);
            double accumulatedLength = 0;
            int segmentCount = closePath ? points.Count : points.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                Point startPoint = points[i];
                Point endPoint = points[(i + 1) % points.Count];
                Vector segment = endPoint - startPoint;
                double segmentLength = segment.Length;

                if (i == 0)
                {
                    AddPixelValueToList(startPoint, bitmap, grayData);
                }

                while (accumulatedLength + stepDistance < segmentLength)
                {
                    accumulatedLength += stepDistance;
                    double t = accumulatedLength / segmentLength;
                    Point samplePoint = startPoint + t * segment;

                    AddPixelValueToList(samplePoint, bitmap, grayData);
                }

                accumulatedLength -= segmentLength;
            }

            if (!closePath)
            {
                AddPixelValueToList(points.Last(), bitmap, grayData);
            }

            return ProfileData.CreateSingleChannel(grayData);
        }

        /// <summary>
        /// 辅助方法：根据位图格式，获取指定点的像素值并添加到数据列表。
        /// </summary>
        private static void AddPixelValueToList(Point point, WriteableBitmap bitmap, List<double> dataList)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);

            if (x < 0 || x >= bitmap.PixelWidth || y < 0 || y >= bitmap.PixelHeight)
            {
                return;
            }

            bitmap.Lock();
            try
            {
                unsafe
                {
                    int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
                    IntPtr pPixel = bitmap.BackBuffer + y * bitmap.BackBufferStride + x * bytesPerPixel;

                    double value = 0;
                    var format = bitmap.Format;

                    if (format == PixelFormats.Gray8 || format == PixelFormats.Indexed8)
                    {
                        value = *((byte*)pPixel);
                    }
                    else if (format == PixelFormats.Gray16)
                    {
                        value = *((ushort*)pPixel);
                    }
                    else if (format == PixelFormats.Gray32Float)
                    {
                        value = *((float*)pPixel);
                    }
                    else if (format == PixelFormats.Bgr24)
                    {
                        byte* p = (byte*)pPixel;
                        value = 0.299 * p[2] + 0.587 * p[1] + 0.114 * p[0];
                    }
                    else if (format == PixelFormats.Rgb24)
                    {
                        byte* p = (byte*)pPixel;
                        value = 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2];
                    }
                    else if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32)
                    {
                        byte* p = (byte*)pPixel;
                        value = 0.299 * p[2] + 0.587 * p[1] + 0.114 * p[0];
                    }
                    else if (format == PixelFormats.Rgb48)
                    {
                        ushort* p = (ushort*)pPixel;
                        const double maxVal = ushort.MaxValue;
                        double r = p[0] / maxVal;
                        double g = p[1] / maxVal;
                        double b = p[2] / maxVal;
                        value = (0.299 * r + 0.587 * g + 0.114 * b) * maxVal;
                    }
                    else
                    {
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

        /// <summary>
        /// 辅助方法：根据位图格式，获取指定点的多通道像素值。
        /// </summary>
        private static void AddMultiChannelPixelValue(Point point, WriteableBitmap bitmap,
            List<double> redList, List<double> greenList, List<double> blueList, List<double> grayList)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);

            if (x < 0 || x >= bitmap.PixelWidth || y < 0 || y >= bitmap.PixelHeight)
            {
                return;
            }

            bitmap.Lock();
            try
            {
                unsafe
                {
                    int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
                    IntPtr pPixel = bitmap.BackBuffer + y * bitmap.BackBufferStride + x * bytesPerPixel;
                    var format = bitmap.Format;

                    double r = 0, g = 0, b = 0, gray = 0;

                    if (format == PixelFormats.Bgr24)
                    {
                        byte* p = (byte*)pPixel;
                        b = p[0];
                        g = p[1];
                        r = p[2];
                        gray = 0.299 * r + 0.587 * g + 0.114 * b;
                    }
                    else if (format == PixelFormats.Rgb24)
                    {
                        byte* p = (byte*)pPixel;
                        r = p[0];
                        g = p[1];
                        b = p[2];
                        gray = 0.299 * r + 0.587 * g + 0.114 * b;
                    }
                    else if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32)
                    {
                        byte* p = (byte*)pPixel;
                        b = p[0];
                        g = p[1];
                        r = p[2];
                        gray = 0.299 * r + 0.587 * g + 0.114 * b;
                    }
                    else if (format == PixelFormats.Rgb48)
                    {
                        ushort* p = (ushort*)pPixel;
                        r = p[0];
                        g = p[1];
                        b = p[2];
                        gray = 0.299 * r + 0.587 * g + 0.114 * b;
                    }
                    else
                    {
                        return;
                    }

                    redList.Add(r);
                    greenList.Add(g);
                    blueList.Add(b);
                    grayList.Add(gray);
                }
            }
            finally
            {
                bitmap.Unlock();
            }
        }
    }
}
