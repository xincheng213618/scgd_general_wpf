using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class DeviceDataPathConfigurationTests
{
    [Fact]
    public void SavesTheLocalPathWithoutAnyConfiguredDevices() => WithStorage(directory =>
    {
        var configHandler = OpenConfig(directory);
        var configuration = new DeviceDataPathConfiguration(configHandler, () => []);
        Assert.Equal(@"D:\CVTest", configuration.InitialPath);

        var result = configuration.Apply(@"E:\Shared");

        Assert.Equal(0, result.UpdatedDevices);
        Assert.Equal(0, result.ReloadRequestedDevices);
        Assert.Equal(@"E:\Shared", configuration.InitialPath);
        var reopened = OpenConfig(directory);
        reopened.LoadConfigs();
        Assert.Equal(@"E:\Shared", reopened.GetRequiredService<DeviceDataPathSettings>().DataBasePath);
    });

    [Fact]
    public void SavingTheLocalPathWithoutDevicesRequiresPermission() => WithStorage(directory =>
    {
        Authorization.Instance.PermissionMode = PermissionMode.User;
        var configHandler = OpenConfig(directory);
        var configuration = new DeviceDataPathConfiguration(configHandler, () => []);

        Assert.Throws<UnauthorizedAccessException>(() => configuration.Apply(@"E:\Shared"));

        Assert.Equal(@"D:\CVTest", configuration.InitialPath);
        Assert.False(File.Exists(configHandler.ConfigFilePath));
    });

    [Fact]
    public void LocalSaveNormalizesMixedPathsAndPreservesOtherJson() => WithStorage(directory =>
    {
        var dao = new SysResourceDao(new LocalTemplateStore(Path.Combine(directory, "local.db")), () => false);
        const string cameraJson = """{"fileServerCfg":{"dataBasePath":"D:\\Camera","customOption":{"enabled":true},"PortRange":"6600-6605"},"foreignProperty":[1,2]}""";
        const string algorithmJson = """{"FileServerCfg":{"DataBasePath":"E:\\Algorithm","Endpoint":"machine"}}""";
        using var camera = LocalDevice<ConfigCamera>(dao, ServiceTypes.Camera, "camera", cameraJson);
        using var algorithm = LocalDevice<ConfigAlgorithm>(dao, ServiceTypes.Algorithm, "algorithm", algorithmJson);
        int reloads = 0;
        var configuration = new DeviceDataPathConfiguration(OpenConfig(directory), () => [camera, algorithm], (_, _, _, _) =>
        {
            reloads++;
            return true;
        });

        Assert.Equal(@"D:\CVTest", configuration.InitialPath);
        var result = configuration.Apply("  D:\\Capture\\..\\Unified\\  ");

        Assert.Equal(2, result.UpdatedDevices);
        Assert.Equal(0, result.ReloadRequestedDevices);
        Assert.Empty(result.ReloadPendingDevices);
        Assert.Empty(result.CameraReconnectDevices);
        Assert.Equal(0, reloads);
        Assert.Equal(@"D:\Unified", camera.Config.FileServerCfg.DataBasePath);
        Assert.Equal(@"D:\Unified", algorithm.Config.FileServerCfg.DataBasePath);
        var cameraValue = JObject.Parse(dao.GetById(camera.SysResourceModel.Id)!.Value!);
        var cameraSettings = Assert.IsType<JObject>(cameraValue["fileServerCfg"]);
        Assert.Equal(@"D:\Unified", (string?)cameraSettings["dataBasePath"]);
        Assert.Equal(true, (bool?)cameraSettings["customOption"]?["enabled"]);
        Assert.Equal("6600-6605", (string?)cameraSettings["PortRange"]);
        Assert.Equal(new[] { 1, 2 }, cameraValue["foreignProperty"]!.Values<int>());
        var algorithmValue = JObject.Parse(dao.GetById(algorithm.SysResourceModel.Id)!.Value!);
        Assert.Equal(@"D:\Unified", ReadPath(algorithmValue));
        Assert.Equal("machine", (string?)algorithmValue["FileServerCfg"]?["Endpoint"]);
        Assert.Equal(0, configuration.Apply(@"D:\Unified").UpdatedDevices);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalSaveRollsBackAnEarlierRowWhenTheSecondRowIsMissingOrStale(bool deleted) => WithStorage(directory =>
    {
        var dao = new SysResourceDao(new LocalTemplateStore(Path.Combine(directory, "local.db")), () => false);
        const string original = """{"FileServerCfg":{"DataBasePath":"D:\\Old"},"unchanged":true}""";
        // Local IDs are negative. The newer row sorts first in the save transaction.
        using var secondRow = LocalDevice<ConfigCamera>(dao, ServiceTypes.Camera, "second-row", original);
        using var firstRow = LocalDevice<ConfigAlgorithm>(dao, ServiceTypes.Algorithm, "first-row", original);
        var configuration = new DeviceDataPathConfiguration(OpenConfig(directory), () => [secondRow, firstRow]);

        if (deleted) dao.DeleteById(secondRow.SysResourceModel.Id);
        else
        {
            var replacement = dao.GetById(secondRow.SysResourceModel.Id)!;
            replacement.Code = "replaced-code";
            dao.Save(replacement);
        }

        Assert.Throws<InvalidOperationException>(() => configuration.Apply(@"D:\New"));
        Assert.Equal(original, dao.GetById(firstRow.SysResourceModel.Id)!.Value);
        Assert.Equal(original, firstRow.SysResourceModel.Value);
        Assert.Equal(@"D:\Old", firstRow.Config.FileServerCfg.DataBasePath);
        Assert.Equal(@"D:\Old", secondRow.Config.FileServerCfg.DataBasePath);
    });

    [Fact]
    public void RemoteSaveRequestsOnlyTypedReloadsAfterCommitAndCanRetryRejectedRequests() => WithStorage(directory =>
    {
        const string original = """{"FileServerCfg":{"DataBasePath":"D:\\Old"}}""";
        const string destination = @"E:\Shared";
        string databasePath = Path.Combine(directory, "remote.db");
        bool current = true;
        var endpoint = new MySqlConnectedEndpoint(_ => current, () => Open(databasePath));
        using var db = Open(databasePath);
        db.CodeFirst.InitTables<SysResourceModel>();
        db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_dictionary (id INTEGER PRIMARY KEY, [key] TEXT, pid INTEGER, val INTEGER)");
        var types = new[] { ServiceTypes.Camera, ServiceTypes.Algorithm, ServiceTypes.Calibration, ServiceTypes.Spectrum };
        var parents = new Dictionary<ServiceTypes, int>();
        foreach (var type in types)
        {
            db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_dictionary ([key], pid, val) VALUES (@route, 1, @type)",
                new { route = type.ToString(), type = (int)type });
            parents.Add(type, db.Insertable(new SysResourceModel { Code = $"host-{type}", Name = $"host-{type}", Type = (int)type }).ExecuteReturnIdentity());
        }

        using var camera = RemoteDevice<ConfigCamera>(ServiceTypes.Camera, "camera");
        using var algorithmA = RemoteDevice<ConfigAlgorithm>(ServiceTypes.Algorithm, "algorithm-a");
        using var algorithmB = RemoteDevice<ConfigAlgorithm>(ServiceTypes.Algorithm, "algorithm-b");
        using var calibration = RemoteDevice<ConfigCalibration>(ServiceTypes.Calibration, "calibration");
        using var spectrum = RemoteDevice<ConfigSpectrum>(ServiceTypes.Spectrum, "spectrum");
        DeviceService[] devices = [camera, algorithmA, algorithmB, calibration, spectrum];
        var requests = new List<(string Type, string Host, string[] Codes, Func<bool> Owns, bool Committed, bool WasCurrent)>();
        bool rejectAlgorithm = true;
        var configuration = new DeviceDataPathConfiguration(OpenConfig(directory), () => devices, (type, host, codes, owns) =>
        {
            using var reader = Open(databasePath);
            bool committed = devices.All(device => ReadPath(JObject.Parse(reader.Queryable<SysResourceModel>()
                .InSingle(device.SysResourceModel.Id).Value!)) == destination);
            requests.Add((type, host, codes.ToArray(), owns, committed, owns()));
            return !rejectAlgorithm || type != nameof(ServiceTypes.Algorithm);
        });

        var first = configuration.Apply(destination);

        Assert.Equal(5, first.UpdatedDevices);
        Assert.Equal(2, first.ReloadRequestedDevices);
        Assert.Equal(new[] { "algorithm-a", "algorithm-b" }, first.ReloadPendingDevices.Order());
        Assert.Equal("camera", Assert.Single(first.CameraReconnectDevices));
        Assert.DoesNotContain(requests, request => request.Type == nameof(ServiceTypes.Camera));
        Assert.Equal(3, requests.Count);
        Assert.All(requests, request => Assert.True(request.Committed && request.WasCurrent));
        var grouped = Assert.Single(requests, request => request.Type == nameof(ServiceTypes.Algorithm));
        Assert.Equal("host-Algorithm", grouped.Host);
        Assert.Equal(new[] { "algorithm-a", "algorithm-b" }, grouped.Codes);
        Assert.Equal(destination, camera.Config.FileServerCfg.DataBasePath);

        rejectAlgorithm = false;
        requests.Clear();
        var retry = configuration.Apply(destination);

        Assert.Equal(0, retry.UpdatedDevices);
        Assert.Equal(2, retry.ReloadRequestedDevices);
        Assert.Empty(retry.ReloadPendingDevices);
        Assert.Equal("camera", Assert.Single(retry.CameraReconnectDevices));
        var request = Assert.Single(requests);
        Assert.Equal(nameof(ServiceTypes.Algorithm), request.Type);
        Assert.True(request.Committed && request.WasCurrent);
        current = false;
        Assert.False(request.Owns());

        DeviceService<T> RemoteDevice<T>(ServiceTypes type, string code) where T : DeviceServiceConfig, new()
        {
            var row = Resource(type, code, original);
            row.Pid = parents[type];
            row.Id = db.Insertable(row).ExecuteReturnIdentity();
            return new DeviceService<T>(row) { ResultDatabaseEndpoint = endpoint };
        }
    });

    private static string? ReadPath(JObject json) => (string?)json["FileServerCfg"]?["DataBasePath"];

    private static ConfigHandler OpenConfig(string directory) => new() { ConfigFilePath = Path.Combine(directory, "ColorVisionConfig.json") };

    private static SysResourceModel Resource(ServiceTypes type, string code, string json) => new()
    {
        Code = code, Name = code, Type = (int)type, IsEnable = true, Value = json
    };

    private static DeviceService<T> LocalDevice<T>(SysResourceDao dao, ServiceTypes type, string code, string json)
        where T : DeviceServiceConfig, new()
    {
        var row = Resource(type, code, json);
        dao.Save(row, local: true);
        return new DeviceService<T>(row) { LocalConfigurationResourceDao = dao };
    }

    private static SqlSugarClient Open(string path) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite,
        IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
    });

    private static void WithStorage(Action<string> action) => WpfTestHost.Invoke(() =>
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-device-data-path-");
        Authorization previous = Authorization.Instance;
        try
        {
            Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
            action(directory.FullName);
        }
        finally
        {
            Authorization.Instance = previous;
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    });
}
