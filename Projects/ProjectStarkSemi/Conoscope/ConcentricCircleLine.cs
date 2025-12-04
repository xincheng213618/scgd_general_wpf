using ColorVision.ImageEditor.Draw;
using System.Collections.Generic;

namespace ProjectStarkSemi.Conoscope
{
    /// <summary>
    /// 同心圆数据类，存储半径角度、圆对象和采样数据
    /// </summary>
    public class ConcentricCircleLine
    {
        /// <summary>
        /// 半径角度（度）- 对应视角角度
        /// </summary>
        public double RadiusAngle { get; set; }

        /// <summary>
        /// 绘制的圆对象
        /// </summary>
        public DVCircle? Circle { get; set; }

        /// <summary>
        /// 沿圆周采样的数据，按照0-360度方向
        /// </summary>
        public List<RgbSample> RgbData { get; set; } = new List<RgbSample>();

        /// <summary>
        /// 是否显示此圆的数据
        /// </summary>
        public bool IsVisible { get; set; } = true;

        public override string ToString() => $"R={RadiusAngle}°";
    }
}
