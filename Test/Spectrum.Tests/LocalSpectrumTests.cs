using ColorVision.Engine.Services;
using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using cvColorVision;
using EngineSettings = ColorVision.Engine.Services.Devices.Spectrum.Configs.GetDataConfig;
using System.IO;
using System.Text;

namespace Spectrum.Tests;

[CollectionDefinition("LocalSpectrumDriver", DisableParallelization = true)]
public sealed class LocalSpectrumDriverCollection;

[Collection("LocalSpectrumDriver")]
public sealed class LocalSpectrumTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData(-1, false, false)]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    [InlineData(null, true, true)]
    [InlineData(-1, true, true)]
    public void LocalOpenChecksOnlyStoredExpiryAndReleasesExpiredConnection(int? expirySeconds, bool experimentalFeatures, bool allowed)
    {
        DateTime now = new(2026, 10, 13, 12, 0, 0);
        LicenseModel license = new() { LiceType = 1, ExpiryDate = expirySeconds.HasValue ? now.AddSeconds(expirySeconds.Value) : null, LicenseValue = "not a license payload" };
        ExperimentalFeaturesConfig settings = new() { IsEnabled = experimentalFeatures };
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native,
            ensureLicenseAvailable: config =>
            {
                Assert.Equal(native.Serial, config.SN);
                LocalSpectrumLicense.EnsureNotExpired(license.ExpiryDate, settings.IsEnabled, now);
            });

        if (allowed)
        {
            session.Open(Config());
            Assert.True(session.IsOpen);
            Assert.Equal(0, native.CloseCalls);
            Assert.Equal(0, native.ReleaseCalls);
        }
        else
        {
            Assert.Throws<LocalSpectrumLicenseException>(() => session.Open(Config()));
            Assert.False(session.IsOpen);
            Assert.False(session.IsQuarantined);
            Assert.Equal(1, native.CloseCalls);
            Assert.Equal(1, native.ReleaseCalls);
            using var lease = SpectrometerDriverLease.TryAcquire();
            Assert.NotNull(lease);
        }
        Assert.Equal(1, native.CreateCalls);
    }

    [Fact]
    public void ExpiryDoesNotInterruptAnOpenSpectrumSessionAndIsRecheckedAfterClose()
    {
        using var files = new CalibrationFiles();
        DateTime now = new(2026, 10, 13, 12, 0, 0);
        DateTime expiry = now.AddSeconds(1);
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native,
            ensureLicenseAvailable: _ => LocalSpectrumLicense.EnsureNotExpired(expiry, false, now));
        session.Open(files.Config);
        now = now.AddSeconds(2);

        session.Open(files.Config);
        session.Capture(new(), files.Config);
        Assert.True(session.IsOpen);
        Assert.Equal(1, native.CreateCalls);
        Assert.Equal(0, native.CloseCalls);

        session.Close();
        Assert.Throws<LocalSpectrumLicenseException>(() => session.Open(files.Config));
        Assert.False(session.IsOpen);
        Assert.Equal(2, native.CreateCalls);
        Assert.Equal(2, native.CloseCalls);
        Assert.Equal(2, native.ReleaseCalls);
    }

    [Fact]
    public void OfflinePreferenceExposesClosedAndLocalStatusIgnoresRemoteHeartbeat()
    {
        var state = new SpectrumBackendState(true);
        state.ObserveService(DeviceStatusType.OffLine);
        Assert.Equal(DeviceStatusType.Closed, state.Status);
        state.SetLocalStatus(DeviceStatusType.Opened);
        state.SetPreference(false);
        state.ObserveService(DeviceStatusType.OffLine);
        Assert.True(state.OpensLocally);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
        Assert.Throws<InvalidOperationException>(() => state.BeginServiceCommand("GetData"));
        state.SetLocalStatus(DeviceStatusType.Closed);
        Assert.False(state.OpensLocally);
    }

    [Fact]
    public void LostServiceConnectionDoesNotReleaseItsHandle()
    {
        var state = new SpectrumBackendState(true);
        state.BeginServiceCommand("Open");
        state.ObserveService(DeviceStatusType.Opened);
        state.EndServiceCommand();
        state.ObserveService(DeviceStatusType.OffLine);
        Assert.False(state.OpensLocally);
        Assert.Throws<InvalidOperationException>(state.BeginLocalCommand);
        state.ObserveService(DeviceStatusType.Closed);
        state.BeginLocalCommand();
        Assert.Throws<InvalidOperationException>(state.BeginLocalCommand);
        Assert.Throws<InvalidOperationException>(() => state.BeginServiceCommand("Open"));
        state.EndLocalCommand();
    }

    [Fact]
    public void FailedInitializationReleasesNativeHandleAndAllowsRetry()
    {
        var native = new FakeNative { InitCode = -123 };
        using var session = new LocalSpectrumSession(native);
        Assert.Throws<InvalidOperationException>(() => session.Open(Config()));
        Assert.Equal(1, native.CloseCalls);
        Assert.Equal(1, native.ReleaseCalls);
        Assert.False(session.IsOpen);
        native.InitCode = 1;
        session.Open(Config());
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void WrongSerialIsRejectedAndHandleReleased()
    {
        var native = new FakeNative { Serial = "OTHER" };
        using var session = new LocalSpectrumSession(native);
        Assert.Contains("SN", Assert.Throws<InvalidOperationException>(() => session.Open(Config())).Message);
        Assert.Equal(1, native.ReleaseCalls);
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void RepeatedOpenReusesHandleAndOtherSessionCannotClaimIt()
    {
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        using var second = new LocalSpectrumSession(new FakeNative());
        session.Open(Config());
        session.Open(Config());
        Assert.Equal(1, native.CreateCalls);
        Assert.Throws<InvalidOperationException>(() => second.Open(Config()));
        session.Close();
        second.Open(Config());
        Assert.True(second.IsOpen);
    }

    [Fact]
    public void SharedDriverLeaseAlsoProtectsNonEngineClients()
    {
        using var lease = SpectrometerDriverLease.TryAcquire();
        Assert.NotNull(lease);
        using var session = new LocalSpectrumSession(new FakeNative());
        Assert.Throws<InvalidOperationException>(() => session.Open(Config()));
    }

    [Fact]
    public void CalibrationFailurePreventsNativeMeasurement()
    {
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        var config = Config();
        session.Open(config);
        Assert.Throws<InvalidOperationException>(() => session.Capture(new(), config));
        Assert.Equal(0, native.CaptureCalls);
    }

    [Fact]
    public void CaptureUsesAutoTimeDarkAndCalibrationThenReturnsSpectrumInMemory()
    {
        using var files = new CalibrationFiles();
        var config = files.Config;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(config);
        var result = session.Capture(new() { AutoIntegration = true, AutoInitDark = true }, config);
        Assert.Equal(240, result.IntegralTime);
        Assert.Equal(1, native.DarkCalls);
        Assert.Equal(new[] { "smoothing", "wave", "magnitude", "auto", "dark", "capture" }, native.Calls);
        Assert.Equal(401, result.Data.fPL.Length);
        Assert.True(session.IsOpen);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShutterCaptureOpensForAutoTimeAndClosesOnlyForDark(bool eqe, bool useFilterWheel)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        files.Config.ShutterCfg.UseFilterWheel = useFilterWheel;
        var native = new FakeNative();
        var port = new FakeShutterPort(native.Calls);
        var wheel = new FakeFilterWheel(native.Calls, files.Config.ShutterCfg);
        using var session = new LocalSpectrumSession(native, shutter: new(_ => port, _ => wheel));
        session.Open(files.Config);
        var result = session.Capture(new() { AutoIntegration = true, AutoInitDark = true, Eqe = eqe, Current = 1 }, files.Config);
        Assert.Equal(240, result.IntegralTime);
        Assert.Equal(new[] { "smoothing", "wave", "magnitude", "shutter-open", "auto", "shutter-close", "dark", "shutter-open", "capture" }, native.Calls);
        Assert.Equal(240, native.DarkTime);
        Assert.Equal(useFilterWheel ? 0 : 3, port.DiscardCalls);
        if (useFilterWheel)
        {
            Assert.Equal(new[] { 5, 0, 5 }, wheel.Ports);
            Assert.True(wheel.Disposed);
        }
        session.Close();
        Assert.Equal(!useFilterWheel, port.Disposed);
    }

    [Theory]
    [InlineData(true, 1, false, false)]
    [InlineData(false, -3, false, false)]
    [InlineData(false, 1, true, false)]
    [InlineData(false, -3, true, false)]
    [InlineData(true, 1, false, true)]
    [InlineData(false, -3, false, true)]
    [InlineData(false, 1, true, true)]
    [InlineData(false, -3, true, true)]
    public void FailedShutterOrDarkRestoresLightAndStopsMeasurement(bool closeFails, int darkCode, bool reopenFails, bool useFilterWheel)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        files.Config.ShutterCfg.UseFilterWheel = useFilterWheel;
        var native = new FakeNative { DarkCode = darkCode };
        int opens = 0;
        bool Confirm(bool open)
        {
            if (!open) return !closeFails;
            return ++opens <= 1 || !reopenFails;
        }
        var wheel = new FakeFilterWheel(native.Calls, files.Config.ShutterCfg) { Confirm = Confirm };
        var shutter = new LocalSpectrumShutter(_ => new FakeShutterPort(native.Calls)
        {
            Reply = command =>
            {
                bool open = command == "a";
                return [Confirm(open) == open ? "turn on" : "turn off"];
            }
        }, _ => wheel);
        using var session = new LocalSpectrumSession(native, shutter: shutter);
        session.Open(files.Config);
        var error = Assert.Throws<InvalidOperationException>(() => session.Capture(new() { AutoInitDark = true }, files.Config));
        string[] expected = ["smoothing", "wave", "magnitude", "shutter-open", "shutter-close"];
        Assert.Equal(expected.Concat(closeFails ? ["shutter-open"] : new[] { "dark", "shutter-open" }), native.Calls);
        Assert.Equal(closeFails ? 0 : 1, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
        if (reopenFails) Assert.Contains(useFilterWheel ? "恢复滤色轮测量位失败" : "恢复打开快门失败", error.Message);
        if (darkCode != 1) Assert.Contains("光谱校零失败", error.Message);
        if (useFilterWheel) Assert.True(wheel.Disposed);
    }

    [Fact]
    public void FailedShutterAcknowledgementPreventsCapture()
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native, shutter: new(_ => new FakeShutterPort(native.Calls) { Reply = _ => ["turn off"] }));
        session.Open(files.Config);
        Assert.Throws<InvalidOperationException>(() => session.Capture(new() { AutoIntegration = true, AutoInitDark = true }, files.Config));
        Assert.DoesNotContain("auto", native.Calls);
        Assert.Equal(0, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdaptiveInitializationClosesShutterButReadingTheDarkTableDoesNot(bool useFilterWheel)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        files.Config.ShutterCfg.UseFilterWheel = useFilterWheel;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native, shutter: new(_ => new FakeShutterPort(native.Calls), config => new FakeFilterWheel(native.Calls, config)));
        session.Open(files.Config);
        session.InitAutoDark(files.Config);
        Assert.Equal(new[] { "shutter-close", "init-adaptive", "shutter-open" }, native.Calls);
        native.Calls.Clear();
        session.Capture(new() { SelfAdaptionInitDark = true }, files.Config);
        Assert.Equal(new[] { "smoothing", "wave", "magnitude", "shutter-open", "dark", "capture" }, native.Calls);
        Assert.True(native.LastDarkAdaptive);
    }

    [Fact]
    public void ShutterCommandsReuseConnectionAndReleaseItOnDisconnectAndSpectrumClose()
    {
        var config = Config();
        config.IsShutterEnable = true;
        var native = new FakeNative();
        var ports = new List<FakeShutterPort>();
        var shutter = new LocalSpectrumShutter(_ => { var port = new FakeShutterPort(native.Calls); ports.Add(port); return port; });
        using var session = new LocalSpectrumSession(native, shutter: shutter);
        session.Open(config);
        session.ControlShutter("ShutterConnect", config);
        session.ControlShutter("ShutterDoOpen", config);
        session.ControlShutter("ShutterDoClose", config);
        Assert.Single(ports);
        Assert.Equal(new[] { "shutter-open", "shutter-close" }, native.Calls);
        session.ControlShutter("ShutterDisconnect", config);
        Assert.True(ports[0].Disposed);
        session.ControlShutter("ShutterConnect", config);
        config.ShutterCfg.Addr = "COM2";
        session.ControlShutter("ShutterDoOpen", config);
        Assert.Equal(3, ports.Count);
        Assert.True(ports[1].Disposed);
        session.Close();
        Assert.All(ports, port => Assert.True(port.Disposed));
    }

    [Fact]
    public void ShutterTimeoutAndStaleOppositeReplyCannotConfirmCommand()
    {
        var config = new ShutterConfig { DelayTime = 1, OpenCmd = "OPEN", CloseCmd = "CLOSE" };
        var port = new FakeShutterPort([]) { Reply = _ => [] };
        var shutter = new LocalSpectrumShutter(_ => port);
        shutter.Connect(config);
        port.Pending.Enqueue("turn on");
        Assert.Throws<TimeoutException>(() => shutter.SetOpen(true, config));
        Assert.Equal(1, port.DiscardCalls);
        Assert.True(port.Disposed);

        port = new FakeShutterPort([]) { Reply = _ => ["turn off\r\nturn on"] };
        Assert.Throws<InvalidOperationException>(() => shutter.SetOpen(true, config));
        Assert.True(port.Disposed);
    }

    [Fact]
    public void ShutterRequiresOpenSpectrumAndEnabledConfiguration()
    {
        var config = Config();
        var native = new FakeNative();
        int connections = 0;
        using var session = new LocalSpectrumSession(native, shutter: new(_ => { connections++; return new FakeShutterPort([]); }));
        config.IsShutterEnable = true;
        Assert.Throws<InvalidOperationException>(() => session.ControlShutter("ShutterConnect", config));
        session.Open(config);
        config.IsShutterEnable = false;
        Assert.Throws<InvalidOperationException>(() => session.ControlShutter("ShutterDoOpen", config));
        Assert.Equal(0, connections);
    }

    [Fact]
    public void ConnectionConfigurationCannotChangeUnderAnOpenHandle()
    {
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        var config = Config();
        session.Open(config);
        config.SN = "CHANGED";
        Assert.Throws<InvalidOperationException>(() => session.Dark(new(), config));
        Assert.Equal(0, native.DarkCalls);
    }

    [Fact]
    public void MeasurementFailureDoesNotCloseOrReconnectTheDevice()
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative { CaptureFails = true };
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        Assert.Throws<InvalidOperationException>(() => session.Capture(new(), files.Config));
        Assert.Equal(1, native.CreateCalls);
        Assert.Equal(0, native.CloseCalls);
        Assert.True(session.IsOpen);
    }

    [Theory]
    [InlineData(float.NaN, 1)]
    [InlineData(0, 1)]
    [InlineData(100, 0)]
    public void InvalidMeasurementParametersAreRejected(float time, int average)
        => Assert.Throws<InvalidOperationException>(() => new LocalSpectrumParameters { IntegralTime = time, NumberOfAverage = average }.Validate());

    [Fact]
    public void NdCaptureLoadsTheSelectedCalibrationFilesOnEveryMeasurement()
    {
        using var first = new CalibrationFiles();
        using var second = new CalibrationFiles();
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(first.Config);
        session.Capture(new() { IsWithND = true }, first.Config);
        Assert.Equal(Path.GetFullPath(first.Config.MaguideFile), native.MagnitudePath);
        session.Capture(new() { IsWithND = true }, second.Config);
        Assert.Equal(Path.GetFullPath(second.Config.MaguideFile), native.MagnitudePath);
        Assert.Equal(2, native.CaptureCalls);
    }

    [Fact]
    public void ResultModelRetainsFullArrayAndDoesNotInventDatabaseIdentity()
    {
        var capture = new LocalSpectrumCapture { DeviceCode = "LOCAL.SPECTRUM", IntegralTime = 240, Data = NativeData() };
        var model = LocalSpectrumResultService.CreateModel(capture, new() { NumberOfAverage = 3 });
        Assert.Equal(0, model.Id);
        Assert.Equal("LOCAL.SPECTRUM", model.DeviceCode);
        Assert.Equal(240f, model.IntTime);
        Assert.Equal(401, Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(model.fPL!)!.Length);
        Assert.Contains("fCIEx", model.CieDataEx);
    }

    [Theory]
    [InlineData(1f, 401)]
    [InlineData(0.1f, 4001)]
    public void MainWindowResultViewUsesActualNativeGrid(float interval, int expected)
    {
        var previous = ColorVision.UI.ConfigService.Instance;
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler());
        try
        {
        COLOR_PARA data = NativeData();
        data.fInterval = interval;
        data.fPL = Enumerable.Repeat(1f, 10000).ToArray();
        var view = new ColorVision.Engine.Services.Devices.Spectrum.Views.ViewResultSpectrum(data);
        Assert.Equal(expected, view.fPL.Length);
        Assert.Equal(401, view.SpectralDatas.Count);
        Assert.Equal(780f, view.SpectralDatas[^1].Wavelength, 3);
        }
        finally { ColorVision.UI.ConfigService.SetInstance(previous); }
    }

    private static ConfigSpectrum Config() => new() { SN = "TEST-SPECTRUM", WavelengthFile = "missing-wave.dat", MaguideFile = "missing-mag.dat" };

    [Fact]
    public void SmoothingSelectionIsAppliedEachCaptureAndCanReturnToMeanForEqe()
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        files.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.SavitzkyGolay;
        files.Config.GetDataConfig.FilterBW = 7;
        session.Capture(new(), files.Config);
        Assert.Equal((SpectrumSmoothingMethod.SavitzkyGolay, 7), native.Smoothing);
        files.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.Mean;
        files.Config.GetDataConfig.FilterBW = 4;
        session.Capture(new() { Eqe = true, Current = 1 }, files.Config);
        Assert.Equal((SpectrumSmoothingMethod.Mean, 4), native.Smoothing);
        Assert.Equal(1, native.CreateCalls);
        Assert.Equal(2, native.CaptureCalls);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(5, -3)]
    public void InvalidSgOrFailedSelectionPreventsCaptureAndDark(int width, int selectionCode)
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative { SmoothingCode = selectionCode };
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        files.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.SavitzkyGolay;
        files.Config.GetDataConfig.FilterBW = width;
        Assert.Throws<InvalidOperationException>(() => session.Capture(new() { AutoInitDark = true }, files.Config));
        Assert.Equal(0, native.CaptureCalls);
        Assert.Equal(0, native.DarkCalls);
        Assert.DoesNotContain("wave", native.Calls);
    }

    [Fact]
    public void AcquisitionSelectionAndWidthStayTogetherDuringLiveConfigEdits()
    {
        using var files = new CalibrationFiles();
        files.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.SavitzkyGolay;
        files.Config.GetDataConfig.FilterBW = 7;
        var native = new FakeNative { OnLoadMagnitude = () => {
            files.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.Mean;
            files.Config.GetDataConfig.FilterBW = 4;
        } };
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        session.Capture(new(), files.Config);
        Assert.Equal((SpectrumSmoothingMethod.SavitzkyGolay, 7), native.Smoothing);
        Assert.Equal((SpectrumSmoothingMethod.SavitzkyGolay, 7), native.CaptureSmoothing);
        session.Capture(new(), files.Config);
        Assert.Equal((SpectrumSmoothingMethod.Mean, 4), native.CaptureSmoothing);
    }
    private static COLOR_PARA NativeData() => new() { fSpect1 = 380, fSpect2 = 780, fInterval = 1, fPL = Enumerable.Repeat(1f, 401).ToArray(), fRi = new float[15], fPh = 12, fCIEx = 1, fCIEy = 2, fCIEz = 3 };

    private sealed class FakeNative : ILocalSpectrumNative
    {
        public int InitCode = 1, CreateCalls, CloseCalls, ReleaseCalls, CaptureCalls, DarkCalls, DarkCode = 1;
        public float DarkTime;
        public bool LastDarkAdaptive;
        public bool CaptureFails;
        public int SmoothingCode = 1;
        public (SpectrumSmoothingMethod Method, int Width) Smoothing;
        public (SpectrumSmoothingMethod Method, int Width) CaptureSmoothing;
        public Action? OnLoadMagnitude;
        public string Serial = "TEST-SPECTRUM";
        public List<string> Calls = [];
        public IntPtr Create(int type, Spectrometer.Emission_CallBack callback) { CreateCalls++; return (IntPtr)123; }
        public int Init(IntPtr h, int port, int baud) => InitCode;
        public int SerialNumber(IntPtr h, StringBuilder serial) { serial.Append(Serial); return 1; }
        public int Close(IntPtr h) { CloseCalls++; return 1; }
        public int Release(IntPtr h) { ReleaseCalls++; return 1; }
        public int LoadWavelength(IntPtr h, string path) { Calls.Add("wave"); return 1; }
        public string? MagnitudePath;
        public int LoadMagnitude(IntPtr h, string path) { Calls.Add("magnitude"); MagnitudePath = path; OnLoadMagnitude?.Invoke(); return 1; }
        public int Configure(IntPtr h, ColorVision.Engine.Services.Devices.Spectrum.Configs.SetEmissionSP100Config c) => 1;
        public int ConfigureSmoothing(IntPtr h, SpectrumSmoothingMethod method, int filterWidth)
        { Calls.Add("smoothing"); Smoothing = (method, filterWidth); return SmoothingCode; }
        public int AutoTime(IntPtr h, ref float time, ConfigSpectrum c) { Calls.Add("auto"); time = 240; return 1; }
        public int Dark(IntPtr h, float time, int avg, int filter, float[] data, bool adaptive)
        { Calls.Add("dark"); DarkCalls++; DarkTime = time; LastDarkAdaptive = adaptive; return DarkCode; }
        public int InitAutoDark(IntPtr h, SelfAdaptionInitDark c) { Calls.Add("init-adaptive"); return 1; }
        public LocalSpectrumCapture Capture(IntPtr h, LocalSpectrumParameters p, EngineSettings settings, float[] dark)
        {
            CaptureSmoothing = (settings.SmoothingMethod, settings.FilterBW);
            Calls.Add("capture"); CaptureCalls++;
            if (CaptureFails) throw new InvalidOperationException("Native failure");
            return new() { IntegralTime = p.IntegralTime, Data = NativeData() };
        }
    }

    private sealed class FakeShutterPort(List<string> calls) : ILocalSpectrumShutterPort
    {
        public bool IsOpen { get; private set; }
        public bool Disposed;
        public int DiscardCalls;
        public Queue<string> Pending = new();
        public Func<string, string[]> Reply = command => command == "a" ? ["TURN ", "ON"] : ["turn ", "off"];
        public void Open() => IsOpen = true;
        public void DiscardInBuffer() { DiscardCalls++; Pending.Clear(); }
        public void Write(string command)
        {
            calls.Add(command == "a" ? "shutter-open" : "shutter-close");
            foreach (string response in Reply(command)) Pending.Enqueue(response);
        }
        public string ReadExisting() => Pending.TryDequeue(out string? value) ? value : string.Empty;
        public void Dispose() { Disposed = true; IsOpen = false; }
    }

    private sealed class FakeFilterWheel(List<string> calls, ShutterConfig config) : ILocalSpectrumFilterWheelOperation
    {
        public bool Disposed;
        public List<int> Ports = [];
        public Func<bool, bool> Confirm = _ => true;
        public void SetOpen(bool open)
        {
            Assert.False(Disposed);
            calls.Add(open ? "shutter-open" : "shutter-close");
            Ports.Add(open ? config.MeasurementPort : config.DarkPort);
            if (!Confirm(open)) throw new InvalidOperationException("滤色轮未确认到位。");
        }
        public void Dispose() => Disposed = true;
    }

    private sealed class CalibrationFiles : IDisposable
    {
        public ConfigSpectrum Config { get; } = LocalSpectrumTests.Config();
        public CalibrationFiles()
        {
            Config.WavelengthFile = Path.Combine(Path.GetTempPath(), $"local-spectrum-wave-{Guid.NewGuid():N}.dat");
            Config.MaguideFile = Path.Combine(Path.GetTempPath(), $"local-spectrum-mag-{Guid.NewGuid():N}.dat");
            using (var writer = new BinaryWriter(File.Create(Config.WavelengthFile))) { writer.Write(24UL); writer.Write(380d); writer.Write(780d); }
            using (var writer = new BinaryWriter(File.Create(Config.MaguideFile)))
            { writer.Write(56UL); writer.Write(4f); writer.Write(683); writer.Write(2UL); writer.Write(380d); writer.Write(780d); writer.Write(1d); writer.Write(1d); }
        }
        public void Dispose() { File.Delete(Config.WavelengthFile); File.Delete(Config.MaguideFile); }
    }
}
