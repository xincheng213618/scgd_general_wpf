using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 绘图可视化接口 - 定义可绘制元素的基本行为
    /// </summary>
    public interface IDrawingVisual
    {
        BaseProperties BaseAttribute { get; }
        Pen Pen { get; set; }
        void Render();
    }
}
