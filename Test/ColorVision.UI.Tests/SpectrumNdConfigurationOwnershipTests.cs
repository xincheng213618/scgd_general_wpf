using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Calibration;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SpectrumNdConfigurationOwnershipTests
{
    private static readonly LocalSpectrumNd.Calibration Selection = new(2, "nd2-wave.dat", "nd2-magnitude.dat", "ND2");

    [Theory]
    [InlineData("current")]
    [InlineData("retired")]
    [InlineData("code")]
    [InlineData("type")]
    [InlineData("tenant")]
    public void ConfirmedNdUpdatesOnlyTheCapturedMatchingResource(string condition)
    {
        WithDatabases((firstPath, secondPath) =>
        {
            using var a = Open(firstPath);
            using var b = Open(secondPath);
            string code = $"ND.Owner.{Guid.NewGuid():N}";
            SysResourceModel first = Seed(a, code, "a", condition);
            SysResourceModel second = Seed(b, code, "b", "current");
            Assert.Equal(first.Id, second.Id);

            MySqlConnectedEndpoint? current = null;
            int firstOpens = 0, secondOpens = 0;
            var original = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => { firstOpens++; return Open(firstPath); });
            var replacement = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => { secondOpens++; return Open(secondPath); });
            current = original;

            WpfTestHost.Invoke(() =>
            {
                var resource = Resource(code, "a");
                resource.Id = first.Id;
                resource.Pid = first.Pid;
                var owner = CreateSpectrum(resource);
                try
                {
                    owner.ResultDatabaseEndpoint = original;
                    DeviceConfigurationTarget target = owner.CaptureConfigurationTarget();
                    if (condition == "retired")
                    {
                        current = replacement;
                        owner.ResultDatabaseEndpoint = replacement;
                    }

                    bool accepted = condition == "current";
                    Assert.Equal(accepted, owner.ApplyLocalNdState(2, Selection, target));
                    Assert.Equal(accepted ? 2 : 1, owner.DisplayConfig.PortNum);
                    Assert.Equal(accepted ? "ND2" : "ND1", owner.Config.ActiveCalibrationGroupName);
                    Assert.Equal(accepted ? "nd2-wave.dat" : "nd1-wave.dat", owner.Config.WavelengthFile);
                    if (accepted)
                    {
                        int opens = firstOpens;
                        Assert.True(owner.ApplyLocalNdState(2, Selection, target));
                        Assert.Equal(opens, firstOpens);
                    }
                }
                finally { DisplayConfigManager.Instance.Configs.TryRemove(code, out _); }
            });

            SysResourceModel firstAfter = a.Queryable<SysResourceModel>().InSingle(first.Id);
            Assert.Equal(first.Name, firstAfter.Name);
            Assert.Equal(first.Remark, firstAfter.Remark);
            if (condition == "current") AssertStoredSelection(firstAfter.Value!, "a");
            else Assert.Equal(first.Value, firstAfter.Value);
            Assert.Equal(second.Value, b.Queryable<SysResourceModel>().InSingle(second.Id).Value);
            Assert.Equal(0, secondOpens);
            if (condition == "retired") Assert.Equal(0, firstOpens);
        });
    }

    [Fact]
    public void NegativeIdKeepsTheOriginalLocalStoreAndDeletedResourceIsNeverRecreated()
    {
        WithDatabases((firstPath, secondPath) =>
        {
            bool connected = false;
            var original = new SysResourceDao(new LocalTemplateStore(firstPath), () => connected);
            var replacement = new SysResourceDao(new LocalTemplateStore(secondPath), () => false);
            string code = $"ND.Local.Owner.{Guid.NewGuid():N}";
            SysResourceModel first = Resource(code, "a"), second = Resource(code, "b");
            original.Save(first);
            replacement.Save(second);
            Assert.Equal(first.Id, second.Id);
            Assert.True(first.Id <= -2);

            WpfTestHost.Invoke(() =>
            {
                var owner = CreateSpectrum(first);
                SysResourceDao previous = SysResourceDao.Instance;
                try
                {
                    owner.LocalConfigurationResourceDao = original;
                    var target = owner.CaptureConfigurationTarget();
                    SysResourceDao.Instance = replacement;
                    connected = true;

                    Assert.True(owner.ApplyLocalNdState(2, Selection with { GroupName = null }, target));
                    Assert.Equal(2, owner.DisplayConfig.PortNum);
                    Assert.Equal("ND1", owner.Config.ActiveCalibrationGroupName);
                    string saved = original.GetById(first.Id)!.Value!;
                    JObject json = JObject.Parse(saved);
                    Assert.Equal("ND1", (string?)json["ActiveCalibrationGroupName"]);
                    Assert.Equal("nd2-wave.dat", (string?)json["CalibrationGroups"]![0]!["WavelengthFile"]);
                    Assert.Equal("a", (string?)json["UnknownField"]);

                    original.DeleteById(first.Id);
                    Assert.False(owner.ApplyLocalNdState(1, new LocalSpectrumNd.Calibration(1, "nd1-wave.dat", "nd1-magnitude.dat", "ND1"), target));
                    Assert.Equal(2, owner.DisplayConfig.PortNum);
                    Assert.Equal("nd2-wave.dat", owner.Config.WavelengthFile);
                    Assert.Null(original.GetById(first.Id));
                    Assert.Equal(second.Value, replacement.GetById(first.Id)!.Value);
                }
                finally
                {
                    SysResourceDao.Instance = previous;
                    DisplayConfigManager.Instance.Configs.TryRemove(code, out _);
                }
            });
        });
    }

    [Fact]
    public void RestartRouteUsesTheCapturedDictionaryAndParentAndRejectsRetirement()
    {
        WithDatabases((firstPath, secondPath) =>
        {
            using var a = Open(firstPath);
            using var b = Open(secondPath);
            string code = $"ND.Restart.{Guid.NewGuid():N}";
            SysResourceModel row = Seed(a, code, "a", "current");
            Seed(b, code, "b", "current");
            MySqlConnectedEndpoint? current = null;
            var original = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => Open(firstPath));
            var replacement = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => Open(secondPath));
            current = original;
            WpfTestHost.Invoke(() =>
            {
                var owner = CreateSpectrum(row);
                try
                {
                    owner.ResultDatabaseEndpoint = original;
                    var target = owner.CaptureConfigurationTarget();
                    Assert.True(target.TryResolveRestart(owner, out var route));
                    Assert.Equal(("type-a", "parent-a", code), route);
                    current = replacement;
                    owner.ResultDatabaseEndpoint = replacement;
                    Assert.False(target.TryResolveRestart(owner, out _));
                }
                finally { DisplayConfigManager.Instance.Configs.TryRemove(code, out _); }
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalibrationWindowSavePersistsAnUnchangedSelectionOnlyForItsOpeningTarget(bool retire)
    {
        WithDatabases((firstPath, secondPath) =>
        {
            using var a = Open(firstPath);
            using var b = Open(secondPath);
            string code = $"ND.Window.{Guid.NewGuid():N}";
            SysResourceModel first = Seed(a, code, "a", "current");
            SysResourceModel second = Seed(b, code, "b", "current");
            MySqlConnectedEndpoint? current = null;
            var original = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => Open(firstPath));
            var replacement = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => Open(secondPath));
            current = original;
            WpfTestHost.Invoke(() =>
            {
                var owner = CreateSpectrum(first);
                try
                {
                    owner.ResultDatabaseEndpoint = original;
                    DeviceConfigurationTarget capturedOnOpen = owner.CaptureConfigurationTarget();
                    owner.Config.NDConfig.NDBindDeviceCode = "changed-wheel";
                    int notifications = 0;
                    owner.ConfigChanged += (_, _) => notifications++;
                    if (retire)
                    {
                        current = replacement;
                        owner.ResultDatabaseEndpoint = replacement;
                    }

                    Assert.Equal(!retire, owner.TrySaveCalibrationGroupConfiguration(owner.Config.ActiveCalibrationGroup, true, capturedOnOpen));
                    Assert.Equal(retire ? 0 : 1, notifications);
                }
                finally { DisplayConfigManager.Instance.Configs.TryRemove(code, out _); }
            });

            string actual = a.Queryable<SysResourceModel>().InSingle(first.Id).Value!;
            if (retire) Assert.DoesNotContain("changed-wheel", actual);
            else Assert.Equal("changed-wheel", (string?)JObject.Parse(actual)["NDConfig"]!["NDBindDeviceCode"]);
            Assert.Equal(second.Value, b.Queryable<SysResourceModel>().InSingle(second.Id).Value);
        });
    }

    private static void AssertStoredSelection(string value, string store)
    {
        var json = JObject.Parse(value);
        Assert.Equal("ND2", (string?)json["ActiveCalibrationGroupName"]);
        Assert.Equal("nd2-wave.dat", (string?)json["WavelengthFile"]);
        Assert.Equal("nd2-magnitude.dat", (string?)json["MaguideFile"]);
        Assert.Equal("nd1-wave.dat", (string?)json["CalibrationGroups"]![0]!["WavelengthFile"]);
        Assert.Equal("nd2-wave.dat", (string?)json["CalibrationGroups"]![1]!["WavelengthFile"]);
        Assert.Equal(store, (string?)json["CalibrationGroups"]![1]!["UnknownGroupField"]);
        Assert.Equal(store, (string?)json["UnknownField"]);
        Assert.Contains("2026-10-02T12:34:56.1234567+08:00", value);
    }

    private static DeviceSpectrum CreateSpectrum(SysResourceModel resource)
    {
        var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
        device.SysResourceModel = resource;
        device.Config = new ConfigSpectrum
        {
            Code = resource.Code!, ActiveCalibrationGroupName = "ND1", WavelengthFile = "nd1-wave.dat", MaguideFile = "nd1-magnitude.dat",
            CalibrationGroups = [new() { GroupName = "ND1", NDHoleIndex = 1, WavelengthFile = "nd1-wave.dat", MaguideFile = "nd1-magnitude.dat" },
                new() { GroupName = "ND2", NDHoleIndex = 2, WavelengthFile = "pending-wave.dat", MaguideFile = "pending-magnitude.dat" }]
        };
        SetBackingField(device, nameof(DeviceSpectrum.CalibrationGroupChangeGuard), new SpectrumCalibrationGroupChangeGuard());
        SetBackingField(device, nameof(DeviceSpectrum.SpectrumBackend), new SpectrumBackendState(true));
        device.DisplayConfig.PortNum = 1;
        return device;
    }

    private static void SetBackingField(DeviceSpectrum device, string name, object value) =>
        typeof(DeviceSpectrum).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, value);

    private static SysResourceModel Seed(SqlSugarClient db, string code, string store, string condition)
    {
        db.CodeFirst.InitTables<SysResourceModel>();
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_dictionary (id INTEGER PRIMARY KEY, [key] TEXT, pid INTEGER, val INTEGER)");
        db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_dictionary ([key], pid, val) VALUES (@route, 1, @type)",
            new { route = "type-" + store, type = (int)ServiceTypes.Spectrum });
        int parentId = db.Insertable(new SysResourceModel { Code = "parent-" + store, Name = "Parent" }).ExecuteReturnIdentity();
        SysResourceModel row = Resource(code, store);
        row.Pid = parentId;
        if (condition == "code") row.Code += "-different";
        if (condition == "type") row.Type++;
        if (condition == "tenant") row.TenantId++;
        row.Id = db.Insertable(row).ExecuteReturnIdentity();
        return row;
    }

    private static SysResourceModel Resource(string code, string store) => new()
    {
        Code = code, Type = (int)ServiceTypes.Spectrum, TenantId = 7, Name = "Spectrum", Remark = "keep remark",
        Value = $$"""
            {"ActiveCalibrationGroupName":"ND1","WavelengthFile":"nd1-wave.dat","MaguideFile":"nd1-magnitude.dat",
             "CalibrationGroups":[{"GroupName":"ND1","WavelengthFile":"nd1-wave.dat","MaguideFile":"nd1-magnitude.dat"},
             {"GroupName":"ND2","WavelengthFile":"pending-wave.dat","MaguideFile":"pending-magnitude.dat","UnknownGroupField":"{{store}}"}],
             "UnknownField":"{{store}}","Timestamp":"2026-10-02T12:34:56.1234567+08:00"}
            """
    };

    private static SqlSugarClient Open(string path) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true
    });

    private static void WithDatabases(Action<string, string> action)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-nd-config-owner-");
        try { action(Path.Combine(directory.FullName, "a.db"), Path.Combine(directory.FullName, "b.db")); }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
