using System.Collections.Generic;

namespace ConoscopeDemo
{
    /// <summary>
    /// 极角线数据类，存储角度和RGB数据
    /// </summary>
    public class PolarAngleLine
    {
        /// <summary>
        /// 极角（度）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 沿线采样的RGB数据
        /// </summary>
        public List<RgbSample> RgbData { get; set; } = new List<RgbSample>();

        public override string ToString() => $"{Angle}°";
    }
}
