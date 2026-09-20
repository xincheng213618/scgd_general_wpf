using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

public sealed class LocalSpectrumParameters
{
    public float IntegralTime { get; set; } = 100;
    public int NumberOfAverage { get; set; } = 1;
    public bool AutoIntegration { get; set; }
    public bool AutoInitDark { get; set; }
    public bool SelfAdaptionInitDark { get; set; }
    public bool IsWithND { get; set; }
    public bool Eqe { get; set; }
    public double AFactor { get; set; } = 1;
    public float Voltage { get; set; }
    public float Current { get; set; }

    public void Validate()
    {
        if (!float.IsFinite(IntegralTime) || IntegralTime <= 0 || NumberOfAverage < 1)
            throw new InvalidOperationException("积分时间必须大于 0，平均次数至少为 1。");
        if (IsWithND) throw new NotSupportedException("本地光谱采集暂不支持服务控制的 ND 轮，请关闭使用 ND 后采集。");
        if (Eqe && (!double.IsFinite(AFactor) || AFactor <= 0 || !float.IsFinite(Voltage) || !float.IsFinite(Current) || Current <= 0))
            throw new InvalidOperationException("EQE 系数和电流必须大于 0，电压必须为有限值。");
    }
}

public sealed class LocalSpectrumCapture
{
    public DateTime CapturedAt { get; init; } = DateTime.Now;
    public string DeviceCode { get; set; } = string.Empty;
    public float IntegralTime { get; init; }
    public COLOR_PARA Data { get; init; }
    public COLOR_PARA_EQE? EqeData { get; init; }
}

internal interface ILocalSpectrumNative
{
    IntPtr Create(int type, Spectrometer.Emission_CallBack callback);
    int Init(IntPtr handle, int port, int baud);
    int SerialNumber(IntPtr handle, StringBuilder serial);
    int Close(IntPtr handle);
    int Release(IntPtr handle);
    int LoadWavelength(IntPtr handle, string path);
    int LoadMagnitude(IntPtr handle, string path);
    int Configure(IntPtr handle, SetEmissionSP100Config config);
    int AutoTime(IntPtr handle, ref float time, ConfigSpectrum config);
    int Dark(IntPtr handle, float time, int average, int filter, float[] data, bool adaptive);
    int InitAutoDark(IntPtr handle, SelfAdaptionInitDark config);
    LocalSpectrumCapture Capture(IntPtr handle, LocalSpectrumParameters parameters, ConfigSpectrum config, float[] dark);
}

internal sealed class LocalSpectrumNative : ILocalSpectrumNative
{
    public IntPtr Create(int type, Spectrometer.Emission_CallBack callback) => Spectrometer.CM_CreateEmission(type, callback);
    public int Init(IntPtr h, int port, int baud) => Spectrometer.CM_Emission_Init(h, port, baud);
    public int SerialNumber(IntPtr h, StringBuilder serial) => Spectrometer.CM_GetSpectrSerialNumber(h, serial);
    public int Close(IntPtr h) => Spectrometer.CM_Emission_Close(h);
    public int Release(IntPtr h) => Spectrometer.CM_ReleaseEmission(h);
    public int LoadWavelength(IntPtr h, string path) => Spectrometer.CM_Emission_LoadWavaLengthFile(h, path);
    public int LoadMagnitude(IntPtr h, string path) => Spectrometer.CM_Emission_LoadMagiudeFile(h, path);
    public int Configure(IntPtr h, SetEmissionSP100Config c) => Spectrometer.CM_SetEmissionSP100(h, c.IsEnabled, c.nStartPos, c.nEndPos, c.dMeanThreshold);
    public int AutoTime(IntPtr h, ref float time, ConfigSpectrum c) => Spectrometer.CM_Emission_GetAutoTime(h, ref time, c.MaxIntegralTime, c.BeginIntegralTime, c.Saturation);
    public int Dark(IntPtr h, float time, int average, int filter, float[] data, bool adaptive) => adaptive
        ? Spectrometer.CM_Emission_AutoDarkStorage(h, time, average, filter, data)
        : Spectrometer.CM_Emission_DarkStorage(h, time, average, filter, data);
    public int InitAutoDark(IntPtr h, SelfAdaptionInitDark c) => Spectrometer.CM_Emission_Init_Auto_Dark(h, c.BeginIntegralTime, c.StepTime, c.StepCount, c.NumberOfAverage);

