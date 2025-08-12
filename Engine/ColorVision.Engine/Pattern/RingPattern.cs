using OpenCvSharp;
using System;

namespace ColorVision.Engine.Pattern
{
    public static class RingPattern
    {
        /// <summary>
        /// 生成多重圆环和中心线的图像
        /// </summary>
        /// <param name="width">图像宽</param>
        /// <param name="height">图像高</param>
        /// <param name="ringWidth">单个圆环宽度</param>
        /// <param name="ringSpacing">各圆环间隔数组（每个元素为相对中心的偏移）</param>
        /// <param name="numRings">圆环数量</param>
        /// <param name="bgColor">背景色</param>
        /// <param name="ringColor">圆环颜色</param>
        /// <param name="drawCenterLine">是否绘制中心线</param>
        /// <returns></returns>
        public static Mat Generate( int width = 1600,  int height = 1600,  int ringWidth = 30,  int[] ringOffsets = null,Scalar? bgColor = null,  Scalar? ringColor = null, bool drawCenterLine = true)
        {
            bgColor ??= Scalar.Black;
            ringColor ??= Scalar.White;
            var img = new Mat(height, width, MatType.CV_8UC1, bgColor.Value);

            double centerX = width / 2.0;
            double centerY = height / 2.0;
            double maxR = Math.Min(centerX, centerY) - ringWidth / 2.0;

            if (ringOffsets == null)
                ringOffsets = new int[] { 0, 150, 300, 600 };

            foreach (var offset in ringOffsets)
            {
                double outerRadius = maxR - offset;
                double innerRadius = outerRadius - ringWidth;
                Cv2.Circle(img, new Point(centerX, centerY), (int)Math.Round((outerRadius + innerRadius) / 2), ringColor.Value, ringWidth, LineTypes.AntiAlias);
            }

            // 中心线
            if (drawCenterLine)
            {
                // 水平
                Cv2.Line(img, new Point(0, height / 2), new Point(width - 1, height / 2), ringColor.Value, 2);
                // 垂直
                Cv2.Line(img, new Point(width / 2, 0), new Point(width / 2, height - 1), ringColor.Value, 2);
            }

            return img;
        }

        /// <summary>
        /// 拼接双图（左、右各一个圆环图）
        /// </summary>
        public static Mat GenerateDouble(
            int width = 1600, int height = 1600,
            int ringWidth = 30, int[] ringOffsets = null,
            Scalar? bgColor = null, Scalar? ringColor = null, bool drawCenterLine = true)
        {
            var left = Generate(width, height, ringWidth, ringOffsets, bgColor, ringColor, drawCenterLine);
            var right = Generate(width, height, ringWidth, ringOffsets, bgColor, ringColor, drawCenterLine);
            Mat result = new Mat(height, width * 2, MatType.CV_8UC1, bgColor ?? Scalar.Black);
            left.CopyTo(result[new Rect(0, 0, width, height)]);
            right.CopyTo(result[new Rect(width, 0, width, height)]);
            return result;
        }
    }
}
