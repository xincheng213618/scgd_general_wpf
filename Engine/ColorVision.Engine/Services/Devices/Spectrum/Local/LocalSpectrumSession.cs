using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;

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
    // The service/flow contract stores SMU current in mA; native EQE accepts A.
    public FlowEngineLib.SMUResultData? SMUData { get; set; }

    internal void ApplySmuData(object? value)
    {
        var data = value switch { null => null, JObject json => json, JToken => null, _ => JObject.FromObject(value) };
        if (data?["V"] == null || data["V"]!.Type == JTokenType.Null || data["I"] == null || data["I"]!.Type == JTokenType.Null)
            throw new InvalidOperationException("EQE 缺少本次流程上游源表的电压、电流。请先执行源表测量，或切换为手动输入：本地光谱节点关闭“使用上游源表电压/流”，旧 EQE 节点开启“自定义电压/流”。");
        SMUData = data.ToObject<FlowEngineLib.SMUResultData>()!;
        Voltage = (float)SMUData.V;
        Current = (float)(SMUData.I / 1000.0);
    }

    public void Validate()
    {
        if (!float.IsFinite(IntegralTime) || IntegralTime <= 0 || NumberOfAverage < 1)
            throw new InvalidOperationException("积分时间必须大于 0，平均次数至少为 1。");
        if (Eqe && (!double.IsFinite(AFactor) || AFactor <= 0 || !float.IsFinite(Voltage) || !float.IsFinite(Current) || Current <= 0))
            throw new InvalidOperationException("EQE 系数和电流必须大于 0，电压必须为有限值。");
    }
}

public sealed class LocalSpectrumCapture
{
    public int MeasurementId { get; set; }
    public DateTime CapturedAt { get; init; } = DateTime.Now;
    public string DeviceCode { get; set; } = string.Empty;
    public float IntegralTime { get; init; }
    public COLOR_PARA Data { get; init; }
    public COLOR_PARA_EQE? EqeData { get; init; }
    public string? AcquisitionParametersJson { get; set; }
    public int? NDPort { get; internal set; }
    internal LocalSpectrumNd.Calibration? NdCalibration { get; set; }
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
    int ConfigureSmoothing(IntPtr handle, SpectrumSmoothingMethod method, int filterWidth);
    int AutoTime(IntPtr handle, ref float time, ConfigSpectrum config, CancellationToken cancellationToken);
    int Dark(IntPtr handle, float time, int average, int filter, float[] data, bool adaptive);
    int InitAutoDark(IntPtr handle, SelfAdaptionInitDark config);
    LocalSpectrumCapture Capture(IntPtr handle, LocalSpectrumParameters parameters, GetDataConfig settings, float[] dark);
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
    public int ConfigureSmoothing(IntPtr h, SpectrumSmoothingMethod method, int filterWidth) => SpectrumSmoothing.Configure(h, method, filterWidth);
    public int AutoTime(IntPtr h, ref float time, ConfigSpectrum c, CancellationToken cancellationToken)
        => Spectrometer.CM_Emission_GetAutoTimeCancelable(h, ref time, c.MaxIntegralTime, c.BeginIntegralTime, c.Saturation, () => cancellationToken.IsCancellationRequested ? 1 : 0);
    public int Dark(IntPtr h, float time, int average, int filter, float[] data, bool adaptive) => adaptive
        ? Spectrometer.CM_Emission_AutoDarkStorage(h, time, average, filter, data)
        : Spectrometer.CM_Emission_DarkStorage(h, time, average, filter, data);
    public int InitAutoDark(IntPtr h, SelfAdaptionInitDark c) => Spectrometer.CM_Emission_Init_Auto_Dark(h, c.BeginIntegralTime, c.StepTime, c.StepCount, c.NumberOfAverage);

    public LocalSpectrumCapture Capture(IntPtr h, LocalSpectrumParameters p, GetDataConfig settings, float[] dark)
    {
        float time = p.IntegralTime;
        if (p.Eqe)
        {
            if (settings.IsSyncFrequencyEnabled) throw new NotSupportedException("本地 EQE 采集暂不支持同步频率模式。");
            COLOR_PARA_EQE eqe = new() { fPL = new float[4001] };
            LocalSpectrumSession.Check(Spectrometer.CM_Emission_GetDataEQE(h, (TRIGGER_MODE)0, time, p.NumberOfAverage, settings.FilterBW, dark, 0, 0, p.AFactor, p.Voltage, p.Current, ref eqe), "EQE 采集");
            // Common color fields have the same names, but the native EQE ABI is a different structure.
            COLOR_PARA data = JsonConvert.DeserializeObject<COLOR_PARA>(JsonConvert.SerializeObject(eqe));
            return new() { IntegralTime = time, Data = SpectrumColorRendering.Apply(data), EqeData = eqe };
        }
        COLOR_PARA result = new() { fPL = new float[10000], fRi = new float[15] };
        int code = settings.IsSyncFrequencyEnabled
            ? Spectrometer.CM_Emission_GetDataSyncfreq(h, (TRIGGER_MODE)0, settings.Syncfreq, settings.SyncfreqFactor, ref time, p.NumberOfAverage, settings.FilterBW, dark, 0, 0, settings.SetWL1, settings.SetWL2, ref result)
            : Spectrometer.CM_Emission_GetData(h, (TRIGGER_MODE)0, time, p.NumberOfAverage, settings.FilterBW, dark, 0, 0, settings.SetWL1, settings.SetWL2, ref result);
        LocalSpectrumSession.Check(code, "光谱采集");
        return new() { IntegralTime = time, Data = SpectrumColorRendering.Apply(result) };
    }
}