    public LocalSpectrumCapture Capture(IntPtr h, LocalSpectrumParameters p, ConfigSpectrum c, float[] dark)
    {
        var settings = c.GetDataConfig;
        float time = p.IntegralTime;
        if (p.Eqe)
        {
            if (settings.IsSyncFrequencyEnabled) throw new NotSupportedException("本地 EQE 采集暂不支持同步频率模式。");
            COLOR_PARA_EQE eqe = new() { fPL = new float[4001] };
            LocalSpectrumSession.Check(Spectrometer.CM_Emission_GetDataEQE(h, (TRIGGER_MODE)0, time, p.NumberOfAverage, settings.FilterBW, dark, 0, 0, p.AFactor, p.Voltage, p.Current, ref eqe), "EQE 采集");
            // Common color fields have the same names, but the native EQE ABI is a different structure.
            COLOR_PARA data = JsonConvert.DeserializeObject<COLOR_PARA>(JsonConvert.SerializeObject(eqe));
            return new() { IntegralTime = time, Data = data, EqeData = eqe };
        }
        COLOR_PARA result = new() { fPL = new float[10000], fRi = new float[15] };
        int code = settings.IsSyncFrequencyEnabled
            ? Spectrometer.CM_Emission_GetDataSyncfreq(h, (TRIGGER_MODE)0, settings.Syncfreq, settings.SyncfreqFactor, ref time, p.NumberOfAverage, settings.FilterBW, dark, 0, 0, settings.SetWL1, settings.SetWL2, ref result)
            : Spectrometer.CM_Emission_GetData(h, (TRIGGER_MODE)0, time, p.NumberOfAverage, settings.FilterBW, dark, 0, 0, settings.SetWL1, settings.SetWL2, ref result);
        LocalSpectrumSession.Check(code, "光谱采集");
        return new() { IntegralTime = time, Data = result };
    }
}

internal sealed class LocalSpectrumSession : IDisposable
{
    // A driver which failed release may still invoke its callback; retain its managed target until process exit.
    private static readonly List<LocalSpectrumSession> QuarantinedSessions = [];
    private readonly object gate = new();
    private readonly ILocalSpectrumNative native;
    private readonly Spectrometer.Emission_CallBack callback = (_, _) => 1;
    private IntPtr handle;
    private SpectrometerDriverLease? lease;
    private ConfigSpectrum? openedConfig;
    private float[] dark = new float[10000];
    private bool disposed;
    private bool quarantined;
    private bool adaptiveDarkInitialized;
    public bool IsOpen { get { lock (gate) return handle != IntPtr.Zero && !quarantined; } }
    public bool IsQuarantined { get { lock (gate) return quarantined; } }

    internal LocalSpectrumSession(ILocalSpectrumNative? native = null) => this.native = native ?? new LocalSpectrumNative();

