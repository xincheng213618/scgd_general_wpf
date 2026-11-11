using ColorVision.Engine.Services;
using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using ColorVision.UI;
using ColorVision.UI.Views;
using cvColorVision;
using EngineSettings = ColorVision.Engine.Services.Devices.Spectrum.Configs.GetDataConfig;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    public void LocalOpenChecksExpiryOrSkipsValidationAndReleasesRejectedConnections(int? expirySeconds, bool skipValidation, bool allowed)
    {
        DateTime now = new(2026, 10, 13, 12, 0, 0);
        LicenseModel license = new() { LiceType = 1, ExpiryDate = expirySeconds.HasValue ? now.AddSeconds(expirySeconds.Value) : null, LicenseValue = "not a license payload" };
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native,
            ensureLicenseAvailable: config =>
            {
                Assert.Equal(native.Serial, config.SN);
                LocalSpectrumLicense.EnsureNotExpired(license.ExpiryDate, skipValidation, now);
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
            ensureLicenseAvailable: _ => LocalSpectrumLicense.EnsureNotExpired(expiry, skipValidation: false, now));
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

    [Theory]
    [InlineData(0.0005f, false)]
    [InlineData(2_147_483.75f, false)]
    [InlineData(0.0005f, true)]
    [InlineData(2_147_483.75f, true)]
    public void UnrepresentableCaptureTimeIsRejectedBeforeCallingNative(float time, bool eqe)
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);

        Assert.Contains("积分时间", Assert.Throws<InvalidOperationException>(() =>
            session.Capture(new() { IntegralTime = time, Eqe = eqe, Current = 1, AutoInitDark = true }, files.Config)).Message);
        Assert.Empty(native.Calls);
    }

    [Theory]
    [InlineData(0.0005f)]
    [InlineData(2_147_483.75f)]
    public void UnrepresentableManualDarkTimeIsRejectedBeforeCallingNative(float time)
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);

        Assert.Contains("积分时间", Assert.Throws<InvalidOperationException>(() =>
            session.Dark(new() { IntegralTime = time }, files.Config)).Message);
        Assert.Empty(native.Calls);
    }

    [Theory]
    [InlineData(0.0005f, null)]
    [InlineData(2_147_483.75f, null)]
    [InlineData(0.0005f, 0)]
    [InlineData(2_147_483.75f, 1)]
    public void UnrepresentableAutoExposureResultDoesNotChangeTheRequestOrMoveNd(float time, int? ndPort)
    {
        using var files = new CalibrationFiles();
        if (ndPort.HasValue) ConfigureNd(files.Config, files.Config);
        var wheel = new FakeNd { Port = ndPort ?? 0 };
        var native = new FakeNative { AutoTimes = new([time, 100]) };
        using var session = new LocalSpectrumSession(native, nd: new(_ => wheel));
        session.Open(files.Config);
        var parameters = new LocalSpectrumParameters { IntegralTime = 25, AutoIntegration = true, AutoInitDark = true, IsWithND = ndPort.HasValue };

        Assert.Contains("积分时间", Assert.Throws<InvalidOperationException>(() => session.Capture(parameters, files.Config)).Message);
        Assert.Equal(25, parameters.IntegralTime);
        Assert.Empty(wheel.Moves);
        Assert.Equal(0, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
    }

    [Theory]
    [InlineData(0.001f)]
    [InlineData(2_147_483.5f)]
    public void RepresentableIntegrationTimeBoundariesReachDarkAndCapture(float time)
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);

        session.Capture(new() { IntegralTime = time, AutoInitDark = true }, files.Config);

        Assert.Equal(time, native.DarkTime);
        Assert.Equal((time, 1), native.CaptureTiming);
    }

    [Theory]
    [InlineData(25f, 30f, 2)]
    [InlineData(4f, 10f / 3f, 18)]
    public void SynchronizedCaptureUsesTheSameEffectiveTimingForDarkAndLight(float requestedTime, float effectiveTime, int effectiveAverage)
    {
        using var files = new CalibrationFiles();
        files.Config.GetDataConfig.IsSyncFrequencyEnabled = true;
        files.Config.GetDataConfig.Syncfreq = 100;
        files.Config.GetDataConfig.SyncfreqFactor = 3;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        var parameters = new LocalSpectrumParameters { IntegralTime = requestedTime, NumberOfAverage = 2, AutoInitDark = true };

        var result = session.Capture(parameters, files.Config);

        Assert.Equal(effectiveTime, native.DarkTime);
        Assert.Equal(effectiveAverage, native.DarkAverage);
        Assert.Equal((effectiveTime, effectiveAverage), native.CaptureTiming);
        Assert.Equal(effectiveTime, result.IntegralTime);
        Assert.Equal(effectiveAverage, LocalSpectrumResultService.CreateModel(result, parameters).iAveNum);
        Assert.Equal(requestedTime, parameters.IntegralTime);
        Assert.Equal(2, parameters.NumberOfAverage);
    }

    [Theory]
    [InlineData(true, 10f / 3f, 18, 4f)]
    [InlineData(false, 240f, 2, 240f)]
    public void PublishingLocalResultKeepsSynchronizedRequestTiming(bool synchronized, float actualTime, int actualAverage, float nextRequestTime)
    {
        using var files = new CalibrationFiles();
        files.Config.Code = "TEST-SPECTRUM-TIMING";
        files.Config.GetDataConfig.IsSyncFrequencyEnabled = synchronized;
        files.Config.GetDataConfig.Syncfreq = 100;
        files.Config.GetDataConfig.SyncfreqFactor = 3;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);

        var previous = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            // Use the actual result and request paths without constructing the runtime device services or UI.
            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            device.Config = files.Config;
            var view = new DockViewRegistration(() => throw new InvalidOperationException(), "Spectrum", isVisible: false);
            typeof(DeviceSpectrum).GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(device, view);
            var display = typeof(DeviceSpectrum).GetMethod("DisplayLocalSpectrum", BindingFlags.NonPublic | BindingFlags.Instance)!
                .CreateDelegate<Action<LocalSpectrumCapture, bool>>(device);
            device.DisplayConfig.IntTime = 4;
            device.DisplayConfig.AveNum = 2;
            device.DisplayConfig.IsAutoIntTime = !synchronized;

            var first = session.Capture(device.BuildLocalSpectrumParameters(), files.Config);
            display(first, false);
            var nextRequest = device.BuildLocalSpectrumParameters();
            var second = session.Capture(nextRequest, files.Config);

            Assert.Equal(actualTime, first.IntegralTime);
            Assert.Equal(actualTime, DeviceSpectrum.CreateLocalSpectrumView(first).IntTime);
            Assert.Equal(actualTime, second.IntegralTime);
            Assert.Equal((actualTime, actualAverage), native.CaptureTiming);
            Assert.Equal(nextRequestTime, device.DisplayConfig.IntTime);
        }
        finally { ConfigService.SetInstance(previous); }
    }

    [Fact]
    public void SdkFaultReportsRestartInstructionAndReleasesTheWrapperHandle()
    {
        var native = new FakeNative { InitCode = Spectrometer.SdkFaultErrorCode };
        using var session = new LocalSpectrumSession(native);

        var error = Assert.Throws<InvalidOperationException>(() => session.Open(Config()));

        Assert.Contains("-13025", error.Message);
        Assert.Contains("重启", error.Message);
        Assert.False(session.IsOpen);
        Assert.Equal(1, native.CloseCalls);
        Assert.Equal(1, native.ReleaseCalls);
        Assert.Equal(0, native.CaptureCalls);
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
    public void StopCancelsPendingCommandAndKeepsReservationUntilWorkReturns()
    {
        var backend = new SpectrumBackendState(true);
        backend.BeginLocalCommand();
        CancellationToken first = backend.LocalCancellationToken;
        Task stopped = backend.StopLocalCommandAsync();
        Assert.True(first.IsCancellationRequested);
        Assert.False(stopped.IsCompleted);
        Assert.Throws<InvalidOperationException>(backend.BeginLocalCommand);
        backend.EndLocalCommand();
        Assert.True(stopped.IsCompletedSuccessfully);
        backend.BeginLocalCommand();
        Assert.False(backend.LocalCancellationToken.IsCancellationRequested);
        backend.EndLocalCommand();
    }

    [Fact]
    public async Task StopReachesRunningAutoExposureAndSessionCanBeUsedAgain()
    {
        using var files = new CalibrationFiles();
        using var entered = new ManualResetEventSlim();
        using var finishSample = new ManualResetEventSlim();
        var backend = new SpectrumBackendState(true);
        var native = new FakeNative { OnAutoTime = token => {
            entered.Set();
            token.WaitHandle.WaitOne();
            finishSample.Wait(CancellationToken.None);
        } };
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        var parameters = new LocalSpectrumParameters { AutoIntegration = true, AutoInitDark = true };
        backend.BeginLocalCommand();
        CancellationToken token = backend.LocalCancellationToken;
        Task capture = Task.Run(() => {
            try { session.Capture(parameters, files.Config, token); }
            finally { backend.EndLocalCommand(); }
        });
        Task stopped;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "Automatic exposure did not start");
            stopped = backend.StopLocalCommandAsync();
            Assert.True(token.IsCancellationRequested);
            Assert.False(stopped.IsCompleted);
            Assert.Throws<InvalidOperationException>(backend.BeginLocalCommand);
        }
        finally { _ = backend.StopLocalCommandAsync(); finishSample.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.WaitAsync(TimeSpan.FromSeconds(10)));
        await stopped;
        Assert.Equal(100, parameters.IntegralTime);
        Assert.Equal(0, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
        Assert.True(session.IsOpen);
        native.OnAutoTime = null;
        session.Capture(new() { AutoIntegration = true }, files.Config);
        Assert.Equal(1, native.CaptureCalls);
        Assert.Equal(1, native.CreateCalls);
    }

    [Theory]
    [InlineData(Spectrometer.AutoExposureCancelledErrorCode)]
    [InlineData(Spectrometer.AutoExposureNotConvergedErrorCode)]
    public void AutoExposureFailureDoesNotContinueToDarkOrMeasurement(int code)
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative { AutoTimeCode = code };
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        var parameters = new LocalSpectrumParameters { AutoIntegration = true, AutoInitDark = true };
        if (code == Spectrometer.AutoExposureCancelledErrorCode)
            Assert.Throws<OperationCanceledException>(() => session.Capture(parameters, files.Config));
        else
            Assert.Contains("无法收敛", Assert.Throws<InvalidOperationException>(() => session.Capture(parameters, files.Config)).Message);
        Assert.Equal(100, parameters.IntegralTime);
        Assert.Equal(0, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void CancellationDuringFinalAcquisitionRetainsTheCompletedResult()
    {
        using var files = new CalibrationFiles();
        using var cancellation = new CancellationTokenSource();
        var native = new FakeNative { OnCapture = cancellation.Cancel };
        using var session = new LocalSpectrumSession(native);
        session.Open(files.Config);
        LocalSpectrumCapture result = session.Capture(new(), files.Config, cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(401, result.Data.fPL.Length);
        Assert.Equal(1, native.CaptureCalls);
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
    public void ShutterCaptureBlocksOnlyForDarkAndThenRestoresTransmission(bool eqe, bool useFilterWheel)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        files.Config.ShutterCfg.UseFilterWheel = useFilterWheel;
        var native = new FakeNative();
        var port = new FakeShutterPort(native.Calls);
        var wheel = new FakeFilterWheel(native.Calls, files.Config.ShutterCfg);
        using var session = new LocalSpectrumSession(native, shutter: new(_ => port, _ => wheel, ms =>
        {
            Assert.Equal(files.Config.ShutterCfg.DelayTime, ms);
            native.Calls.Add("shutter-settled");
        }));
        session.Open(files.Config);
        var result = session.Capture(new() { AutoIntegration = true, AutoInitDark = true, Eqe = eqe, Current = 1 }, files.Config);
        Assert.Equal(240, result.IntegralTime);
        string[] expected = ["smoothing", "wave", "magnitude", "auto", "shutter-dark", "shutter-settled", "dark", "shutter-light", "shutter-settled", "capture"];
        Assert.Equal(useFilterWheel ? expected.Where(call => call != "shutter-settled") : expected, native.Calls);
        Assert.Equal(240, native.DarkTime);
        Assert.Equal(useFilterWheel ? 0 : 2, port.DiscardCalls);
        if (useFilterWheel)
        {
            Assert.Equal(new[] { 0, 5 }, wheel.Ports);
            Assert.True(wheel.Disposed);
        }
        session.Close();
        Assert.Equal(!useFilterWheel, port.Disposed);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void SerialShutterDarkAndLightUseConfiguredDirectionAndCommandAcknowledgements(bool openForDark, bool swappedCommands)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        files.Config.ShutterCfg.OpenForDark = openForDark;
        files.Config.ShutterCfg.OpenCmd = swappedCommands ? "b" : "a";
        files.Config.ShutterCfg.CloseCmd = swappedCommands ? "a" : "b";
        var native = new FakeNative();
        var port = new FakeShutterPort(native.Calls);
        using var session = new LocalSpectrumSession(native, shutter: new(_ => port, waitForSettle: _ => { }));
        session.Open(files.Config);

        session.Capture(new() { AutoInitDark = true }, files.Config);

        string block = openForDark ? files.Config.ShutterCfg.OpenCmd : files.Config.ShutterCfg.CloseCmd;
        string light = openForDark ? files.Config.ShutterCfg.CloseCmd : files.Config.ShutterCfg.OpenCmd;
        Assert.Equal(new[] { block, light }, port.Commands);
        Assert.Equal(1, native.DarkCalls);
        Assert.Equal(1, native.CaptureCalls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void CaptureWithoutDarkPreservesManualShutterState(bool actuatorOpen, bool autoIntegration, bool useFilterWheel)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        files.Config.ShutterCfg.UseFilterWheel = useFilterWheel;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native, shutter: new(_ => new FakeShutterPort(native.Calls),
            config => new FakeFilterWheel(native.Calls, config), _ => { }));
        session.Open(files.Config);
        session.ControlShutter(actuatorOpen ? "ShutterDoOpen" : "ShutterDoClose", files.Config);
        Assert.Equal(new[] { actuatorOpen ? "shutter-dark" : "shutter-light" }, native.Calls);
        native.Calls.Clear();

        session.Capture(new() { AutoIntegration = autoIntegration }, files.Config);

        string[] expected = ["smoothing", "wave", "magnitude"];
        Assert.Equal(expected.Concat(autoIntegration ? ["auto", "capture"] : new[] { "capture" }), native.Calls);
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
        bool Confirm(bool transmitting) => transmitting ? !reopenFails : !closeFails;
        var wheel = new FakeFilterWheel(native.Calls, files.Config.ShutterCfg) { Confirm = Confirm };
        var shutter = new LocalSpectrumShutter(_ => new FakeShutterPort(native.Calls)
        {
            Reply = command =>
            {
                bool transmitting = command == "b";
                return [Confirm(transmitting) == transmitting ? "turn off" : "turn on"];
            }
        }, _ => wheel, _ => { });
        using var session = new LocalSpectrumSession(native, shutter: shutter);
        session.Open(files.Config);
        var error = Assert.Throws<InvalidOperationException>(() => session.Capture(new() { AutoInitDark = true }, files.Config));
        string[] expected = ["smoothing", "wave", "magnitude", "shutter-dark"];
        Assert.Equal(expected.Concat(closeFails ? ["shutter-light"] : new[] { "dark", "shutter-light" }), native.Calls);
        Assert.Equal(closeFails ? 0 : 1, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
        if (reopenFails) Assert.Contains(useFilterWheel ? "恢复滤色轮测量位失败" : "恢复光闸通光状态失败", error.Message);
        if (darkCode != 1) Assert.Contains("光谱校零失败", error.Message);
        if (useFilterWheel) Assert.True(wheel.Disposed);
    }

    [Fact]
    public void FailedShutterAcknowledgementPreventsDarkAndCapture()
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native, shutter: new(_ => new FakeShutterPort(native.Calls) { Reply = _ => ["turn off"] }, waitForSettle: _ => { }));
        session.Open(files.Config);
        Assert.Throws<InvalidOperationException>(() => session.Capture(new() { AutoIntegration = true, AutoInitDark = true }, files.Config));
        Assert.Contains("auto", native.Calls);
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
        using var session = new LocalSpectrumSession(native, shutter: new(_ => new FakeShutterPort(native.Calls), config => new FakeFilterWheel(native.Calls, config), _ => { }));
        session.Open(files.Config);
        session.InitAutoDark(files.Config);
        Assert.Equal(new[] { "shutter-dark", "init-adaptive", "shutter-light" }, native.Calls);
        native.Calls.Clear();
        session.Capture(new() { SelfAdaptionInitDark = true }, files.Config);
        Assert.Equal(new[] { "smoothing", "wave", "magnitude", "dark", "capture" }, native.Calls);
        Assert.True(native.LastDarkAdaptive);
    }

    [Fact]
    public void ShutterCommandsReuseConnectionAndReleaseItOnDisconnectAndSpectrumClose()
    {
        var config = Config();
        config.IsShutterEnable = true;
        var native = new FakeNative();
        var ports = new List<FakeShutterPort>();
        var shutter = new LocalSpectrumShutter(_ => { var port = new FakeShutterPort(native.Calls); ports.Add(port); return port; }, waitForSettle: _ => { });
        using var session = new LocalSpectrumSession(native, shutter: shutter);
        session.Open(config);
        session.ControlShutter("ShutterConnect", config);
        session.ControlShutter("ShutterDoOpen", config);
        session.ControlShutter("ShutterDoClose", config);
        Assert.Single(ports);
        Assert.Equal(new[] { "shutter-dark", "shutter-light" }, native.Calls);
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
    public void CloseStillReleasesTheDirectNdWheelWhenShutterDisposalFails()
    {
        var config = Config();
        config.IsShutterEnable = true;
        config.NDConfig.IsNDPort = true;
        config.NDConfig.IsBingNDDevice = false;
        config.NDConfig.SzComName = "COM48125";
        config.NDConfig.BaudRate = 9600;

        var failure = new IOException("shutter disposal failed");
        var shutterPort = new FakeShutterPort([]) { DisposeFailure = failure };
        var shutter = new LocalSpectrumShutter(_ => shutterPort);
        var spectrum = new FakeNative();
        var wheel = new FakeDirectNdNative();
        using var session = new LocalSpectrumSession(spectrum, shutter: shutter, nd: new(directSession: new(wheel)));
        session.Open(config);
        session.ControlShutter("ShutterConnect", config);
        Assert.Equal(1, wheel.OpenCalls);

        Assert.Same(failure, Assert.Throws<IOException>(session.Close));

        Assert.Equal(1, spectrum.CloseCalls);
        Assert.Equal(1, spectrum.ReleaseCalls);
        Assert.Equal(1, wheel.CloseCalls);
        LocalCfwPortSession.EnsureNotLocallyOwned(config.NDConfig.SzComName);
        Assert.False(session.IsOpen);
        Assert.False(session.IsQuarantined);
        session.Open(config);
        Assert.Equal(2, wheel.OpenCalls);
    }

    [Fact]
    public void ShutterTimeoutAndStaleOppositeReplyCannotConfirmCommand()
    {
        var config = new ShutterConfig { DelayTime = 1, OpenCmd = "OPEN", CloseCmd = "CLOSE" };
        var port = new FakeShutterPort([]) { Reply = _ => [] };
        int settleCalls = 0;
        var shutter = new LocalSpectrumShutter(_ => port, waitForSettle: _ => settleCalls++);
        shutter.Connect(config);
        port.Pending.Enqueue("turn on");
        Assert.Throws<TimeoutException>(() => shutter.SetTransmitting(false, config));
        Assert.Equal(1, port.DiscardCalls);
        Assert.True(port.Disposed);

        port = new FakeShutterPort([]) { Reply = _ => ["turn off\r\nturn on"] };
        Assert.Throws<InvalidOperationException>(() => shutter.SetTransmitting(false, config));
        Assert.True(port.Disposed);
        Assert.Equal(0, settleCalls);
    }

    [Fact]
    public void ResetDarkClearsManualAndAdaptiveDataWithoutReconnectingOrChangingCalibration()
    {
        using var files = new CalibrationFiles();
        var native = new FakeNative { DarkValue = 123 };
        using var session = new LocalSpectrumSession(native);
        Assert.Throws<InvalidOperationException>(() => session.ResetDark(files.Config));
        session.Open(files.Config);
        session.Dark(new(), files.Config);
        session.Capture(new(), files.Config);
        Assert.All(native.LastCaptureDark, value => Assert.Equal(123, value));
        session.InitAutoDark(files.Config);
        session.Capture(new() { SelfAdaptionInitDark = true }, files.Config);
        string wave = files.Config.WavelengthFile, magnitude = files.Config.MaguideFile;
        int darkCalls = native.DarkCalls;

        session.ResetDark(files.Config);
        session.Capture(new(), files.Config);
        Assert.All(native.LastCaptureDark, value => Assert.Equal(0, value));
        Assert.Contains("初始化", Assert.Throws<InvalidOperationException>(() => session.Capture(new() { SelfAdaptionInitDark = true }, files.Config)).Message);
        Assert.Equal(darkCalls, native.DarkCalls);
        Assert.True(session.IsOpen);
        Assert.Equal(1, native.CreateCalls);
        Assert.Equal(0, native.CloseCalls);
        Assert.Equal(wave, files.Config.WavelengthFile);
        Assert.Equal(magnitude, files.Config.MaguideFile);

        session.InitAutoDark(files.Config);
        session.Capture(new() { SelfAdaptionInitDark = true }, files.Config);
        Assert.All(native.LastCaptureDark, value => Assert.Equal(123, value));
    }

    [Theory]
    [InlineData(100, 100, 0, 1, "步数")]
    [InlineData(100, 0, 1, 1, "间隔")]
    [InlineData(100, 100, 1, 0, "平均次数")]
    [InlineData(0, 100, 1, 1, "积分时间")]
    [InlineData(2_147_400, 100, 1, 1, "积分时间")]
    [InlineData(100, int.MaxValue, int.MaxValue, 1, "积分时间")]
    public void InvalidAdaptiveDarkSettingsPreserveExistingCalibrationAndLeaveShutterAlone(float start, int step, int count, int average, string message)
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        var native = new FakeNative { DarkValue = 123 };
        using var session = new LocalSpectrumSession(native, shutter: new(_ => new FakeShutterPort(native.Calls), waitForSettle: _ => { }));
        session.Open(files.Config);
        session.InitAutoDark(files.Config);
        native.Calls.Clear();

        var settings = files.Config.SelfAdaptionInitDark;
        settings.BeginIntegralTime = start;
        settings.StepTime = step;
        settings.StepCount = count;
        settings.NumberOfAverage = average;

        var error = Assert.Throws<InvalidOperationException>(() => session.InitAutoDark(files.Config));
        Assert.Contains(message, error.Message);
        Assert.Empty(native.Calls);
        session.Capture(new() { SelfAdaptionInitDark = true }, files.Config);
        Assert.True(native.LastDarkAdaptive);
        Assert.Equal(123, native.LastCaptureDark[0]);
    }

    [Fact]
    public void AdaptiveDarkInitializationUsesTheSettingsValidatedBeforeWaitingForTheShutter()
    {
        using var files = new CalibrationFiles();
        files.Config.IsShutterEnable = true;
        var settings = files.Config.SelfAdaptionInitDark;
        settings.BeginIntegralTime = 120;
        settings.StepTime = 30;
        settings.StepCount = 3;
        settings.NumberOfAverage = 2;

        var native = new FakeNative();
        var shutter = new LocalSpectrumShutter(_ => new FakeShutterPort(native.Calls), waitForSettle: _ =>
        {
            settings.BeginIntegralTime = 0;
            settings.StepTime = 0;
            settings.StepCount = 0;
            settings.NumberOfAverage = 0;
        });
        using var session = new LocalSpectrumSession(native, shutter: shutter);
        session.Open(files.Config);
        session.InitAutoDark(files.Config);

        Assert.Equal(0, settings.StepCount);
        Assert.Equal((120f, 30, 3, 2), native.AdaptiveDarkSettings);
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

    // These cases protect the physical ND/calibration and dark-path boundaries, independently of UI routing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NdAutoIntegrationUsesConfirmedHoleCalibrationAndRestoresThatHoleAfterDark(bool eqe)
    {
        using var first = new CalibrationFiles();
        using var second = new CalibrationFiles();
        ConfigureNd(first.Config, second.Config);
        var wheel = new FakeNd();
        var native = new FakeNative { OnAutoTime = _ => { }, AutoTimes = new([1, 100]) };
        using var session = new LocalSpectrumSession(native, nd: new(_ => wheel));
        session.Open(first.Config);
        var result = session.Capture(new() { IsWithND = true, AutoIntegration = true, AutoInitDark = true, Eqe = eqe, Current = 1 }, first.Config);
        Assert.Equal(1, result.NDPort);
        Assert.Equal(1, LocalSpectrumResultService.CreateModel(result, new()).NDPort);
        Assert.Equal(100, result.IntegralTime);
        Assert.Equal(Path.GetFullPath(second.Config.MaguideFile), native.MagnitudePath);
        Assert.Equal("ND1", result.NdCalibration!.GroupName);
        Assert.Equal(new[] { 1, 4, 1 }, wheel.Moves);
        Assert.Equal(1, wheel.Port);
        Assert.Equal(new[] { "auto", "auto", "dark", "capture" }, native.Calls.Where(call => call is "auto" or "dark" or "capture"));
        Assert.True(wheel.Disposed);
        Assert.Contains(Path.GetFileName(second.Config.MaguideFile), result.AcquisitionParametersJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedNdDarkRestoresLightAndNeverCaptures(bool restoreFails)
    {
        using var files = new CalibrationFiles();
        ConfigureNd(files.Config, files.Config);
        var wheel = new FakeNd { Port = 1, FailRestore = restoreFails };
        var native = new FakeNative { DarkCode = 0 };
        using var session = new LocalSpectrumSession(native, nd: new(_ => wheel));
        session.Open(files.Config);
        var error = Assert.Throws<InvalidOperationException>(() => session.Capture(new() { IsWithND = true, AutoInitDark = true }, files.Config));
        Assert.Equal(new[] { 4, 1 }, wheel.Moves);
        Assert.Equal(0, native.CaptureCalls);
        Assert.Contains(restoreFails ? "恢复 ND" : "光谱校零", error.Message);
        Assert.True(wheel.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NdAutoIntegrationRestoresInitialHoleOnCancellationOrNonconvergence(bool cancel)
    {
        using var files = new CalibrationFiles();
        ConfigureNd(files.Config, files.Config);
        var wheel = new FakeNd();
        using var stopped = new CancellationTokenSource();
        var native = new FakeNative { AutoTimes = new([1, 500]), OnAutoTime = _ => { if (cancel && wheel.Port == 1) stopped.Cancel(); } };
        using var session = new LocalSpectrumSession(native, nd: new(_ => wheel));
        session.Open(files.Config);
        var parameters = new LocalSpectrumParameters { IsWithND = true, AutoIntegration = true, AutoInitDark = true };
        if (cancel) Assert.Throws<OperationCanceledException>(() => session.Capture(parameters, files.Config, stopped.Token));
        else Assert.Contains("收敛", Assert.Throws<InvalidOperationException>(() => session.Capture(parameters, files.Config)).Message);
        Assert.Equal(new[] { 1, 0 }, wheel.Moves);
        Assert.Equal(0, wheel.Port);
        Assert.Equal(0, native.DarkCalls);
        Assert.Equal(0, native.CaptureCalls);
        Assert.Equal(100, parameters.IntegralTime);
        Assert.True(wheel.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NdAutoIntegrationRestoresInitialHoleIfFinalCaptureFails(bool restoreFails)
    {
        using var files = new CalibrationFiles();
        ConfigureNd(files.Config, files.Config);
        files.Config.NDConfig.EnableResetND = true;
        var wheel = new FakeNd { Port = 1 };
        var native = new FakeNative { AutoTimes = new([100]), CaptureFails = true, DarkValue = 40 };
        using var session = new LocalSpectrumSession(native, nd: new(_ => wheel));
        session.Open(files.Config);
        session.Dark(new(), files.Config);
        wheel.Moves.Clear();
        wheel.FailRestore = restoreFails;
        native.DarkValue = 7;

        var error = Assert.Throws<InvalidOperationException>(() => session.Capture(new() { IsWithND = true, AutoIntegration = true, AutoInitDark = true }, files.Config));

        Assert.Equal(new[] { 0, 4, 0, 1 }, wheel.Moves);
        Assert.Equal(restoreFails ? 0 : 1, wheel.Port);
        Assert.Equal(1, native.CaptureCalls);
        Assert.Equal(7, native.LastCaptureDark[0]);
        if (restoreFails)
        {
            Assert.Contains("恢复初始孔位 1 失败", error.Message);
            Assert.Collection(Assert.IsType<AggregateException>(error.InnerException).InnerExceptions,
                capture => Assert.Equal("Native failure", capture.Message), restore => Assert.Equal("restore failed", restore.Message));
        }
        else
        {
            Assert.Equal("Native failure", error.Message);
            native.CaptureFails = false;
            Assert.Equal(1, session.Capture(new(), files.Config).NDPort);
            Assert.Equal(40, native.LastCaptureDark[0]);
        }
    }

    [Fact]
    public void NdMissingCalibrationRejectsCaptureBeforeAnyMovementAndLegacyFilesRemainUsable()
    {
        using var files = new CalibrationFiles();
        ConfigureNd(files.Config, files.Config);
        files.Config.CalibrationGroups.RemoveAt(1);
        var wheel = new FakeNd();
        var native = new FakeNative();
        using var session = new LocalSpectrumSession(native, nd: new(_ => wheel));
        session.Open(files.Config);
        Assert.Throws<InvalidOperationException>(() => session.Capture(new() { IsWithND = true, AutoIntegration = true }, files.Config));
        Assert.Empty(wheel.Moves);
        Assert.Equal(0, native.CaptureCalls);
        files.Config.NDConfig.NDCaliNameGroups = [files.Config.MaguideFile, files.Config.MaguideFile];
        Assert.Equal(Path.GetFullPath(files.Config.MaguideFile), LocalSpectrumNd.ResolveCalibration(files.Config, 1).MagnitudeFile);
    }

    private static void ConfigureNd(ConfigSpectrum first, ConfigSpectrum second)
    {
        first.NDConfig.IsNDPort = true;
        first.NDConfig.NDRate = [0, 8];
        first.NDConfig.NDMinExpTime = 10;
        first.NDConfig.NDMaxExpTime = 300;
        first.NDConfig.DarkNDPort = 4;
        first.CalibrationGroups = [new() { GroupName = "ND0", NDHoleIndex = 0, WavelengthFile = first.WavelengthFile, MaguideFile = first.MaguideFile },
            new() { GroupName = "ND1", NDHoleIndex = 1, WavelengthFile = second.WavelengthFile, MaguideFile = second.MaguideFile }];
    }

    private sealed class FakeNd : ILocalSpectrumNdOperation
    {
        public int Port;
        public bool Disposed, FailRestore;
        public List<int> Moves = [];
        public int GetPort() => Port;
        public int SetPort(int port)
        {
            Moves.Add(port);
            if (FailRestore && Moves.Count > 1 && port == 1) throw new InvalidOperationException("restore failed");
            return Port = port;
        }
        public void Dispose() => Disposed = true;
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
        public int DarkAverage;
        public (float Time, int Average) CaptureTiming;
        public float DarkValue;
        public float[] LastCaptureDark = [];
        public bool LastDarkAdaptive;
        public bool CaptureFails;
        public Action? OnCapture;
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
        public Action<CancellationToken>? OnAutoTime;
        public int AutoTimeCode = 1;
        public Queue<float>? AutoTimes;
        public int AutoTime(IntPtr h, ref float time, ConfigSpectrum c, CancellationToken cancellationToken)
        { Calls.Add("auto"); OnAutoTime?.Invoke(cancellationToken); time = AutoTimes?.Dequeue() ?? 240; return AutoTimeCode; }
        public int Dark(IntPtr h, float time, int avg, int filter, float[] data, bool adaptive)
        { Calls.Add("dark"); DarkCalls++; DarkTime = time; DarkAverage = avg; LastDarkAdaptive = adaptive; Array.Fill(data, DarkValue); return DarkCode; }
        public (float Start, int Step, int Count, int Average) AdaptiveDarkSettings;
        public int InitAutoDark(IntPtr h, SelfAdaptionInitDark c)
        {
            Calls.Add("init-adaptive");
            AdaptiveDarkSettings = (c.BeginIntegralTime, c.StepTime, c.StepCount, c.NumberOfAverage);
            return 1;
        }
        public LocalSpectrumCapture Capture(IntPtr h, LocalSpectrumParameters p, EngineSettings settings, float[] dark)
        {
            CaptureSmoothing = (settings.SmoothingMethod, settings.FilterBW);
            CaptureTiming = (p.IntegralTime, p.NumberOfAverage);
            LastCaptureDark = dark.ToArray();
            Calls.Add("capture"); CaptureCalls++;
            OnCapture?.Invoke();
            if (CaptureFails) throw new InvalidOperationException("Native failure");
            return new() { IntegralTime = p.IntegralTime, Data = NativeData() };
        }
    }

    private sealed class FakeShutterPort(List<string> calls) : ILocalSpectrumShutterPort
    {
        public bool IsOpen { get; private set; }
        public bool Disposed;
        public Exception? DisposeFailure;
        public int DiscardCalls;
        public List<string> Commands = [];
        public Queue<string> Pending = new();
        public Func<string, string[]> Reply = command => command == "a" ? ["TURN ", "ON"] : ["turn ", "off"];
        public void Open() => IsOpen = true;
        public void DiscardInBuffer() { DiscardCalls++; Pending.Clear(); }
        public void Write(string command)
        {
            Commands.Add(command);
            calls.Add(command == "a" ? "shutter-dark" : "shutter-light");
            foreach (string response in Reply(command)) Pending.Enqueue(response);
        }
        public string ReadExisting() => Pending.TryDequeue(out string? value) ? value : string.Empty;
        public void Dispose()
        {
            Disposed = true;
            IsOpen = false;
            if (DisposeFailure is { } failure) throw failure;
        }
    }

    private sealed class FakeDirectNdNative : ILocalCfwPortNative
    {
        internal int OpenCalls { get; private set; }
        internal int CloseCalls { get; private set; }
        public IntPtr Open(LocalCfwPortConnection config) { OpenCalls++; return (IntPtr)456; }
        public int Close(IntPtr handle) { CloseCalls++; return 0; }
        public int SetPort(IntPtr handle, int port) => 0;
        public int GetPort(IntPtr handle, ref int port) { port = 0; return 0; }
    }

    private sealed class FakeFilterWheel(List<string> calls, ShutterConfig config) : ILocalSpectrumFilterWheelOperation
    {
        public bool Disposed;
        public List<int> Ports = [];
        public Func<bool, bool> Confirm = _ => true;
        public void SetTransmitting(bool transmitting)
        {
            Assert.False(Disposed);
            calls.Add(transmitting ? "shutter-light" : "shutter-dark");
            Ports.Add(transmitting ? config.MeasurementPort : config.DarkPort);
            if (!Confirm(transmitting)) throw new InvalidOperationException("滤色轮未确认到位。");
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
