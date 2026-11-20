using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Job;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Job;
using ColorVision.Scheduler;
using Quartz;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.UI.Tests;

public partial class ScheduledDeviceJobHelperTests
{
    public static TheoryData<string?, string?, string?> DeviceSelections => new()
    {
        { "unit-a", "unit-b", "unit-a" }, // The explicit code coincides with the other device's name.
        { null, "primary", "unit-a" },
        { null, "unit-b", "unit-b" },
        { null, "unit-a", null }, // The legacy value matches one code and a different device's name.
        { null, "shared-name", null },
        { "removed-code", "primary", null },
        { "", "primary", null },
    };

    [Theory]
    [MemberData(nameof(DeviceSelections))]
    public Task CameraCaptureJob_DispatchesOnlyToTheResolvedDevice(string? code, string? legacy, string? expectedCode) =>
        VerifyDispatchAsync<DeviceCamera, ConfigCamera>(
            new CameraCaptureJobConfig { DeviceCameraCode = code, DeviceCameraName = legacy },
            (devices, capture) => new CameraCaptureJob(devices, capture), expectedCode);

    [Theory]
    [MemberData(nameof(DeviceSelections))]
    public Task SpectrumGetDataJob_DispatchesOnlyToTheResolvedDevice(string? code, string? legacy, string? expectedCode) =>
        VerifyDispatchAsync<DeviceSpectrum, ConfigSpectrum>(
            new SpectrumGetDataJobConfig { DeviceSpectrumCode = code, DeviceSpectrumName = legacy },
            (devices, capture) => new SpectrumGetDataJob(devices, capture), expectedCode);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeviceEditor_PreservesLegacyBindingUntilTheUserSelectsOrClearsIt(bool spectrum)
    {
        WpfTestHost.Invoke(() =>
        {
            const string styleKey = "ComboBox.Small";
            Application application = Application.Current;
            bool addStyle = application.TryFindResource(styleKey) == null;
            if (addStyle) application.Resources.Add(styleKey, new Style(typeof(ComboBox)));
            try
            {
                Type deviceType = spectrum ? typeof(DeviceSpectrum) : typeof(DeviceCamera);
                DeviceService[] devices = spectrum ? CreateDevices<DeviceSpectrum, ConfigSpectrum>() : CreateDevices<DeviceCamera, ConfigCamera>();
                IJobConfig config = spectrum ? new SpectrumGetDataJobConfig { DeviceSpectrumName = "primary" }
                    : new CameraCaptureJobConfig { DeviceCameraName = "primary" };
                string codePropertyName = spectrum ? nameof(SpectrumGetDataJobConfig.DeviceSpectrumCode) : nameof(CameraCaptureJobConfig.DeviceCameraCode);
                PropertyInfo property = config.GetType().GetProperty(codePropertyName)!;
                var editor = new DeviceNameEditor(type =>
                {
                    Assert.Equal(deviceType, type);
                    return devices;
                });
                IJobConfig ambiguous = spectrum ? new SpectrumGetDataJobConfig { DeviceSpectrumName = "unit-a" }
                    : new CameraCaptureJobConfig { DeviceCameraName = "unit-a" };
                ComboBox unresolved = Assert.Single(editor.GenProperties(property, ambiguous).Children.OfType<ComboBox>());
                Assert.Null(unresolved.SelectedItem);
                Assert.Equal("unit-a", unresolved.Text);
                unresolved.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                Assert.Null(property.GetValue(ambiguous));

                var panel = editor.GenProperties(property, config);
                ComboBox combo = Assert.Single(panel.Children.OfType<ComboBox>());

                Assert.Equal("unit-a", Assert.IsType<string>(combo.SelectedValue));
                Assert.Contains("primary", combo.Text, StringComparison.Ordinal);
                Assert.Contains("unit-a", combo.Text, StringComparison.Ordinal);
                Assert.Null(property.GetValue(config));

                combo.SetCurrentValue(Selector.SelectedValueProperty, "unit-b");

                Assert.Equal("unit-b", Assert.IsType<string>(property.GetValue(config)));

                using var presentation = new HwndSource(new HwndSourceParameters("Scheduled device editor", 32, 32));
                combo.SelectedIndex = -1;
                combo.Text = "offline-device-code";
                combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, presentation, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.Equal("offline-device-code", property.GetValue(config));

                HandyControl.Interactivity.ControlCommands.Clear.Execute(null, combo);

                Assert.Equal(string.Empty, Assert.IsType<string>(property.GetValue(config)));
                Assert.Equal("primary", spectrum ? ((SpectrumGetDataJobConfig)config).DeviceSpectrumName : ((CameraCaptureJobConfig)config).DeviceCameraName);
            }
            finally
            {
                if (addStyle) application.Resources.Remove(styleKey);
            }
        });
    }

