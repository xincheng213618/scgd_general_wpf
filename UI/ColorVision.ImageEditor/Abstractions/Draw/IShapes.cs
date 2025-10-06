using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Draw
{
    /// <summary>
    /// 圆形接口 - 定义圆形的基本属性
    /// </summary>
    public interface ICircle
    {
        Point Center { get; set; }
        double Radius { get; set; }
    }

    /// <summary>
    /// 矩形接口 - 定义矩形的基本属性
    /// </summary>
    public interface IRectangle
    {
        Rect Rect { get; set; }
    }

    /// <summary>
    /// 贝塞尔曲线接口 - 定义曲线的控制点
    /// </summary>
    public interface IBezierCurve
    {
        List<Point> Points { get; set; }
    }

    /// <summary>
    /// 线条接口 - 定义线条的基本属性
    /// </summary>
    public interface ILine
    {
        Point StartPoint { get; set; }
        Point EndPoint { get; set; }
    }
}
