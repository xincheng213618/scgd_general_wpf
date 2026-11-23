using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using ColorVision.UI.Authorizations;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class ServiceResourceDeletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmationCannotDeleteFromAReplacementDatabase(bool deleteTerminal) => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        bool confirmed = false;

        bool deleted = deleteTerminal ? fixture.Terminal.DeleteWithConfirmation(SwitchDatabase)
            : fixture.Devices[0].DeleteWithConfirmation(SwitchDatabase);

        Assert.True(confirmed);
        Assert.False(deleted);
        fixture.AssertOriginalHierarchyIntact();
        Assert.Equal(0, fixture.ReplacementOpens);

        bool SwitchDatabase()
        {
            confirmed = true;
            fixture.RetireOriginal();
            fixture.Terminal.ResourceDatabaseEndpoint = fixture.Replacement;
            fixture.Devices[0].ResultDatabaseEndpoint = fixture.Replacement;
            return true;
        }
    });

    [Fact]
    public void RevokedDeletePermissionDuringConfirmationLeavesTheCurrentHierarchyIntact() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();

        Assert.Throws<AccessDeniedException>(() =>
        {
            fixture.Terminal.DeleteWithConfirmation(() =>
            {
                Authorization.Instance.PermissionMode = PermissionMode.User;
                return true;
            });
        });

        Assert.Equal(0, fixture.OriginalOpens);
        Assert.Equal(0, fixture.ReplacementOpens);
        fixture.AssertOriginalHierarchyIntact();
    });

    [Fact]
    public void TerminalDeleteRollsBackEveryChildIfTheFinalParentDeleteFails() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        using (var db = fixture.Original.CreateDbClient())
        {
            db.Ado.ExecuteCommand("""
                CREATE TRIGGER reject_terminal_delete BEFORE DELETE ON t_scgd_sys_resource
                WHEN OLD.pid IS NULL AND OLD.code = 'offline-delete-terminal'
                BEGIN SELECT RAISE(ABORT, 'Synthetic terminal delete failure'); END;
                """);
        }

        var error = Record.Exception(() => { fixture.Terminal.DeleteWithConfirmation(() => true); });
        Assert.NotNull(error);
        Assert.Contains("Synthetic terminal delete failure", error.ToString(), StringComparison.Ordinal);

        fixture.AssertOriginalHierarchyIntact();
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void SuccessfulTerminalDeleteCommitsBeforeDetachingItsDevicesAndTransport() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        List<string[]> rowsAtDisposal = [];
        fixture.Transport.OnDispose = ObserveStoredRows;
        foreach (var device in fixture.Devices) device.OnDispose = ObserveStoredRows;

        Assert.True(fixture.Terminal.DeleteWithConfirmation(() => true));

        Assert.Equal(Fixture.UnrelatedCodes, fixture.ReadCodes("original"));
        Assert.Equal(Fixture.InitialCodes, fixture.ReadCodes("replacement"));
        Assert.Empty(fixture.Parent.VisualChildren);
        Assert.Empty(fixture.Terminal.VisualChildren);
        Assert.Empty(fixture.Manager.TerminalServices);
        Assert.Empty(fixture.Manager.DeviceServices);
        Assert.Null(fixture.Terminal.Parent);
        Assert.All(fixture.Devices, device =>
        {
            Assert.Null(device.Parent);
            Assert.Equal(1, device.DisposeCount);
        });
        Assert.Equal(1, fixture.Transport.DisposeCount);
        Assert.Equal(3, rowsAtDisposal.Count);
        Assert.All(rowsAtDisposal, rows => Assert.Equal(Fixture.UnrelatedCodes, rows));
        Assert.Equal(0, fixture.ReplacementOpens);

        void ObserveStoredRows() => rowsAtDisposal.Add(fixture.ReadCodes("original"));
    });

    [Fact]
    public void LocalTerminalDeletesOnlyFromItsCapturedStore() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var original = new SysResourceDao(fixture.CreateLocalStore("local-original"), () => false);
        var replacement = new SysResourceDao(fixture.CreateLocalStore("local-replacement"), () => false);
        var first = SeedLocal(original);
        var second = SeedLocal(replacement);
        Assert.True(first.Id <= -2);
        Assert.Equal(first.Id, second.Id);
        var transport = (TrackingTransport)RuntimeHelpers.GetUninitializedObject(typeof(TrackingTransport));
        var terminal = new TerminalService(first, transport) { LocalConfigurationResourceDao = original };
        fixture.Parent.AddChild(terminal);
        fixture.Manager.TerminalServices.Add(terminal);
        SysResourceDao previous = SysResourceDao.Instance;
        try
        {
            Assert.True(terminal.DeleteWithConfirmation(() =>
            {
                SysResourceDao.Instance = replacement;
                return true;
            }));

            Assert.Empty(original.GetLocal());
            Assert.Equal(2, replacement.GetLocal().Count);
            Assert.Equal(1, transport.DisposeCount);
            Assert.DoesNotContain(terminal, fixture.Parent.VisualChildren);
            Assert.DoesNotContain(terminal, fixture.Manager.TerminalServices);
        }
        finally { SysResourceDao.Instance = previous; }
    });

    [Fact]
    public void LocalTerminalDeleteRollsBackTheChildWhenTheParentDeleteFails() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        LocalTemplateStore store = fixture.CreateLocalStore("local-rollback");
        var local = new SysResourceDao(store, () => false);
        var root = SeedLocal(local);
        var child = Assert.Single(local.GetAllByPid(root.Id));
        int rootDocumentId = -root.Id - 1, childDocumentId = -child.Id - 1;
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = store.DatabasePath, Pooling = false }.ToString()))
        {
            db.Open();
            using var trigger = db.CreateCommand();
            trigger.CommandText = $"""
                CREATE TRIGGER reject_local_terminal_delete BEFORE DELETE ON local_templates
                WHEN OLD.kind = 'device-resource' AND OLD.id = {rootDocumentId}
                    AND NOT EXISTS (SELECT 1 FROM local_templates WHERE kind = 'device-resource' AND id = {childDocumentId})
                BEGIN SELECT RAISE(ABORT, 'Synthetic local parent delete failure'); END;
                """;
            trigger.ExecuteNonQuery();
        }

        var transport = (TrackingTransport)RuntimeHelpers.GetUninitializedObject(typeof(TrackingTransport));
        var terminal = new TerminalService(root, transport) { LocalConfigurationResourceDao = local };
        fixture.Parent.AddChild(terminal);
        fixture.Manager.TerminalServices.Add(terminal);

        var error = Assert.Throws<SqliteException>(() => { terminal.DeleteWithConfirmation(() => true); });
        Assert.Contains("Synthetic local parent delete failure", error.Message, StringComparison.Ordinal);
        Assert.Equal(root.Code, local.GetById(root.Id)?.Code);
        Assert.Equal(child.Code, local.GetById(child.Id)?.Code);
        Assert.Contains(terminal, fixture.Parent.VisualChildren);
        Assert.Contains(terminal, fixture.Manager.TerminalServices);
        Assert.Equal(0, transport.DisposeCount);
    });

    private static SysResourceModel SeedLocal(SysResourceDao dao)
    {
        var root = new SysResourceModel { Code = "local-terminal", Name = "Terminal", Type = (int)ServiceTypes.Calibration };
        dao.Save(root);
        dao.Save(new SysResourceModel { Code = "local-device", Name = "Device", Type = root.Type, Pid = root.Id });
        return root;
    }

    private static void AsAdministrator(Action action) => WpfTestHost.Invoke(() =>
    {
        Authorization previous = Authorization.Instance;
        try
        {
            Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
            action();
        }
        finally { Authorization.Instance = previous; }
    });

    private sealed class Fixture : IDisposable
    {
        internal static readonly string[] UnrelatedCodes = ["unrelated-child", "unrelated-terminal"];
        internal static readonly string[] InitialCodes = ["offline-delete-device-1", "offline-delete-device-2", "offline-delete-terminal", .. UnrelatedCodes];
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo CopilotRegistrationField = typeof(ServiceManager).GetField("_copilotAgentExtensionRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-service-delete-");
        private readonly ServiceManager? previousManager = ServiceManager.Current;
        private MySqlConnectedEndpoint active;

        internal Fixture()
        {
            Original = new MySqlConnectedEndpoint(IsCurrent, () => { OriginalOpens++; return Open("original"); });
            var replacement = new MySqlConnectedEndpoint(IsCurrent, () => { ReplacementOpens++; return Open("replacement"); });
            active = Original;
            Replacement = replacement;
            try
            {
                var first = Seed("original");
                var other = Seed("replacement");
                Assert.Equal(first.Terminal.Id, other.Terminal.Id);
                Assert.Equal(first.Devices.Select(row => row.Id), other.Devices.Select(row => row.Id));

                Manager = new ServiceManager(Dispatcher.CurrentDispatcher);
                CopilotRegistrationField.SetValue(Manager, this);
                ManagerField.SetValue(null, Manager);
                Parent = new TypeService();
                Transport = (TrackingTransport)RuntimeHelpers.GetUninitializedObject(typeof(TrackingTransport));
                Terminal = new TerminalService(first.Terminal, Transport) { ResourceDatabaseEndpoint = Original };
                Parent.AddChild(Terminal);
                Manager.TerminalServices.Add(Terminal);
                Devices = first.Devices.Select(row => new TrackingDevice(row) { ResultDatabaseEndpoint = Original }).ToArray();
                foreach (var device in Devices)
                {
                    Terminal.AddChild(device);
                    Manager.DeviceServices.Add(device);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal MySqlConnectedEndpoint Original { get; }
        internal MySqlConnectedEndpoint Replacement { get; }
        internal int OriginalOpens { get; private set; }
        internal int ReplacementOpens { get; private set; }
        internal ServiceManager Manager { get; }
        internal TypeService Parent { get; }
        internal TerminalService Terminal { get; }
        internal TrackingTransport Transport { get; }
        internal TrackingDevice[] Devices { get; }
        internal void RetireOriginal() => active = Replacement;
        internal LocalTemplateStore CreateLocalStore(string name) => new(Path.Combine(directory.FullName, name + ".db"));
        private bool IsCurrent(MySqlConnectedEndpoint endpoint) => ReferenceEquals(active, endpoint);

        internal void AssertOriginalHierarchyIntact()
        {
            Assert.Equal(InitialCodes, ReadCodes("original"));
            Assert.Equal(InitialCodes, ReadCodes("replacement"));
            Assert.Same(Terminal, Assert.Single(Parent.VisualChildren));
            Assert.Same(Terminal, Assert.Single(Manager.TerminalServices));
            Assert.Equal(Devices, Terminal.VisualChildren.Cast<TrackingDevice>());
            Assert.Equal(Devices, Manager.DeviceServices.Cast<TrackingDevice>());
            Assert.All(Devices, device => Assert.Equal(0, device.DisposeCount));
            Assert.Equal(0, Transport.DisposeCount);
        }

        internal string[] ReadCodes(string store)
        {
            using var db = Open(store);
            return db.Queryable<SysResourceModel>().Select(row => row.Code).ToList().Select(code => code!)
                .OrderBy(code => code, StringComparer.Ordinal).ToArray();
        }

        private (SysResourceModel Terminal, SysResourceModel[] Devices) Seed(string store)
        {
            using var db = Open(store);
            db.CodeFirst.InitTables<SysResourceModel>();
            var terminal = Insert("offline-delete-terminal");
            var devices = new[] { Insert("offline-delete-device-1", terminal.Id), Insert("offline-delete-device-2", terminal.Id) };
            var other = Insert("unrelated-terminal");
            Insert("unrelated-child", other.Id);
            return (terminal, devices);

            SysResourceModel Insert(string code, int? parentId = null)
            {
                var row = new SysResourceModel { Name = $"{store} {code}", Code = code, Pid = parentId, Type = (int)ServiceTypes.Calibration, Value = "{}" };
                row.Id = db.Insertable(row).ExecuteReturnIdentity();
                return row;
            }
        }

        private SqlSugarClient Open(string store) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(directory.FullName, store + ".db")};Pooling=False", DbType = DbType.Sqlite,
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });

        public void Dispose()
        {
            ManagerField.SetValue(null, previousManager);
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private sealed class TrackingTransport : MQTTServiceTerminalBase
    {
        internal int DisposeCount { get; private set; }
        internal Action? OnDispose { get; set; }

        public override void Dispose()
        {
            DisposeCount++;
            OnDispose?.Invoke();
        }
    }

    private sealed class TrackingDevice(SysResourceModel resource) : DeviceService<DeviceServiceConfig>(resource)
    {
        internal int DisposeCount { get; private set; }
        internal Action? OnDispose { get; set; }

        public override UserControl GetDisplayControl() => new();

        public override void Dispose()
        {
            DisposeCount++;
            OnDispose?.Invoke();
            base.Dispose();
        }
    }
}
