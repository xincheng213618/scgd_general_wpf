using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ServiceManagerReloadTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(8);

    [Fact]
    public async Task SlowOlderReadKeepsDevicesUsableAndCannotReplaceNewerHierarchy()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowOldRead = new ManualResetEventSlim();
        var (manager, original, changes) = WpfTestHost.Invoke(CreateCurrentHierarchy);
        Task<bool> old = WpfTestHost.Invoke(() => manager.ReloadServicesAsync(() =>
        {
            Assert.False(Application.Current.Dispatcher.CheckAccess());
            readStarted.SetResult();
            if (!allowOldRead.Wait(TestTimeout)) throw new TimeoutException("The older read was not released.");
            return Snapshot("Older hierarchy");
        }, () => true));

        try
        {
            await readStarted.Task.WaitAsync(TestTimeout);
            Task<bool> current = WpfTestHost.Invoke(() =>
            {
                Assert.Same(original, Assert.Single(manager.DeviceServices));
                Assert.Equal(0, original.DisposeCount);
                Assert.Empty(changes);
                return manager.ReloadServicesAsync(() => Snapshot("Current hierarchy"), () => true);
            });

            Assert.True(await current.WaitAsync(TestTimeout));
            allowOldRead.Set();
            Assert.False(await old.WaitAsync(TestTimeout));
            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(manager.DeviceServices);
                Assert.Equal(1, original.DisposeCount);
                Assert.Equal("Current hierarchy", Assert.Single(manager.TypeServices).Name);
                Assert.Equal(["Current hierarchy"], changes);
            });
        }
        finally
        {
            allowOldRead.Set();
            await old.WaitAsync(TestTimeout);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedReadOrChangedSourceLeavesExistingHierarchyAndAliasesIntact(bool readFails)
    {
        var (manager, original, changes) = WpfTestHost.Invoke(CreateCurrentHierarchy);
        Task<bool> reload = WpfTestHost.Invoke(() => manager.ReloadServicesAsync(() => readFails
            ? throw new IOException("Synthetic dictionary or resource read failure.")
            : Snapshot("Different database"), () => false));

        Assert.False(await reload.WaitAsync(TestTimeout));
        WpfTestHost.Invoke(() =>
        {
            Assert.Same(manager.DeviceServices, manager.LastGenControl);
            Assert.Same(original, Assert.Single(manager.LastGenControl));
            Assert.Equal(0, original.DisposeCount);
            Assert.Equal("Existing hierarchy", Assert.Single(manager.TypeServices).Name);
            Assert.Empty(changes);
        });
    }

    private static (ServiceManager Manager, TrackingDevice Current, List<string> Changes) CreateCurrentHierarchy()
    {
        var manager = new ServiceManager(Dispatcher.CurrentDispatcher);
        var device = new TrackingDevice { Code = "offline-existing" };
        manager.DeviceServices.Add(device);
        manager.LastGenControl = manager.DeviceServices;
        var dictionary = Snapshot("Existing hierarchy").Dictionary.Single();
        manager.TypeServices.Add(new TypeService { Name = dictionary.Name!, SysDictionaryModel = dictionary });
        List<string> changes = [];
        manager.ServiceChanged += (_, _) => changes.Add(manager.TypeServices.Single().Name);
        return (manager, device, changes);
    }

    private static ServiceManager.ServiceHierarchySnapshot Snapshot(string name) => new(
        [new SysDictionaryModel { Name = name, Value = (int)ServiceTypes.Camera }],
        new ServiceManager.ServiceResourceSnapshot([], [], [], []), 0);

    private sealed class TrackingDevice : DeviceService
    {
        internal int DisposeCount { get; private set; }

        public override CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();

        public override void Dispose() => DisposeCount++;
    }
}
