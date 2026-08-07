namespace Conoscope.Core
{
    /// <summary>
    /// 参考曲线上的单个 XYZ 采样点。使用值类型，避免每个像素分配一个对象。
    /// </summary>
    public readonly record struct RgbSample(
        double Position,
        int DX,
        int DY,
        double X,
        double Y,
        double Z);
}
