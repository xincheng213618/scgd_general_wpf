using ColorVision.ImageEditor.Draw;
using System.Collections.Generic;

namespace ProjectStarkSemi.Conoscope
{
    /// <summary>
    /// 极角线数据类，存储角度、线对象和RGB数据
    /// </summary>
    public class PolarAngleLine
    {
        /// <summary>
        /// 极角（度）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 绘制的线对象
        /// </summary>
        public DVLine? Line { get; set; }

        /// <summary>
        /// 沿线采样的RGB数据
        /// </summary>
        public List<RgbSample> RgbData { get; set; } = new List<RgbSample>();

        /// <summary>
        /// 是否显示此线的数据
        /// </summary>
        public bool IsVisible { get; set; } = true;

        public override string ToString() => $"{Angle}°";
    }
}