    public void Open(ConfigSpectrum config)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (quarantined) throw new InvalidOperationException("光谱仪释放失败，请重启程序后再连接。");
            if (handle != IntPtr.Zero) return;
            if (string.IsNullOrWhiteSpace(config.SN)) throw new InvalidOperationException("请先选择物理光谱仪的 SN。");
            ConfigSpectrum snapshot = JsonConvert.DeserializeObject<ConfigSpectrum>(JsonConvert.SerializeObject(config))!;
            lease = SpectrometerDriverLease.TryAcquire() ?? throw new InvalidOperationException("本机光谱仪驱动正在被其他光谱窗口或设备占用，请先断开该连接。");
            try
            {
                handle = native.Create((int)snapshot.SpectrometerType, callback);
                if (handle == IntPtr.Zero) throw new InvalidOperationException("创建本地光谱仪失败，请检查驱动和许可证。");
                Check(native.Init(handle, int.TryParse(snapshot.ComPort, out int port) ? port : 0, snapshot.BaudRate), "连接光谱仪");
                var serial = new StringBuilder(1024);
                Check(native.SerialNumber(handle, serial), "读取光谱仪 SN");
                if (!string.Equals(serial.ToString().Trim(), snapshot.SN.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"实际连接的光谱仪 SN 为 {serial}，与所选 {snapshot.SN} 不一致。");
                if (snapshot.SetEmissionSP100Config.IsEnabled) Check(native.Configure(handle, snapshot.SetEmissionSP100Config), "设置 SP100");
                openedConfig = snapshot;
            }
            catch { Close(); throw; }
        }
    }

    public LocalSpectrumCapture Capture(LocalSpectrumParameters parameters, ConfigSpectrum config)
    {
        lock (gate)
        {
            EnsureOpen(config);
            parameters.Validate();
            if (parameters.AutoInitDark && config.IsShutterEnable)
                throw new NotSupportedException("本地采集尚未接入外部快门。请关闭自动校零，手动遮光并校零后再测量。");
            string wave = ResolvePath(config.WavelengthFile), magnitude = ResolvePath(config.MaguideFile);
            string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wave, magnitude);
            if (error != null) throw new InvalidOperationException(error);
            if (config.SetEmissionSP100Config.IsEnabled || openedConfig!.SetEmissionSP100Config.IsEnabled)
            {
                Check(native.Configure(handle, config.SetEmissionSP100Config), "设置 SP100");
                openedConfig!.SetEmissionSP100Config = config.SetEmissionSP100Config;
            }
            Check(native.LoadWavelength(handle, wave), "加载波长校正");
            Check(native.LoadMagnitude(handle, magnitude), "加载幅度校正");
            if (parameters.AutoIntegration)
            {
                float time = parameters.IntegralTime;
                Check(native.AutoTime(handle, ref time, config), "自动积分");
                parameters.IntegralTime = time;
                parameters.Validate();
            }
            if (parameters.AutoInitDark || parameters.SelfAdaptionInitDark)
                Dark(parameters, config, parameters.SelfAdaptionInitDark);
            return native.Capture(handle, parameters, config, dark);
        }
    }

    public void Dark(LocalSpectrumParameters p, ConfigSpectrum c, bool adaptive = false)
    {
        lock (gate)
        {
            EnsureOpen(c);
            p.Validate();
            if (adaptive && !adaptiveDarkInitialized) throw new InvalidOperationException("请先完成自适应校零初始化，再使用自适应暗校正。");
            var buffer = new float[10000];
            Check(native.Dark(handle, p.IntegralTime, p.NumberOfAverage, c.GetDataConfig.FilterBW, buffer, adaptive), "光谱校零");
            dark = buffer;
        }
    }

    public void InitAutoDark(ConfigSpectrum c)
    {
        lock (gate)
        {
            EnsureOpen(c);
            adaptiveDarkInitialized = false;
            Check(native.InitAutoDark(handle, c.SelfAdaptionInitDark), "自适应校零");
            adaptiveDarkInitialized = true;
        }
    }

    private void EnsureOpen(ConfigSpectrum config)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (handle == IntPtr.Zero || quarantined) throw new InvalidOperationException("请先打开本地光谱仪。");
        if (openedConfig!.SN != config.SN || openedConfig.SpectrometerType != config.SpectrometerType || openedConfig.ComPort != config.ComPort || openedConfig.BaudRate != config.BaudRate)
            throw new InvalidOperationException("光谱仪连接配置已改变，请关闭后重新打开。");
    }

    internal static string ResolvePath(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
    internal static void Check(int code, string operation)
    {
        if (code != 1) throw new InvalidOperationException($"{operation}失败（{code}）：{Spectrometer.GetErrorMessage(code)}");
    }

    public void Close()
    {
        lock (gate)
        {
            if (quarantined) throw new InvalidOperationException("光谱仪释放失败，请重启程序。");
            try
            {
                if (handle != IntPtr.Zero)
                {
                    try { Check(native.Close(handle), "关闭光谱仪"); }
                    finally { Check(native.Release(handle), "释放光谱仪"); }
                }
            }
            catch
            {
                quarantined = true;
                lease?.Quarantine();
                lock (QuarantinedSessions) QuarantinedSessions.Add(this);
                throw;
            }
            finally
            {
                handle = IntPtr.Zero;
                openedConfig = null;
                dark = new float[10000];
                adaptiveDarkInitialized = false;
                lease?.Dispose();
                if (!quarantined) lease = null;
            }
        }
    }

    public void Dispose() { lock (gate) { if (disposed) return; try { Close(); } finally { disposed = true; } } }
}
