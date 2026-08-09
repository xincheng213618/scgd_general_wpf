using System.Collections.Generic;

namespace Conoscope.Core
{
    /// <summary>
    /// 参考曲线公共数据：方位角线和极角圆共用数值与采样点存储。
    /// </summary>
    public abstract class ReferenceCurve
    {
        public double ReferenceValue { get; set; }

        public List<RgbSample> Samples { get; } = new List<RgbSample>();

        public abstract bool IsClosed { get; }
    }

    /// <summary>
    /// 沿一个方位角穿过图像的采样线。
    /// </summary>
    public sealed class PolarAngleLine : ReferenceCurve
    {
        public double Angle
        {
            get => ReferenceValue;
            set => ReferenceValue = value;
        }

        public override bool IsClosed => false;

        public override string ToString() => $"{Angle}°";
    }
}
