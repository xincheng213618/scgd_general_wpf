using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using cvColorVision;
using System.IO;
using System.Text;

namespace Spectrum.Tests;

[CollectionDefinition("LocalSpectrumDriver", DisableParallelization = true)]
public sealed class LocalSpectrumDriverCollection;

[Collection("LocalSpectrumDriver")]
public sealed class LocalSpectrumTests
{
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
        Assert.Equal(new[] { "wave", "magnitude", "auto", "dark", "capture" }, native.Calls);
        Assert.Equal(401, result.Data.fPL.Length);
        Assert.True(session.IsOpen);
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
    public void UnsupportedNdIsRejectedBeforeNativeCapture()
    {
        using var session = new LocalSpectrumSession(new FakeNative());
        session.Open(Config());
        Assert.Throws<NotSupportedException>(() => session.Capture(new() { IsWithND = true }, Config()));
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
    private static COLOR_PARA NativeData() => new() { fSpect1 = 380, fSpect2 = 780, fInterval = 1, fPL = Enumerable.Repeat(1f, 401).ToArray(), fRi = new float[15], fPh = 12, fCIEx = 1, fCIEy = 2, fCIEz = 3 };

    private sealed class FakeNative : ILocalSpectrumNative
    {
        public int InitCode = 1, CreateCalls, CloseCalls, ReleaseCalls, CaptureCalls, DarkCalls;
        public bool CaptureFails;
        public string Serial = "TEST-SPECTRUM";
        public List<string> Calls = [];
        public IntPtr Create(int type, Spectrometer.Emission_CallBack callback) { CreateCalls++; return (IntPtr)123; }
        public int Init(IntPtr h, int port, int baud) => InitCode;
        public int SerialNumber(IntPtr h, StringBuilder serial) { serial.Append(Serial); return 1; }
        public int Close(IntPtr h) { CloseCalls++; return 1; }
        public int Release(IntPtr h) { ReleaseCalls++; return 1; }
        public int LoadWavelength(IntPtr h, string path) { Calls.Add("wave"); return 1; }
        public int LoadMagnitude(IntPtr h, string path) { Calls.Add("magnitude"); return 1; }
        public int Configure(IntPtr h, ColorVision.Engine.Services.Devices.Spectrum.Configs.SetEmissionSP100Config c) => 1;
        public int AutoTime(IntPtr h, ref float time, ConfigSpectrum c) { Calls.Add("auto"); time = 240; return 1; }
        public int Dark(IntPtr h, float time, int avg, int filter, float[] data, bool adaptive) { Calls.Add("dark"); DarkCalls++; return 1; }
        public int InitAutoDark(IntPtr h, SelfAdaptionInitDark c) => 1;
        public LocalSpectrumCapture Capture(IntPtr h, LocalSpectrumParameters p, ConfigSpectrum c, float[] dark)
        {
            Calls.Add("capture"); CaptureCalls++;
            if (CaptureFails) throw new InvalidOperationException("Native failure");
            return new() { IntegralTime = p.IntegralTime, Data = NativeData() };
        }
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