internal sealed class LocalSpectrumSession : IDisposable
{
    // A driver which failed release may still invoke its callback; retain its managed target until process exit.
    private static readonly List<LocalSpectrumSession> QuarantinedSessions = [];
    private readonly object gate = new();
    private readonly ILocalSpectrumNative native;
    private readonly LocalSpectrumShutter shutter;
    private readonly LocalSpectrumNd nd;
    private readonly Action<ConfigSpectrum> ensureLicenseAvailable;
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

    internal LocalSpectrumSession(ILocalSpectrumNative? native = null, Action<ConfigSpectrum>? ensureLicenseAvailable = null, LocalSpectrumShutter? shutter = null, LocalSpectrumNd? nd = null)
    {
        this.native = native ?? new LocalSpectrumNative();
        this.shutter = shutter ?? new LocalSpectrumShutter();
        this.nd = nd ?? new LocalSpectrumNd();
        this.ensureLicenseAvailable = ensureLicenseAvailable ?? (_ => { });
    }

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
                ensureLicenseAvailable(snapshot);
                if (snapshot.NDConfig.IsNDPort)
                {
                    using var operation = nd.Acquire(snapshot.NDConfig);
                    operation.GetPort();
                }
            }
            catch { Close(); throw; }
        }
    }

    public LocalSpectrumCapture Capture(LocalSpectrumParameters parameters, ConfigSpectrum config, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            EnsureOpen(config);
            cancellationToken.ThrowIfCancellationRequested();
            parameters.Validate();
            // Freeze the selection and width together while native calls run; the UI can edit the live config.
            var configured = config.GetDataConfig;
            var settings = new GetDataConfig
            {
                SmoothingMethod = configured.SmoothingMethod,
                FilterBW = configured.FilterBW,
                IsSyncFrequencyEnabled = configured.IsSyncFrequencyEnabled,
                Syncfreq = configured.Syncfreq,
                SyncfreqFactor = configured.SyncfreqFactor,
                SetWL1 = configured.SetWL1,
                SetWL2 = configured.SetWL2
            };
            SpectrumSmoothing.Validate(settings.SmoothingMethod, settings.FilterBW);
            bool withNd = parameters.IsWithND && config.NDConfig.IsNDPort;
            using var ndOperation = config.NDConfig.IsNDPort ? nd.Acquire(config.NDConfig) : null;
            int? ndPort = ndOperation?.GetPort();
            LocalSpectrumNd.Calibration? calibration = ndPort.HasValue ? LocalSpectrumNd.ResolveCalibration(config, ndPort.Value) : null;
            string wave = calibration?.WavelengthFile ?? ResolvePath(config.WavelengthFile), magnitude = calibration?.MagnitudeFile ?? ResolvePath(config.MaguideFile);
            string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wave, magnitude);
            if (error != null) throw new InvalidOperationException(error);
            using var lightPathOperation = config.IsShutterEnable && !SharesNdShutter(config) ? shutter.AcquireOperation(config.ShutterCfg) : null;
            Check(native.ConfigureSmoothing(handle, settings.SmoothingMethod, settings.FilterBW), "设置光谱平滑算法");
            if (config.SetEmissionSP100Config.IsEnabled || openedConfig!.SetEmissionSP100Config.IsEnabled)
            {
                Check(native.Configure(handle, config.SetEmissionSP100Config), "设置 SP100");
                openedConfig!.SetEmissionSP100Config = config.SetEmissionSP100Config;
            }
            Check(native.LoadWavelength(handle, wave), "加载波长校正");
            Check(native.LoadMagnitude(handle, magnitude), "加载幅度校正");
            if (config.IsShutterEnable && !SharesNdShutter(config)) shutter.SetOpen(true, config.ShutterCfg);
            if (parameters.AutoIntegration)
            {
                float time = withNd ? FindNdAutoTime(ndOperation!, config, cancellationToken) : FindAutoTime(config, cancellationToken);
                if (withNd)
                {
                    ndPort = ndOperation!.GetPort();
                    calibration = LocalSpectrumNd.ResolveCalibration(config, ndPort.Value);
                    wave = calibration.WavelengthFile;
                    magnitude = calibration.MagnitudeFile;
                    Check(native.LoadWavelength(handle, wave), "加载 ND 波长校正");
                    Check(native.LoadMagnitude(handle, magnitude), "加载 ND 幅度校正");
                }
                parameters.IntegralTime = time;
                parameters.Validate();
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (parameters.AutoInitDark || parameters.SelfAdaptionInitDark)
                DarkCore(parameters, config, parameters.SelfAdaptionInitDark, ndOperation);
            cancellationToken.ThrowIfCancellationRequested();
            var result = native.Capture(handle, parameters, settings, dark);
            result.NDPort = ndPort;
            result.NdCalibration = calibration;
            result.AcquisitionParametersJson = JsonConvert.SerializeObject(new
            {
                Parameters = parameters, Acquisition = settings,
                config.SN, NDPort = ndPort, WavelengthFile = wave, MagnitudeFile = magnitude
            });
            return result;
        }
    }

    public void Dark(LocalSpectrumParameters p, ConfigSpectrum c, bool adaptive = false)
    {
        lock (gate)
        {
            EnsureOpen(c);
            using var ndOperation = !adaptive && c.NDConfig.IsNDPort ? nd.Acquire(c.NDConfig) : null;
            DarkCore(p, c, adaptive, ndOperation);
        }
    }

    private void DarkCore(LocalSpectrumParameters p, ConfigSpectrum c, bool adaptive, ILocalSpectrumNdOperation? ndOperation)
    {
        EnsureOpen(c);
        p.Validate();
        if (adaptive && !adaptiveDarkInitialized) throw new InvalidOperationException("请先完成自适应校零初始化，再使用自适应暗校正。");
        using var lightPathOperation = !adaptive && c.IsShutterEnable && !SharesNdShutter(c) ? shutter.AcquireOperation(c.ShutterCfg) : null;
        var buffer = new float[10000];
        void AcquireDark() => Check(native.Dark(handle, p.IntegralTime, p.NumberOfAverage, c.GetDataConfig.FilterBW, buffer, adaptive), "光谱校零");
        if (adaptive) AcquireDark();
        else WithDarkPath(c, ndOperation, AcquireDark);
        dark = buffer;
    }

    public void InitAutoDark(ConfigSpectrum c)
    {
        lock (gate)
        {
            EnsureOpen(c);
            adaptiveDarkInitialized = false;
            using var ndOperation = c.NDConfig.IsNDPort ? nd.Acquire(c.NDConfig) : null;
            using var lightPathOperation = c.IsShutterEnable && !SharesNdShutter(c) ? shutter.AcquireOperation(c.ShutterCfg) : null;
            WithDarkPath(c, ndOperation, () => Check(native.InitAutoDark(handle, c.SelfAdaptionInitDark), "自适应校零"));
            adaptiveDarkInitialized = true;
        }
    }

    internal int ControlNd(ConfigSpectrum config, int? port, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            token.ThrowIfCancellationRequested();
            using var operation = nd.Acquire(config.NDConfig);
            int actual = port.HasValue ? operation.SetPort(port.Value) : operation.GetPort();
            if (port.HasValue && actual != port.Value) throw new InvalidOperationException($"ND 未确认到达孔位 {port.Value}，实际孔位为 {actual}。");
            token.ThrowIfCancellationRequested();
            return actual;
        }
    }

    private float FindAutoTime(ConfigSpectrum config, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        float time = config.BeginIntegralTime;
        int code = native.AutoTime(handle, ref time, config, token);
        if (code == Spectrometer.AutoExposureCancelledErrorCode) throw new OperationCanceledException("光谱仪自动曝光已停止。", token);
        Check(code, "自动积分");
        token.ThrowIfCancellationRequested();
        return time;
    }

    private float FindNdAutoTime(ILocalSpectrumNdOperation operation, ConfigSpectrum config, CancellationToken token)
    {
        NDConfig settings = config.NDConfig;
        if (!double.IsFinite(settings.NDMinExpTime) || !double.IsFinite(settings.NDMaxExpTime)
            || settings.NDMinExpTime < 0 || settings.NDMaxExpTime <= settings.NDMinExpTime)
            throw new InvalidOperationException("ND 自动积分的最大时间必须大于最小时间，且最小时间不能为负数。");
        if (settings.NDRate.Count == 0 || settings.NDRate.Any(rate => rate < 0) || settings.NDRate.Distinct().Count() != settings.NDRate.Count)
            throw new InvalidOperationException("请配置非负且不重复的 ND 倍率，每个列表位置对应同索引的物理孔位。");
        int[] ports = settings.NDRate.Select((rate, port) => (rate, port)).OrderBy(item => item.rate).Select(item => item.port).ToArray();
        // Validate every candidate before moving any hardware; a missing file must not cause an uncalibrated capture.
        foreach (int candidate in ports) LocalSpectrumNd.ResolveCalibration(config, candidate);
        int current = settings.EnableResetND ? operation.SetPort(ports[0]) : operation.GetPort();
        var visited = new System.Collections.Generic.HashSet<int>();
        while (visited.Add(current))
        {
            int index = Array.IndexOf(ports, current);
            if (index < 0) throw new InvalidOperationException($"当前 ND 孔位 {current} 未配置倍率。");
            float time = FindAutoTime(config, token);
            int next = time < settings.NDMinExpTime ? index + 1 : time > settings.NDMaxExpTime ? index - 1 : index;
            if (next == index) return time;
            if (next < 0 || next >= ports.Length || visited.Contains(ports[next]))
                throw new InvalidOperationException("ND 自动积分无法在配置的孔位及积分范围内收敛，请检查光路和 ND 参数。");
            token.ThrowIfCancellationRequested();
            current = operation.SetPort(ports[next]);
        }
        throw new InvalidOperationException("ND 自动积分无法收敛。");
    }

    private void WithDarkPath(ConfigSpectrum config, ILocalSpectrumNdOperation? operation, Action acquireDark)
    {
        if ((config.IsShutterEnable && !SharesNdShutter(config)) || operation == null) { WithClosedShutter(config, acquireDark); return; }
        int darkPort = config.NDConfig.DarkNDPort >= 0 ? config.NDConfig.DarkNDPort : SharesNdShutter(config) ? config.ShutterCfg.DarkPort : -1;
        if (darkPort < 0) throw new InvalidOperationException("请配置 ND 校零孔位或启用外部快门后再校零。");
        int previous = operation.GetPort();
        if (previous == darkPort) throw new InvalidOperationException("当前 ND 位于校零孔，请先切到测量孔位。");
        Exception? failure = null;
        try { operation.SetPort(darkPort); acquireDark(); }
        catch (Exception ex) { failure = ex; }
        try { operation.SetPort(previous); }
        catch (Exception ex) { throw new InvalidOperationException($"校零后恢复 ND 测量位 {previous} 失败，请检查光路。", failure == null ? ex : new AggregateException(failure, ex)); }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool SharesNdShutter(ConfigSpectrum config) => config.IsShutterEnable && config.ShutterCfg.UseFilterWheel
        && config.NDConfig.IsNDPort && config.NDConfig.IsBingNDDevice
        && string.Equals(config.ShutterCfg.FilterWheelDeviceCode, config.NDConfig.NDBindDeviceCode, StringComparison.Ordinal);

    public void ControlShutter(string eventName, ConfigSpectrum config)
    {
        lock (gate)
        {
            EnsureOpen(config);
            if (eventName == "ShutterDisconnect") { shutter.Disconnect(); return; }
            if (!config.IsShutterEnable) throw new InvalidOperationException("请先在光谱仪配置中启用快门。");
            using var lightPathOperation = shutter.AcquireOperation(config.ShutterCfg);
            switch (eventName)
            {
                case "ShutterConnect": shutter.Connect(config.ShutterCfg); break;
                case "ShutterDoOpen": shutter.SetOpen(true, config.ShutterCfg); break;
                case "ShutterDoClose": shutter.SetOpen(false, config.ShutterCfg); break;
                default: throw new NotSupportedException($"未知快门操作：{eventName}。");
            }
        }
    }

    private void WithClosedShutter(ConfigSpectrum config, Action operation)
    {
        if (!config.IsShutterEnable) { operation(); return; }
        Exception? failure = null;
        try
        {
            shutter.SetOpen(false, config.ShutterCfg);
            operation();
        }
        catch (Exception ex) { failure = ex; }
        // Even an unconfirmed close may have moved the shutter. Always attempt to restore light.
        try { shutter.SetOpen(true, config.ShutterCfg); }
        catch (Exception ex)
        {
            string restore = config.ShutterCfg.UseFilterWheel ? "恢复滤色轮测量位失败" : "恢复打开快门失败";
            throw new InvalidOperationException($"{failure?.Message} 校零后{restore}：{ex.Message} 请检查光路。".Trim(),
                failure == null ? ex : new AggregateException(failure, ex));
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
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
                shutter.Disconnect();
                nd.Disconnect();
            }
        }
    }

    public void Dispose() { lock (gate) { if (disposed) return; try { Close(); } finally { disposed = true; } } }
}
