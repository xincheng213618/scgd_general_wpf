using cvColorVision;
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal interface ILocalSmuNative
{
    int GetLocalFamily(int handle) => 0;
    int CloseAdvancedOutput(int handle, int channel, int mode) => throw new NotSupportedException("当前 DLL 不支持本地输出关闭状态。");
    void ExecuteAdvanced(int handle, LocalSmuSettings settings, double[] sources, double[] limits, double[] v, double[] i, double[] r, int[] status)
        => throw new NotSupportedException("当前源表 DLL 未提供本地高级测量，请更新 cvCamera.dll。");
    int Open(LocalSmuConnection connection);
    int GetIdn(int handle, StringBuilder text, ref int length);
    int SetWiring(int handle, bool fourWire, bool front);
    int SetDelay(int handle, double milliseconds);
    int SetChannel(int handle, bool channelA);
    int SetSource(int handle, bool voltage);
    int Measure(int handle, LocalSmuParameters parameters, bool step, ref double voltage, ref double current);
    int Read(int handle, ref double voltage, ref double current);
    int Sweep(int handle, LocalSmuParameters parameters, double[] voltages, double[] currents);
    int GetSweepCapabilities(int handle) => 0;
    int List(int handle, LocalSmuParameters parameters, double[] sourceValues, double[] voltages, double[] currents) => throw new NotSupportedException("当前源表驱动不支持列表扫描。");
    int CloseOutput(int handle);
    int Close(int handle);
}

internal sealed class LocalSmuNative : ILocalSmuNative
{
    [DllImport("cvCamera.dll", EntryPoint = "cvPss_Sx_GetLocalFamily", CallingConvention = CallingConvention.StdCall)]
    private static extern int NativeFamily(int handle);
    [DllImport("cvCamera.dll", EntryPoint = "cvPss_Sx_CloseLocalOutput", CallingConvention = CallingConvention.StdCall)]
    private static extern int NativeCloseOutput(int handle, int channel, int mode);
    public int CloseAdvancedOutput(int handle, int channel, int mode)
    {
        try { return NativeCloseOutput(handle, channel, mode); }
        catch (EntryPointNotFoundException ex) { throw new InvalidOperationException("当前 DLL 不支持本地输出关闭状态，请更新 cvCamera.dll。", ex); }
    }
    [DllImport("cvCamera.dll", EntryPoint = "cvPss_Sx_ExecuteLocal", CallingConvention = CallingConvention.StdCall)]
    private static extern int NativeExecute(int handle, in SmuNativeSettings settings, [In] double[] sources, [In] double[] limits,
        [Out] double[] v, [Out] double[] i, [Out] double[] r, [Out] int[] status, [Out] byte[] error, int capacity);
    public int GetLocalFamily(int handle)
    {
        try { return NativeFamily(handle); }
        catch (EntryPointNotFoundException) { return 0; }
    }
    public void ExecuteAdvanced(int handle, LocalSmuSettings settings, double[] sources, double[] limits, double[] v, double[] i, double[] r, int[] status)
    {
        var native = SmuNativeSettings.From(settings, sources.Length);
        byte[] error = new byte[2048];
        try
        {
            if (NativeExecute(handle, in native, sources.Select(value => settings.IsSourceV ? value : value / 1000).ToArray(),
                limits.Select(value => settings.IsSourceV ? value / 1000 : value).ToArray(), v, i, r, status, error, error.Length) != 1)
            {
                int terminator = Array.IndexOf(error, (byte)0);
                throw new InvalidOperationException($"源表操作失败：{Encoding.ASCII.GetString(error, 0, terminator < 0 ? error.Length : terminator)}");
            }
        }
        catch (EntryPointNotFoundException ex) { throw new InvalidOperationException("当前源表 DLL 未提供本地高级测量，请更新 cvCamera.dll 后重启。", ex); }
    }
    public int Open(LocalSmuConnection c)
    {
        if (c.IsNet || c.BaudRate == 9600) return PassSx.OpenNetDevice(c.IsNet, c.DeviceName, c.DeviceType);
        try { return PassSx.OpenNetDeviceEx(false, c.DeviceName, c.DeviceType, c.BaudRate); }
        catch (EntryPointNotFoundException ex)
        {
            throw new InvalidOperationException("当前源表 DLL 不支持设置串口波特率，请更新 cvCamera.dll 后重新启动程序。", ex);
        }
    }
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
    public int GetSweepCapabilities(int h)
    {
        try { return PassSx.GetSweepCapabilities(h); }
        catch (EntryPointNotFoundException) { return 0; }
    }
    public int List(int h, LocalSmuParameters p, double[] sourceValues, double[] v, double[] i)
    {
        double[] nativeValues = Array.ConvertAll(sourceValues, p.NativeSource);
        return PassSx.ListData(h, p.IsAutoRng ? 0 : p.SrcRng, p.IsAutoRng ? 0 : p.LmtRng, p.NativeLimit, nativeValues, nativeValues.Length, v, i);
    }
    public int CloseOutput(int h) => PassSx.CloseOutput(h);
    public int Close(int h) => PassSx.CloseDevice(h);
}
