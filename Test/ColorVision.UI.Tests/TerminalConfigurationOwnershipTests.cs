using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class TerminalConfigurationOwnershipTests
{
    private const string TerminalCode = "offline-terminal-owner";
    private const string ChildCode = "offline-new-device";
    private const string FutureTimestamp = "2026-10-02T12:34:56.1234567+08:00";

    [Fact]
    public void RetiredEditorCannotRenameOrReloadACollidingTerminal() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var terminal = fixture.Terminal;
        TerminalConfigurationTarget openingTarget = terminal.CaptureConfigurationTarget();
        int reloadRequests = 0, notifications = 0;
        terminal.ReloadRequest = (_, _, _) => { reloadRequests++; return true; };
        terminal.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(TerminalService.Name)) notifications++; };
        fixture.SwitchDatabase();
        terminal.ResourceDatabaseEndpoint = fixture.Replacement;

        Assert.ThrowsAny<InvalidOperationException>(() => terminal.Rename("stale editor", openingTarget));
        Assert.False(terminal.TryReloadDevices(openingTarget));
        terminal.ResourceDatabaseEndpoint = fixture.Original;
        terminal.Config.Name = "stale explicit save";
        Assert.ThrowsAny<InvalidOperationException>(terminal.Save);

        Assert.Equal("original", terminal.Name);
        Assert.Equal("stale explicit save", terminal.Config.Name);
        Assert.Equal("original", fixture.ReadTerminal("original").Name);
        Assert.Equal("replacement", fixture.ReadTerminal("replacement").Name);
        Assert.Equal(0, notifications);
        Assert.Equal(0, reloadRequests);
        Assert.Equal(0, fixture.OriginalOpens);
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void ExplicitSaveWritesTheCompleteConfigToItsOwnerWithoutReloadingDevices() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var terminal = fixture.Terminal;
        var replacement = fixture.ReadTerminal("replacement");
        int reloads = 0;
        terminal.ReloadRequest = (_, _, _) => { reloads++; return true; };
        terminal.Config.Code = "offline-edited-code";
        terminal.Config.Name = "edited terminal name";
        terminal.Config.ServiceType = ServiceTypes.Calibration;
        terminal.Config.SendTopic = "updated/command";
        terminal.Config.SubscribeTopic = "updated/status";
        terminal.Config.ServiceToken = "synthetic-configuration-value";
        var expectedJson = JObject.FromObject(terminal.Config);

        terminal.Save();

        var saved = fixture.ReadTerminal("original");
        Assert.Equal(("offline-edited-code", "edited terminal name"), (saved.Code, saved.Name));
        Assert.Equal((saved.Code, saved.Name), (terminal.Code, terminal.Name));
        Assert.True(JToken.DeepEquals(expectedJson, ReadJson(saved.Value!)));
        Assert.Equal("original-metadata", saved.Remark);
        var unchanged = fixture.ReadTerminal("replacement");
        Assert.Equal((replacement.Code, replacement.Name, replacement.Value), (unchanged.Code, unchanged.Name, unchanged.Value));
        Assert.Equal(0, reloads);
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void RenamePreservesStoredJsonAndRefreshReloadsOnlyItsActiveDevicesWithoutSaving() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var terminal = fixture.Terminal;
        fixture.SeedReloadDevices();
        var target = terminal.CaptureConfigurationTarget();
        var original = fixture.ReadTerminal("original");
        var reloads = new List<((string TypeCode, string ServiceCode) Route, string[] Devices)>();
        Func<bool>? dispatchGuard = null;
        terminal.ReloadRequest = (route, devices, canDispatch) =>
        {
            reloads.Add((route, devices.ToArray()));
            dispatchGuard = canDispatch;
            return canDispatch();
        };

        terminal.Rename("renamed", target);

        var renamed = fixture.ReadTerminal("original");
        JObject expected = ReadJson(original.Value!);
        expected["Name"] = "renamed";
        Assert.True(JToken.DeepEquals(expected, ReadJson(renamed.Value!)));
        Assert.Contains(FutureTimestamp, renamed.Value);
        Assert.Equal((original.Id, original.Code, original.Type, original.TenantId, original.Remark),
            (renamed.Id, renamed.Code, renamed.Type, renamed.TenantId, renamed.Remark));
        Assert.Equal("renamed", terminal.Name);
        Assert.Equal("renamed", terminal.Config.Name);
        Assert.Empty(reloads);

        terminal.Config.Name = "unsaved draft";
        Assert.True(terminal.RefreshCommand.CanExecute(null));
        terminal.RefreshCommand.Execute(null);

        var reload = Assert.Single(reloads);
        Assert.Equal(("type-original", TerminalCode), reload.Route);
        Assert.Equal(new[] { "DEV.Calibration.Offline.A", "DEV.Calibration.Offline.B" },
            reload.Devices.OrderBy(code => code, StringComparer.Ordinal));
        Assert.NotNull(dispatchGuard);
        Assert.True(dispatchGuard());
        var afterRefresh = fixture.ReadTerminal("original");
        Assert.Equal((renamed.Name, renamed.Value), (afterRefresh.Name, afterRefresh.Value));
        terminal.Save();
        Assert.Equal("unsaved draft", fixture.ReadTerminal("original").Name);
        Assert.Single(reloads);
        Assert.Equal("replacement", fixture.ReadTerminal("replacement").Name);
        Assert.Equal(0, fixture.ReplacementOpens);

        Assert.True(dispatchGuard());
        fixture.SwitchDatabase();
        Assert.False(dispatchGuard());
    });

    [Fact]
    public void EmptyTerminalDoesNotSubmitADeviceReload() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        int reloads = 0;
        fixture.Terminal.ReloadRequest = (_, _, _) => { reloads++; return true; };

        fixture.Terminal.TryReloadDevices();

        Assert.Equal(0, reloads);
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void ChildCreationRejectsAnOpeningTargetWhenTheOwnerHasBeenRebound() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var target = fixture.Terminal.CaptureConfigurationTarget();
        int configPreparations = 0;
        var factory = CreateChildFactory((_, _) => configPreparations++);
        fixture.SwitchDatabase();
        fixture.Terminal.ResourceDatabaseEndpoint = fixture.Replacement;

        Assert.False(TerminalDeviceCreation.TryPersist(fixture.Terminal, target, factory, ChildCode, "new child", out var saved));

        Assert.Null(saved);
        Assert.Equal(0, configPreparations);
        Assert.Equal(0, fixture.OriginalOpens);
        Assert.Equal(0, fixture.ReplacementOpens);
        Assert.Empty(fixture.ReadChildren("original"));
        Assert.Empty(fixture.ReadChildren("replacement"));
    });

    [Fact]
    public void ChildCreationUsesTheOriginalParentAndRejectsAnActiveDuplicate() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var target = fixture.Terminal.CaptureConfigurationTarget();
        var factory = CreateChildFactory();

        Assert.True(TerminalDeviceCreation.TryPersist(fixture.Terminal, target, factory, ChildCode, "new child", out var saved));

        Assert.NotNull(saved);
        var child = Assert.Single(fixture.ReadChildren("original"));
        Assert.Equal(((int?)fixture.Terminal.SysResourceModel.Id, (int)ServiceTypes.Calibration, ChildCode, "new child", fixture.Terminal.SysResourceModel.TenantId),
            (child.Pid, child.Type, child.Code, child.Name, child.TenantId));
        Assert.Equal(child.Id, saved.Id);
        var config = JsonConvert.DeserializeObject<DeviceServiceConfig>(child.Value!)!;
        Assert.Equal((ChildCode, "new child", "original/command", "original/status"),
            (config.Code, config.Name, config.SendTopic, config.SubscribeTopic));
        Assert.False(TerminalDeviceCreation.TryPersist(fixture.Terminal, target, factory, ChildCode, "duplicate", out _));
        Assert.Equal(child.Id, Assert.Single(fixture.ReadChildren("original")).Id);
        Assert.Empty(fixture.ReadChildren("replacement"));
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Theory]
    [InlineData(null, @"D:\CVTest")]
    [InlineData(@"E:\LocalPreference", @"E:\LocalPreference")]
    public void ChildCreationUsesTheLocalDataPath(string? configuredPath, string expectedPath) => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        fixture.SeedDataDevice("original", @"C:\offline-test\original");
        var terminal = fixture.Terminal;
        Assert.True(DeviceServiceFactoryRegistry.TryGetFactory(ServiceTypes.Calibration, out var factory));
        var configHandler = fixture.CreateLocalConfigHandler();
        if (configuredPath != null)
        {
            Assert.True(configHandler.TrySave(new DeviceDataPathSettings { DataBasePath = configuredPath }, out string error), error);
            configHandler.LoadConfigs();
        }

        var previous = ConfigService.Instance;
        SysResourceModel? saved;
        try
        {
            ConfigService.SetInstance(configHandler);
            Assert.True(TerminalDeviceCreation.TryPersist(terminal, terminal.CaptureConfigurationTarget(), factory!, ChildCode, "new calibration", out saved));
        }
        finally { ConfigService.SetInstance(previous); }

        var children = fixture.ReadChildren("original");
        var child = Assert.Single(children, row => row.Code == ChildCode);
        Assert.Equal(saved!.Id, child.Id);
        Assert.Equal(expectedPath, JsonConvert.DeserializeObject<ConfigCalibration>(child.Value!)!.FileServerCfg.DataBasePath);
        var existing = Assert.Single(children, row => row.Code == "offline-existing-data-device");
        Assert.Equal(@"C:\offline-test\original", JsonConvert.DeserializeObject<ConfigCalibration>(existing.Value!)!.FileServerCfg.DataBasePath);
        Assert.Empty(fixture.ReadChildren("replacement"));
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void ChildCreationRollsBackIfTheOriginalSourceRetiresAfterInsert() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var target = fixture.Terminal.CaptureConfigurationTarget();
        bool observedInsert = false;
        fixture.AfterOriginalSql = sql =>
        {
            if (!sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                || !sql.Contains("t_scgd_sys_resource", StringComparison.OrdinalIgnoreCase)) return;
            observedInsert = true;
            fixture.SwitchDatabase();
        };

        Assert.False(TerminalDeviceCreation.TryPersist(fixture.Terminal, target, CreateChildFactory(), ChildCode, "new child", out var saved));

        Assert.True(observedInsert);
        Assert.Null(saved);
        Assert.Empty(fixture.ReadChildren("original"));
        Assert.Empty(fixture.ReadChildren("replacement"));
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void ChildCreationUsesTheDefaultLicenseFromItsOwningDatabase() => AsAdministrator(() =>
    {
        using var fixture = new Fixture(ServiceTypes.SMU);
        fixture.SeedLicense("original", "offline-camera-a");
        fixture.SeedLicense("replacement", "offline-camera-b");
        var terminal = fixture.Terminal;
        var factory = CreateChildFactory(serviceType: ServiceTypes.SMU);

        Assert.True(TerminalDeviceCreation.TryPersist(terminal, terminal.CaptureConfigurationTarget(), factory, ChildCode, "new SMU", out var saved));

        var child = Assert.Single(fixture.ReadChildren("original"));
        Assert.Equal(child.Id, saved.Id);
        Assert.Equal("offline-camera-a", ReadJson(child.Value!).Value<string>(nameof(DeviceServiceConfig.SN)));
        Assert.Empty(fixture.ReadChildren("replacement"));
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void LocalRenameAndChildCreationUseTheAttachedStoreAfterTheGlobalDaoChanges() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var original = fixture.CreateLocalDao("local-original");
        var replacement = fixture.CreateLocalDao("local-replacement");
        var first = CreateTerminalRow("local-original");
        var second = CreateTerminalRow("local-replacement");
        original.Save(first);
        replacement.Save(second);
        Assert.True(first.Id <= -2);
        Assert.Equal(first.Id, second.Id);
        var terminal = new TerminalService(first, CreateTransport()) { LocalConfigurationResourceDao = original };
        var target = terminal.CaptureConfigurationTarget();
        int reloadRequests = 0;
        terminal.ReloadRequest = (_, _, _) => { reloadRequests++; return true; };
        SysResourceDao previous = SysResourceDao.Instance;
        try
        {
            SysResourceDao.Instance = replacement;
            terminal.Rename("local renamed", target);
            Assert.False(terminal.TryReloadDevices(target));
            Assert.True(TerminalDeviceCreation.TryPersist(terminal, target, CreateChildFactory(), ChildCode, "local child", out var child));
            Assert.NotNull(child);
            Assert.True(child.Id <= -2);
            Assert.Equal(child.Id, Assert.Single(original.GetAllByPid(first.Id)).Id);
            Assert.Equal("local renamed", original.GetById(first.Id)?.Name);
            Assert.Equal("local-replacement", replacement.GetById(second.Id)?.Name);
            Assert.Empty(replacement.GetAllByPid(second.Id));
            Assert.Equal(0, reloadRequests);
        }
        finally { SysResourceDao.Instance = previous; }
    });

    private static DeviceServiceFactory<DeviceServiceConfig> CreateChildFactory(Action<DeviceServiceConfig, DeviceServiceCreateContext>? configure = null,
        ServiceTypes serviceType = ServiceTypes.Calibration) =>
        new(serviceType, _ => throw new InvalidOperationException("Persistence must complete before device activation."), configure);

    private static MQTTServiceTerminalBase CreateTransport() =>
        (OfflineTransport)RuntimeHelpers.GetUninitializedObject(typeof(OfflineTransport));

    private static JObject ReadJson(string json) => JsonConvert.DeserializeObject<JObject>(json,
        new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;

    private static SysResourceModel CreateTerminalRow(string store, ServiceTypes serviceType = ServiceTypes.Calibration) => new()
    {
        Name = store, Code = TerminalCode, Type = (int)serviceType, TenantId = 7, Remark = $"{store}-metadata",
        Value = new JObject
        {
            ["Name"] = store, ["Code"] = TerminalCode, ["ServiceType"] = (int)ServiceTypes.Camera,
            ["SendTopic"] = $"{store}/command", ["SubscribeTopic"] = $"{store}/status",
            ["Future"] = new JObject { ["Timestamp"] = FutureTimestamp, ["Store"] = store }
        }.ToString(Formatting.None)
    };

    private static void AsAdministrator(Action action) => WpfTestHost.Invoke(() =>
    {
        var previous = Authorization.Instance;
        try
        {
            Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
            action();
        }
        finally { Authorization.Instance = previous; }
    });

    private sealed class Fixture : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-terminal-write-");
        private MySqlConnectedEndpoint active;

        internal Fixture(ServiceTypes serviceType = ServiceTypes.Calibration)
        {
            Original = new MySqlConnectedEndpoint(IsCurrent, () =>
            {
                OriginalOpens++;
                var db = Open("original");
                db.Aop.OnLogExecuted = (sql, _) => AfterOriginalSql?.Invoke(sql);
                return db;
            });
            Replacement = new MySqlConnectedEndpoint(IsCurrent, () => { ReplacementOpens++; return Open("replacement"); });
            active = Original;
            try
            {
                var first = Seed("original", serviceType);
                Assert.Equal(first.Id, Seed("replacement", serviceType).Id);
                Terminal = new TerminalService(first, CreateTransport()) { ResourceDatabaseEndpoint = Original };
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal MySqlConnectedEndpoint Original { get; }
        internal MySqlConnectedEndpoint Replacement { get; }
        internal TerminalService Terminal { get; }
        internal int OriginalOpens { get; private set; }
        internal int ReplacementOpens { get; private set; }
        internal Action<string>? AfterOriginalSql { get; set; }
        internal void SwitchDatabase() => active = Replacement;
        private bool IsCurrent(MySqlConnectedEndpoint endpoint) => ReferenceEquals(active, endpoint);
        internal SysResourceDao CreateLocalDao(string store) => new(new LocalTemplateStore(PathFor(store)), () => false);
        internal ConfigHandler CreateLocalConfigHandler() => new() { ConfigFilePath = Path.Combine(directory.FullName, "app-config.json") };

        internal SysResourceModel ReadTerminal(string store)
        {
            using var db = Open(store);
            return db.Queryable<SysResourceModel>().Where(row => row.Id == Terminal.SysResourceModel.Id).Single();
        }

        internal List<SysResourceModel> ReadChildren(string store)
        {
            using var db = Open(store);
            return db.Queryable<SysResourceModel>().Where(row => row.Pid == Terminal.SysResourceModel.Id).ToList();
        }

        internal void SeedDataDevice(string store, string dataPath)
        {
            using var db = Open(store);
            var owner = Terminal.SysResourceModel;
            var config = new ConfigCalibration();
            config.FileServerCfg.DataBasePath = dataPath;
            db.Insertable(new SysResourceModel
            {
                Code = "offline-existing-data-device", Name = "existing data device", Pid = owner.Id,
                Type = owner.Type, TenantId = owner.TenantId, Value = JsonConvert.SerializeObject(config)
            }).ExecuteCommand();
        }

        internal void SeedReloadDevices()
        {
            using var db = Open("original");
            var owner = Terminal.SysResourceModel;
            var unrelated = CreateTerminalRow("unrelated");
            unrelated.Code = "offline-other-terminal";
            int unrelatedId = db.Insertable(unrelated).ExecuteReturnIdentity();
            var activeA = Device("DEV.Calibration.Offline.A", owner.Id);
            var activeB = Device("DEV.Calibration.Offline.B", owner.Id);
            var disabled = Device("DEV.Calibration.Disabled", owner.Id);
            disabled.IsEnable = false;
            var removed = Device("DEV.Calibration.Removed", owner.Id);
            removed.IsDelete = true;
            db.Insertable(new[] { activeA, activeB, disabled, removed, Device("DEV.Calibration.Foreign", unrelatedId) }).ExecuteCommand();

            SysResourceModel Device(string code, int parentId) => new()
            {
                Code = code, Name = code, Pid = parentId, Type = owner.Type, TenantId = owner.TenantId, Value = "{}"
            };
        }

        internal void SeedLicense(string store, string serial)
        {
            using var db = Open(store);
            db.CodeFirst.InitTables<LicenseModel>();
            db.Insertable(new LicenseModel
            {
                MacAddress = serial, LiceType = 0, DevCameraId = 0, DevCaliId = 0, Model = "OFFLINE-TEST", CusTomerName = "Offline test",
                LicenseValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"device_mode":"OFFLINE-TEST","expiry_date":"2100000000"}"""))
            }).ExecuteCommand();
        }

        private SysResourceModel Seed(string store, ServiceTypes serviceType)
        {
            using var db = Open(store);
            db.CodeFirst.InitTables<SysResourceModel>();
            db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_dictionary (id INTEGER PRIMARY KEY, [key] TEXT, pid INTEGER, val INTEGER)");
            db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_dictionary ([key], pid, val) VALUES (@route, 1, @type)",
                new { route = $"type-{store}", type = (int)serviceType });
            var row = CreateTerminalRow(store, serviceType);
            row.Id = db.Insertable(row).ExecuteReturnIdentity();
            return row;
        }

        private string PathFor(string store) => Path.Combine(directory.FullName, store + ".db");

        private SqlSugarClient Open(string store) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={PathFor(store)};Pooling=False", DbType = DbType.Sqlite,
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });

        public void Dispose()
        {
            Terminal?.RetireConfiguration();
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private sealed class OfflineTransport : MQTTServiceTerminalBase
    {
        public override void Dispose() { }
    }
}
