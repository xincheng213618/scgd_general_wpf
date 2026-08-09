namespace Conoscope.Core
{
    /// <summary>
    /// 沿一个极角半径的圆周采样线。
    /// </summary>
    public sealed class ConcentricCircleLine : ReferenceCurve
    {
        public double RadiusAngle
        {
            get => ReferenceValue;
            set => ReferenceValue = value;
        }

        public override bool IsClosed => true;

        public override string ToString() => $"R={RadiusAngle}°";
    }
}
