using OpenCvSharp;

namespace ColorVision.Engine.Pattern
{


    public static class NineDotPattern
    {
        /// <summary>
        /// 生成9点阵图（3x3圆点）
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <param name="dotDiameter">圆点直径</param>
        /// <param name="backgroundColor">背景色</param>
        /// <param name="dotColor">圆点颜色</param>
        /// <returns>Mat</returns>
        public static Mat Generate(  int width = 1920,  int height = 1080, int dotDiameter = 50, Scalar? backgroundColor = null, Scalar? dotColor = null)
        {
            backgroundColor ??= Scalar.Black;
            dotColor ??= Scalar.White;
            var img = new Mat(height, width, MatType.CV_8UC3, backgroundColor.Value);

            // 3x3点阵，间隔算法
            int rows = 3, cols = 3;
            double gapX = (width - cols * dotDiameter) / (cols + 1.0);
            double gapY = (height - rows * dotDiameter) / (rows + 1.0);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int cx = (int)(gapX + dotDiameter / 2 + j * (dotDiameter + gapX));
                    int cy = (int)(gapY + dotDiameter / 2 + i * (dotDiameter + gapY));
                    Cv2.Circle(img, new Point(cx, cy), dotDiameter / 2, dotColor.Value, -1, LineTypes.AntiAlias);
                }
            }
            return img;
        }
    }
}