    [Fact]
    public void ScheduledDeviceJobs_DisallowSameJobKeyConcurrency()
    {
        Assert.True(typeof(CameraCaptureJob).IsDefined(typeof(DisallowConcurrentExecutionAttribute), true));
        Assert.True(typeof(SpectrumGetDataJob).IsDefined(typeof(DisallowConcurrentExecutionAttribute), true));
    }

    [Fact]
    public void GetTimeout_ZeroMeansNoTimeout()
    {
        var schedulerInfo = new SchedulerInfo { TimeoutSeconds = 0 };

        Assert.Equal(Timeout.InfiniteTimeSpan, ScheduledDeviceJobHelper.GetTimeout(schedulerInfo));
    }

    [Fact]
    public void GetTimeout_PositiveValueUsesSeconds()
    {
        var schedulerInfo = new SchedulerInfo { TimeoutSeconds = 12 };

        Assert.Equal(TimeSpan.FromSeconds(12), ScheduledDeviceJobHelper.GetTimeout(schedulerInfo));
    }

    [Fact]
    public async Task WaitForTerminalStateAsync_TimesOutWithoutTerminalState()
    {
        var msgRecord = new MsgRecord();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                msgRecord,
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None));
    }

    [Fact]
    public async Task WaitForTerminalStateAsync_ObservesCancellation()
    {
        var msgRecord = new MsgRecord();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                msgRecord,
                Timeout.InfiniteTimeSpan,
                cancellation.Token));
    }

    private static async Task VerifyDispatchAsync<TDevice, TConfig>(IJobConfig config,
        Func<Func<IEnumerable<TDevice>>, Func<TDevice, MsgRecord?>, IJob> createJob, string? expectedCode)
        where TDevice : DeviceService<TConfig>
        where TConfig : DeviceServiceConfig, new()
    {
        WpfTestHost.Invoke(() => { });
        TDevice[] devices = CreateDevices<TDevice, TConfig>();
        var dispatched = new List<TDevice>();
        IJob job = createJob(() => devices, device =>
        {
            dispatched.Add(device);
            return new MsgRecord { MsgRecordState = MsgRecordState.Success };
        });
        IJobExecutionContext context = CreateJobContext(job, "capture", config, timeoutSeconds: 2);
        Task execution = job.Execute(context).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        if (expectedCode == null)
        {
            await Assert.ThrowsAsync<JobExecutionException>(() => execution);
            Assert.Empty(dispatched);
        }
        else
        {
            await execution;
            Assert.Same(Assert.Single(devices, device => device.Code == expectedCode), Assert.Single(dispatched));
        }
    }

    private static IJobExecutionContext CreateJobContext(IJob job, string key, IJobConfig config, int timeoutSeconds = 0)
    {
        IJobDetail detail = JobBuilder.Create().OfType(job.GetType()).WithIdentity(key).Build();
        detail.JobDataMap["SchedulerInfo"] = new SchedulerInfo { Config = config, TimeoutSeconds = timeoutSeconds };
        var context = DispatchProxy.Create<IJobExecutionContext, JobContextProxy>();
        ((JobContextProxy)(object)context).Detail = detail;
        return context;
    }

    private static TDevice[] CreateDevices<TDevice, TConfig>()
        where TDevice : DeviceService<TConfig>
        where TConfig : DeviceServiceConfig, new() =>
    [
        CreateDevice<TDevice, TConfig>("unit-b", "unit-a"),
        CreateDevice<TDevice, TConfig>("unit-a", "primary"),
        CreateDevice<TDevice, TConfig>("unit-c", "shared-name"),
        CreateDevice<TDevice, TConfig>("unit-d", "shared-name"),
    ];

    private static TDevice CreateDevice<TDevice, TConfig>(string code, string name)
        where TDevice : DeviceService<TConfig>
        where TConfig : DeviceServiceConfig, new()
    {
        var device = (TDevice)RuntimeHelpers.GetUninitializedObject(typeof(TDevice));
        device.SysResourceModel = new SysResourceModel { Code = code, Name = name };
        device.Config = new TConfig { Code = code, Name = name };
        return device;
    }

    public class JobContextProxy : DispatchProxy
    {
        public IJobDetail Detail { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == "get_JobDetail"
            ? Detail
            : throw new NotSupportedException(targetMethod?.Name);
    }
}
