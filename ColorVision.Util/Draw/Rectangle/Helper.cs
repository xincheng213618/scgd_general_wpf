using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Util.Draw.Rectangle
{
    public static class Helpers
    {
        public static List<Point> SortPolyPoints(List<Point> vPoints)
        {
            if (vPoints == null || vPoints.Count == 0) return new List<Point>();
            //计算重心
            double X = 0, Y = 0;
            for (int i = 0; i < vPoints.Count; i++)
            {
                X += vPoints[i].X;
                Y += vPoints[i].Y;
            }
            Point center = new Point((int)X / vPoints.Count, (int)Y / vPoints.Count);
            //冒泡排序
            for (int i = 0; i < vPoints.Count - 1; i++)
            {
                for (int j = 0; j < vPoints.Count - i - 1; j++)
                {
                    if (PointCmp(vPoints[j], vPoints[j + 1], center))
                    {
                        (vPoints[j + 1], vPoints[j]) = (vPoints[j], vPoints[j + 1]);
                    }
                }
            }
            return vPoints;
        }

        private static bool PointCmp(Point a, Point b, Point center)
        {
            if (a.X >= 0 && b.X < 0)
                return true;
            else if (a.X == 0 && b.X == 0)
                return a.Y > b.Y;
            //向量OA和向量OB的叉积
            double det = (a.X - center.X) * (b.Y - center.Y) - (b.X - center.X) * (a.Y - center.Y);
            if (det < 0)
                return true;
            if (det > 0)
                return false;
            //向量OA和向量OB共线，以距离判断大小
            double d1 = (a.X - center.X) * (a.X - center.X) + (a.Y - center.Y) * (a.Y - center.Y);
            double d2 = (b.X - center.X) * (b.X - center.X) + (b.Y - center.Y) * (b.Y - center.Y);
            return d1 > d2;
        }
    }
}
