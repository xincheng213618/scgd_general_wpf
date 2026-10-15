using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColorVision.Engine.Messages;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;

namespace ColorVision.UI.Tests;

public class LocalCameraMotorTests
{
    internal static ConfigCamera Configuration(string port = "COM791") => new()
    {
        MotorConfig = new MotorConfig { IsUseMotor = true, IsCameraLinkage = true, SzComName = port, MaxPosition = 1000 }
    };

    [Fact]
    public void CameraOwnsConfiguredMotorAndReusesItUntilClose()
    {
        var camera = new LocalCameraSessionTests.FakeNative();
        var motor = new FakeMotor();
        var config = Configuration();
        using var session = new LocalCameraSession(camera, new CameraBackendState(true), config, motorNative: motor);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Equal(new[] { "open", "configure" }, motor.Events);
        Assert.Equal(1000, motor.Settings!.Maximum);
        Assert.Equal(config.MotorConfig.RunSpeed, motor.Settings.Speed);
        Assert.Equal(config.MotorConfig.HomeTimeout, motor.Settings.HomeTimeout);
        Assert.Equal(150, session.UseMotor(m => m.Move(150, true)));
        Assert.Equal(130, session.UseMotor(m => m.Move(-20, false)));
        int moves = motor.Moves;
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UseMotor(m => m.Move(1001, true)));
        Assert.Equal(moves, motor.Moves);
        session.Close(true);
        Assert.Equal(1, motor.Closes);
        Assert.Equal(1, camera.Closes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CameraLinkageControlsOpenFailureAndReconnectionCanRecover(bool linkage)
    {
        var camera = new LocalCameraSessionTests.FakeNative();
        var motor = new FakeMotor { ConfigurationError = true };
        var config = Configuration();
        config.MotorConfig.IsCameraLinkage = linkage;
        using var session = new LocalCameraSession(camera, new CameraBackendState(true), config, motorNative: motor);
        if (linkage) Assert.Throws<InvalidOperationException>(() => session.Open("camera", TakeImageMode.Measure_Normal, 16));
        else session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Equal(!linkage, session.IsOpen);
        Assert.Equal(1, motor.Closes);
        if (!linkage) Assert.Throws<InvalidOperationException>(() => session.UseMotor(m => m.ReadPosition()));
        session.Close(true);
        motor.ConfigurationError = false;
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.True(session.IsOpen);
        Assert.Equal(0, session.UseMotor(m => m.ReadPosition()));
    }

    [Fact]
    public void DisabledMotorNeverOpensAndChangedConnectionRequiresReopen()
    {
        var config = Configuration();
        config.MotorConfig.IsUseMotor = false;
        var motor = new FakeMotor();
        using var session = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), new CameraBackendState(true), config, motorNative: motor);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Empty(motor.Events);
        Assert.Throws<InvalidOperationException>(() => session.UseMotor(m => m.ReadPosition()));
        config.MotorConfig.IsUseMotor = true;
        Assert.Throws<InvalidOperationException>(() => session.UseMotor(m => m.ReadPosition()));
        Assert.Empty(motor.Events);
        session.Close(true);
        session.Open("camera", TakeImageMode.Live, 8);
        Assert.Throws<InvalidOperationException>(() => session.UseMotor(m => m.Move(20, true)));
        Assert.Equal(0, motor.Moves);
    }

    [Fact]
    public void FailedMotorCloseRetainsCameraAndPortUntilRetrySucceeds()
    {
        var camera = new LocalCameraSessionTests.FakeNative();
        var native = new FakeMotor { CloseResult = -10063 };
        var backend = new CameraBackendState(true);
        using var owner = new LocalCameraSession(camera, backend, Configuration(), motorNative: native);
        using var other = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), new CameraBackendState(true), Configuration(), motorNative: new FakeMotor());
        owner.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Throws<InvalidOperationException>(() => owner.Close(true));
        Assert.True(owner.IsOpen);
        Assert.Equal(0, camera.Closes);
        camera.OpenResult = -10009;
        Assert.Equal(-10009, owner.SwitchMode(TakeImageMode.Live, 8));
        Assert.False(owner.IsOpen);
        Assert.True(backend.LocalOwned);
        Assert.Throws<InvalidOperationException>(() => other.Open("other", TakeImageMode.Measure_Normal, 16));
        native.CloseResult = 1;
        owner.Close(true);
        Assert.Equal(1, other.Open("other", TakeImageMode.Measure_Normal, 16));
    }

    [Fact]
    public async Task ClosingDuringFocusWaitsForTheNativeSampleAndPreventsFurtherMotion()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var native = new FakeMotor { OnSample = () => { entered.Set(); release.Wait(); } };
        using var session = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), new CameraBackendState(true), Configuration(), motorNative: native);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        var focus = Task.Run(() => session.AutoFocus(Settings(), 10, 1));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            var close = Task.Run(() => session.Close(true));
            // Synchronize on the cancellation signal rather than dispatcher timing.
            Assert.True(SpinWait.SpinUntil(() => FocusCancellationRequested(session), TimeSpan.FromSeconds(10)));
            Assert.False(close.IsCompleted);
            Assert.Equal(0, native.Closes);
            release.Set();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await focus);
            await close.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, native.Moves);
            Assert.Equal(1, native.Closes);
        }
        finally { release.Set(); }
    }

    private static bool FocusCancellationRequested(LocalCameraSession session)
    {
        var field = typeof(LocalCameraSession).GetField("focusCancellation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (field.GetValue(session) as CancellationTokenSource)?.IsCancellationRequested == true;
    }

    internal static LocalAutoFocusSettings Settings(FindFuncModel method = FindFuncModel.FindMaxDiff) =>
        new(EvaFunc.Tenengrad, method, 0, 1000, 100, 100, 0.5, 1, 0, 10000);

    [Fact]
    public void TemplateSnapshotValidatesTravelAndDoesNotChangeWhenTemplateIsEdited()
    {
        var config = Configuration();
        var template = new AutoFocusParam { MinPosition = 0, MaxPosition = 1000, CurStep = 100, Forwardparam = 100, StopStep = 1 };
        var snapshot = LocalAutoFocusSettings.FromTemplate(template, config.MotorConfig);
        template.MaxPosition = 2000;
        Assert.Equal(1000, snapshot.Maximum);
        Assert.Throws<ArgumentException>(() => LocalAutoFocusSettings.FromTemplate(template, config.MotorConfig));
        template.MaxPosition = 1000;
        template.Curtailparam = 1;
        Assert.Throws<ArgumentException>(() => LocalAutoFocusSettings.FromTemplate(template, config.MotorConfig));
    }

    [Fact]
    public void VidUsesTheSamePiecewiseMappingAsTheService()
    {
        string path = Path.GetTempFileName();
        var config = Configuration();
        config.MotorConfig.VID.IsUseVID = true;
        config.MotorConfig.VID.MappingFileName = path;
        File.WriteAllText(path, "{\"VidNewVision\":{\"positionValue\":500,\"shortSighted\":{\"a\":0,\"b\":0,\"c\":2,\"d\":1},\"longSighted\":{\"a\":0,\"b\":0,\"c\":4,\"d\":2}}}");
        try
        {
            using var session = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), new CameraBackendState(true), config, motorNative: new FakeMotor());
            session.Open("camera", TakeImageMode.Measure_Normal, 16);
            Assert.Equal(251, session.UseMotor(m => m.ConvertVid(500)));
            Assert.Equal(152, session.UseMotor(m => m.ConvertVid(600)));
            File.WriteAllText(path, "{\"a\":0,\"b\":0,\"c\":0,\"d\":0}");
            Assert.Throws<InvalidOperationException>(() => session.UseMotor(m => m.ConvertVid(500)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExistingMotorCommandApiUsesTheLocalSessionWithoutMqtt()
    {
        WpfTestHost.Invoke(() => { });
        var config = Configuration();
        config.Code = "local-motor-test";
        var backend = new CameraBackendState(true);
        var native = new FakeMotor();
        using var session = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), backend, config, motorNative: native);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        device.Config = config;
        device.SysResourceModel = new SysResourceModel { Code = config.Code };
        typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, backend);
        typeof(DeviceCamera).GetField("<LocalCameraSession>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, session);
        var mqtt = (MQTTCamera)RuntimeHelpers.GetUninitializedObject(typeof(MQTTCamera));
        mqtt.Device = device;

        async Task<MsgRecord> Execute(Func<MsgRecord> command)
        {
            var completed = new TaskCompletionSource<MsgRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfTestHost.Invoke(() =>
            {
                MsgRecord record = command();
                record.MsgRecordStateChanged += (_, state) =>
                {
                    if (state is MsgRecordState.Success or MsgRecordState.Fail) completed.TrySetResult(record);
                };
                if (record.MsgRecordState is MsgRecordState.Success or MsgRecordState.Fail) completed.TrySetResult(record);
            });
            return await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(MsgRecordState.Success, (await Execute(() => mqtt.Move(150))).MsgRecordState);
        Assert.Equal(MsgRecordState.Success, (await Execute(mqtt.GetPosition)).MsgRecordState);
        Assert.Equal(MsgRecordState.Success, (await Execute(mqtt.GoHome)).MsgRecordState);
        Assert.Equal(MsgRecordState.Success, (await Execute(() => mqtt.MoveDiaphragm(2.8))).MsgRecordState);
        var failed = await Execute(() => mqtt.Move(2000));
        Assert.Equal(MsgRecordState.Fail, failed.MsgRecordState);
        Assert.Contains("超出电机行程", failed.MsgReturn.Message);
        WpfTestHost.Invoke(() => Assert.Equal(0, config.MotorConfig.Position));

        session.Close(true);
        foreach (Func<MsgRecord> command in new Func<MsgRecord>[] { () => mqtt.Move(150), mqtt.GetPosition, mqtt.GoHome, () => mqtt.MoveDiaphragm(2.8) })
        {
            var closed = await Execute(command);
            Assert.Equal(MsgRecordState.Fail, closed.MsgRecordState);
            Assert.Contains("本地相机尚未打开", closed.MsgReturn.Message);
        }
        Assert.Equal(MsgRecordState.Fail, (await Execute(() => mqtt.AutoFocus(new AutoFocusParam()))).MsgRecordState);
    }

    internal sealed class FakeMotor : ILocalCameraMotorNative
    {
        internal readonly List<string> Events = [];
        internal int Moves, Closes, Position, MoveResult = 1, CloseResult = 1;
        internal bool ConfigurationError;
        internal LocalMotorSettings? Settings;
        internal Action? OnSample;
        public int Open(ref IntPtr handle, LocalMotorSettings settings) { Events.Add("open"); Settings = settings; handle = new IntPtr(700); return 1; }
        public void Configure(IntPtr handle, LocalMotorSettings settings) { Events.Add("configure"); if (ConfigurationError) throw new InvalidOperationException("configuration failed"); }
        public int Close(IntPtr handle) { Closes++; Events.Add("close"); return CloseResult; }
        public int Move(IntPtr handle, int position, int timeout) { Moves++; Position = position; return MoveResult; }
        public int GetPosition(IntPtr handle, out int position, int timeout) { position = Position; return 1; }
        public int Home(IntPtr handle, LocalMotorSettings settings) { Position = 0; return 1; }
        public int Aperture(IntPtr handle, float aperture, int timeout) => 1;
        public void PrepareFocus(IntPtr cameraHandle, float exposure, float gain) { }
        public int EvaluateFocus(IntPtr cameraHandle, EvaFunc evaluation, out double value) { OnSample?.Invoke(); value = 1000000 - Math.Pow(Position - 463, 2); return 1; }
    }
}
