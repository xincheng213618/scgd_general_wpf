using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Common.Algorithms
{
    //计算凸包Graham扫描算法
    public class GrahamScan
    {
        public static List<Point> ComputeConvexHull(List<Point> points)
        {
            if (points.Count < 3)
                throw new ArgumentException("至少需要三个点来构建凸包");

            // 找到基准点
            Point p0 = points.OrderBy(p => p.Y).ThenBy(p => p.X).First();
            points.Remove(p0);

            // 按极角排序
            points.Sort((p1, p2) =>
            {
                double angle1 = Math.Atan2(p1.Y - p0.Y, p1.X - p0.X);
                double angle2 = Math.Atan2(p2.Y - p0.Y, p2.X - p0.X);
                return angle1.CompareTo(angle2);
            });

            // 初始化栈
            Stack<Point> stack = new();
            stack.Push(p0);
            stack.Push(points[0]);
            stack.Push(points[1]);

            // 处理所有的点
            for (int i = 2; i < points.Count; i++)
            {
                Point top = stack.Pop();
                while (CrossProduct(stack.Peek(), top, points[i]) <= 0)
                {
                    top = stack.Pop();
                }
                stack.Push(top);
                stack.Push(points[i]);
            }

            return stack.ToList();
        }

        private static double CrossProduct(Point p1, Point p2, Point p3)
        {
            return (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
        }
    }
}
