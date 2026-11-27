using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Types;
using ColorVision.Database;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ServiceManagerReloadTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(8);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstCatalogueUsePinsStandaloneSourceButNormalStartupWaitsForDatabaseValidation(bool runDatabaseInitializer)
    {
        var target = new MySqlDatabaseTarget("STATION", 3306, "production");
        MySqlConnectedEndpoint? connected = null;
        var session = new WorkstationConfigurationSession(() => WorkstationConfigurationSelection.Capture(
            WorkstationConfigurationSource.AutomaticAtStartup, target, connected));
        Assert.Equal(WorkstationConfigurationSource.Local, session.Preview.Source);

        if (runDatabaseInitializer) session.BeginStartup();
        Assert.Equal(WorkstationConfigurationSource.Local, session.ForUse.Source);
        connected = Endpoint(target);
        if (runDatabaseInitializer) session.CompleteStartup();
        var pinned = session.ForUse;
        Assert.Equal(runDatabaseInitializer ? WorkstationConfigurationSource.MySql : WorkstationConfigurationSource.Local, pinned.Source);
        connected = null;
        Assert.Same(pinned, session.ForUse);
        Assert.Same(pinned, session.Preview);
    }

    [Theory]
    [InlineData(WorkstationConfigurationSource.AutomaticAtStartup, false, WorkstationConfigurationSource.Local)]
    [InlineData(WorkstationConfigurationSource.AutomaticAtStartup, true, WorkstationConfigurationSource.MySql)]
    [InlineData(WorkstationConfigurationSource.Local, true, WorkstationConfigurationSource.Local)]
    [InlineData(WorkstationConfigurationSource.MySql, false, WorkstationConfigurationSource.MySql)]
    public void ConfigurationSourceRetainsItsStartupChoiceAcrossReconnects(WorkstationConfigurationSource preference,
        bool initiallyConnected, WorkstationConfigurationSource expected)
    {
        var config = new MySqlConfig { Host = "STATION", Database = "production" };
        var target = MySqlDatabaseTarget.From(config);
        var original = Endpoint(target);
        var selection = WorkstationConfigurationSelection.Capture(preference, target, initiallyConnected ? original : null);
        Assert.Equal(expected, selection.Source);
        Assert.Null(selection.Accept(null));
        Assert.Null(selection.Accept(Endpoint(target, false)));
        Assert.Null(selection.Accept(Endpoint(target with { Database = "other" })));
        Assert.Null(selection.Accept(Endpoint(target with { Port = 4406 })));
        config.Host = "station";
        var reconnected = Endpoint(MySqlDatabaseTarget.From(config));
        Assert.Same(expected == WorkstationConfigurationSource.MySql ? reconnected : null, selection.Accept(reconnected));
    }

    [Fact]
    public async Task SameDatabaseReconnectRebindsDeviceWithoutDisposingItOrRevivingItsOldEditor()
    {
        var target = new MySqlDatabaseTarget("STATION", 3306, "production");
        var original = Endpoint(target);
        var manager = WpfTestHost.Invoke(() => new ServiceManager(Dispatcher.CurrentDispatcher));
        Assert.True(await WpfTestHost.Invoke(() => manager.ReloadServicesAsync(
            () => Snapshot("Production") with { Endpoint = original }, () => true)).WaitAsync(TestTimeout));
        WpfTestHost.Invoke(() =>
        {
            var device = new TrackingDevice { SysResourceModel = new SysResourceModel { Id = 12, Code = "Camera" }, ResultDatabaseEndpoint = original };
            manager.DeviceServices.Add(device);
            var editor = device.CaptureConfigurationTarget();
            Assert.True(editor.IsCurrent(device));
            Assert.True(manager.UpdateDirectoryConnectionAsync(null).IsCompletedSuccessfully);
            Assert.True(manager.UpdateDirectoryConnectionAsync(Endpoint(target with { Database = "other" })).IsCompletedSuccessfully);
            Assert.Same(original, device.ResultDatabaseEndpoint);
            var next = Endpoint(target);
            Assert.True(manager.UpdateDirectoryConnectionAsync(next).IsCompletedSuccessfully);
            Assert.Same(device, Assert.Single(manager.DeviceServices));
            Assert.Same(next, device.ResultDatabaseEndpoint);
            Assert.Equal(0, device.DisposeCount);
            Assert.False(editor.IsCurrent(device));
            Assert.True(device.CaptureConfigurationTarget().IsCurrent(device));
        });
    }

    [Fact]
    public void LocalDirectoryKeepsItsLiveDeviceWhenAnUnrelatedDatabaseConnects()
    {
        WpfTestHost.Invoke(() =>
        {
            var (manager, device, changes) = CreateCurrentHierarchy();
            var target = new MySqlDatabaseTarget("STATION", 3306, "production");
            var selection = WorkstationConfigurationSelection.Capture(WorkstationConfigurationSource.Local, target, null);
            Assert.True(manager.UpdateDirectoryConnectionAsync(selection.Accept(Endpoint(target))).IsCompletedSuccessfully);
            Assert.Same(device, Assert.Single(manager.DeviceServices));
            Assert.Equal(0, device.DisposeCount);
            Assert.Empty(changes);
        });
    }

    private static MySqlConnectedEndpoint Endpoint(MySqlDatabaseTarget target, bool current = true) =>
        new(_ => current, () => throw new InvalidOperationException("The directory must not perform a database operation."), target);

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
