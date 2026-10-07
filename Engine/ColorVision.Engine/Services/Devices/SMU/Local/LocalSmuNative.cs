using cvColorVision;
using System.Text;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal interface ILocalSmuNative
{
    int Open(LocalSmuConnection connection);
    int GetIdn(int handle, StringBuilder text, ref int length);
    int SetWiring(int handle, bool fourWire, bool front);
    int SetDelay(int handle, double milliseconds);
    int SetChannel(int handle, bool channelA);
    int SetSource(int handle, bool voltage);
    int Measure(int handle, LocalSmuParameters parameters, bool step, ref double voltage, ref double current);
    int Read(int handle, ref double voltage, ref double current);
    int Sweep(int handle, LocalSmuParameters parameters, double[] voltages, double[] currents);
    int CloseOutput(int handle);
    int Close(int handle);
}

internal sealed class LocalSmuNative : ILocalSmuNative
{
    public int Open(LocalSmuConnection c) => PassSx.OpenNetDevice(c.IsNet, c.DeviceName, c.DeviceType);
    public int GetIdn(int h, StringBuilder text, ref int length) => PassSx.GetIDN(h, text, ref length);
    public int SetWiring(int h, bool fourWire, bool front) => PassSx.Set4WireFront(h, fourWire, front);
    public int SetDelay(int h, double milliseconds) => PassSx.SetDelayTime(h, milliseconds);
    public int SetChannel(int h, bool channelA) => PassSx.SetSrcAorB(h, channelA);
    public int SetSource(int h, bool voltage) => PassSx.SetSourceV(h, voltage);
    public int Measure(int h, LocalSmuParameters p, bool step, ref double v, ref double i) => !p.IsAutoRng
        ? PassSx.StepMeasureDataEx(h, p.SrcRng, p.LmtRng, p.NativeSource(p.MeasureValue), p.NativeLimit, ref v, ref i)
        : step ? PassSx.StepMeasureData(h, p.NativeSource(p.MeasureValue), p.NativeLimit, ref v, ref i)
        : PassSx.MeasureData(h, p.NativeSource(p.MeasureValue), p.NativeLimit, ref v, ref i);
    public int Read(int h, ref double v, ref double i) => PassSx.GetMeasureResult(h, ref v, ref i);
    public int Sweep(int h, LocalSmuParameters p, double[] v, double[] i) => p.IsAutoRng
        ? PassSx.SweepDataAutoRng(h, p.NativeLimit, p.NativeSource(p.BeginValue), p.NativeSource(p.EndValue), p.Points, v, i)
        : PassSx.SweepData(h, p.SrcRng, p.LmtRng, p.NativeLimit, p.NativeSource(p.BeginValue), p.NativeSource(p.EndValue), p.Points, v, i);
    public int CloseOutput(int h) => PassSx.CloseOutput(h);
    public int Close(int h) => PassSx.CloseDevice(h);
}
