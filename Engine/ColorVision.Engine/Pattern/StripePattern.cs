using OpenCvSharp;
using System;

namespace ColorVision.Engine.Pattern
{
    public static class StripePattern
    {
        /// <summary>
        /// 生成横竖线对图（线宽和间隔均可调）
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <param name="stripeWidth">线宽(px)</param>
        /// <param name="direction">0: 横线对, 1: 竖线对, 2: 横竖组合</param>
        /// <returns></returns>
        public static Mat Generate( int width = 1920,int height = 1080, int stripeWidth = 1,  int direction = 2 )
        {
            var img = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);

            // 横线
            if (direction == 0 || direction == 2)
            {
                for (int y = 0; y < height; y += stripeWidth * 2)
                    img.RowRange(y, Math.Min(y + stripeWidth, height)).SetTo(Scalar.White);
            }
            // 竖线
            if (direction == 1 || direction == 2)
            {
                for (int x = 0; x < width; x += stripeWidth * 2)
                    img.ColRange(x, Math.Min(x + stripeWidth, width)).SetTo(Scalar.White);
            }
            return img;
        }
    }
}
