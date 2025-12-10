using System.Collections.Generic;

namespace ConoscopeDemo
{
    /// <summary>
    /// 同心圆线数据类，存储半径角度和RGB数据
    /// </summary>
    public class ConcentricCircleLine
    {
        /// <summary>
        /// 半径对应的角度（度）
        /// </summary>
        public double RadiusAngle { get; set; }

        /// <summary>
        /// 沿圆周采样的RGB数据
        /// </summary>
        public List<RgbSample> RgbData { get; set; } = new List<RgbSample>();

        public override string ToString() => $"R={RadiusAngle}°";
    }
}
