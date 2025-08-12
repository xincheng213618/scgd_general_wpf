namespace ColorVision.Engine.Pattern
{

    public static class DotPattern
    {
        // 点阵
        public static OpenCvSharp.Mat Generate(int w, int h, int spacing, int radius, OpenCvSharp.Scalar dotColor, OpenCvSharp.Scalar bgColor)
        {
            var mat = new OpenCvSharp.Mat(h, w, OpenCvSharp.MatType.CV_8UC3, bgColor);
            for (int y = spacing / 2; y < h; y += spacing)
                for (int x = spacing / 2; x < w; x += spacing)
                    OpenCvSharp.Cv2.Circle(mat, new OpenCvSharp.Point(x, y), radius, dotColor, -1);
            return mat;
        }
    }
}
