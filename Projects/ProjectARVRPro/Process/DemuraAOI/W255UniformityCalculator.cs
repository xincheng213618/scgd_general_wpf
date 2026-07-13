using OpenCvSharp;

namespace ProjectARVRPro.Process.DemuraAOI
{
    public static class W255UniformityCalculator
    {
        public static W255UniformityResult Calculate(string filePath, int radius)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Failure(filePath, radius, "W255图像路径为空。");

            try
            {
                using Mat source = Cv2.ImRead(filePath, ImreadModes.Unchanged);
                W255UniformityResult result = Calculate(source, radius);
                result.FilePath = filePath;
                return result;
            }
            catch (Exception ex)
            {
                return Failure(filePath, radius, $"读取W255图像异常: {ex.Message}");
            }
        }

        public static W255UniformityResult Calculate(Mat source, int radius)
        {
            if (source == null || source.Empty())
                return Failure(string.Empty, radius, "W255图像为空或无法读取。");
            if (source.Channels() != 1)
                return Failure(string.Empty, radius, $"W255图像必须是单通道，当前通道数为{source.Channels()}。");
            if (radius <= 0)
                return Failure(string.Empty, radius, "W255九点ROI半径必须大于0。");

            int width = source.Width;
            int height = source.Height;
            var centers = CreateCenters(width, height);
            if (centers.Any(center => center.X - radius < 0 || center.Y - radius < 0 || center.X + radius >= width || center.Y + radius >= height))
                return Failure(string.Empty, radius, $"W255图像尺寸{width}x{height}不足以容纳半径{radius}的九点ROI。");

            try
            {
                using Mat image = new Mat();
                source.ConvertTo(image, MatType.CV_64FC1);
                var means = new List<double>(centers.Count);
                int roiSize = radius * 2 + 1;

                foreach (Point center in centers)
                {
                    var rect = new Rect(center.X - radius, center.Y - radius, roiSize, roiSize);
                    using Mat roi = new Mat(image, rect);
                    using Mat mask = Mat.Zeros(roiSize, roiSize, MatType.CV_8UC1);
                    Cv2.Circle(mask, new Point(radius, radius), radius, Scalar.White, -1);
                    double mean = Cv2.Mean(roi, mask).Val0;
                    if (!double.IsFinite(mean) || mean < 0)
                        return Failure(string.Empty, radius, "W255九点ROI包含无效或负亮度值。");
                    means.Add(mean);
                }

                double minimum = means.Min();
                double maximum = means.Max();
                if (maximum <= 0)
                    return Failure(string.Empty, radius, "W255九点ROI最大均值为0，无法计算均匀性。");

                return new W255UniformityResult
                {
                    Success = true,
                    Width = width,
                    Height = height,
                    Radius = radius,
                    PointMeans = means,
                    Minimum = minimum,
                    Maximum = maximum,
                    Uniformity = minimum / maximum
                };
            }
            catch (Exception ex)
            {
                return Failure(string.Empty, radius, $"计算W255均匀性异常: {ex.Message}");
            }
        }

        private static List<Point> CreateCenters(int width, int height)
        {
            int[] xs = new[] { width / 4, width / 2, width * 3 / 4 };
            int[] ys = new[] { height / 4, height / 2, height * 3 / 4 };
            return ys.SelectMany(y => xs.Select(x => new Point(x, y))).ToList();
        }

        private static W255UniformityResult Failure(string filePath, int radius, string message)
        {
            return new W255UniformityResult
            {
                FilePath = filePath ?? string.Empty,
                Radius = radius,
                ErrorMessage = message
            };
        }
    }
}
