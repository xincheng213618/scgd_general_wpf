using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Pattern
{

    public class SFRPattern
    {
        public static Mat Generate( int width = 640,int height = 400,  int squareSize = 50,double angleDeg = 10, double borderRatio = 0.1, int rows = 3, int cols = 3)
        {
            var img = new Mat(height, width, MatType.CV_8UC3, Scalar.White);

            double leftRightMargin = width * borderRatio;
            double topBottomMargin = height * borderRatio;
            double usableWidth = width - 2 * leftRightMargin;
            double usableHeight = height - 2 * topBottomMargin;
            double xSpacing = (cols == 1) ? 0 : usableWidth / (cols - 1);
            double ySpacing = (rows == 1) ? 0 : usableHeight / (rows - 1);

            var centers = new List<Point2f>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float cx = (float)(leftRightMargin + col * xSpacing);
                    float cy = (float)(topBottomMargin + row * ySpacing);
                    centers.Add(new Point2f(cx, cy));
                }
            }

            double theta = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);
            float half = squareSize / 2f;

            // 正方形顶点
            Point2f[] baseVertices = new[]
            {
            new Point2f(-half, -half),
            new Point2f( half, -half),
            new Point2f( half,  half),
            new Point2f(-half,  half)
        };

            foreach (var center in centers)
            {
                // 旋转
                Point[] pts = new Point[4];
                for (int i = 0; i < 4; i++)
                {
                    float x = baseVertices[i].X;
                    float y = baseVertices[i].Y;
                    float rx = (float)(x * cos - y * sin) + center.X;
                    float ry = (float)(x * sin + y * cos) + center.Y;
                    pts[i] = new Point((int)Math.Round(rx), (int)Math.Round(ry));
                }
                Cv2.FillPoly(img, new[] { pts }, Scalar.Black);
            }

            return img;

        }
    }
}
