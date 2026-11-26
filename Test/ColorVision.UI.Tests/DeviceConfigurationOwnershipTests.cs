using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class DeviceConfigurationOwnershipTests
{
    private const string OriginalCode = "offline-config-owner";

    [Theory]
    [InlineData("current")]
    [InlineData("before-confirm")]
    [InlineData("during-copy")]
    public void GenericEditorCommitsOnlyToItsOpeningDatabase(string retirement) => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        using var device = fixture.CreateDevice();
        var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
            .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
        foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
        PropertyEditorWindow? editor = null;
        try
        {
            bool retiredBeforeConfirm = retirement == "before-confirm";
            bool retiredDuringCopy = retirement == "during-copy";
            int submitted = 0;
            Exception? saveError = null;
            editor = new PropertyEditorWindow(device.Config, PropertyEditorEditMode.Transactional);
            DeviceConfigurationTarget openingTarget = device.ProtectConfigurationEditor(editor);
            editor.Submitted += (_, _) =>
            {
                submitted++;
                saveError = Record.Exception(() => device.CommitConfiguration(expectedTarget: openingTarget));
            };
            ((DeviceServiceConfig)editor.EditConfig).Name = "edited name";
            if (retiredDuringCopy)
            {
                device.Config.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(DeviceServiceConfig.Name)) SwitchDatabase();
                };
            }
            if (retiredBeforeConfirm) SwitchDatabase();

            Assert.IsType<Button>(editor.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(retiredBeforeConfirm ? 0 : 1, submitted);
            Assert.Equal(retiredBeforeConfirm ? "original name" : "edited name", device.Config.Name);
            Assert.Equal(retirement == "current" ? "edited name" : "original name", device.Name);
            Assert.Equal(device.Name, fixture.ReadRow("original").Name);
            Assert.Equal("replacement name", fixture.ReadRow("replacement").Name);
            Assert.Equal(0, fixture.ReplacementOpens);
            if (retirement == "current")
                Assert.Equal(("type-original", "parent-original", OriginalCode), Assert.Single(fixture.Restarts));
            else
                Assert.Empty(fixture.Restarts);
            if (retiredBeforeConfirm)
                Assert.Equal(Visibility.Visible, Assert.IsType<TextBlock>(editor.FindName("SubmissionError")).Visibility);
            if (retiredDuringCopy)
                Assert.IsAssignableFrom<InvalidOperationException>(saveError);
            else
                Assert.Null(saveError);

            void SwitchDatabase()
            {
                fixture.RetireOriginal();
                device.ResultDatabaseEndpoint = fixture.Replacement;
            }
        }
        finally
        {
            editor?.Close();
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    });

    [Fact]
    public void CalibrationDisplayEditSavesLocallyAndPreservesTheOtherSettings() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        var previousConfig = ConfigService.Instance;
        var handler = new ConfigHandler { IsAutoSave = false };
        var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
            .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
        foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
        TestEnvironment.SetConfig(handler);
        CameraBoundDeviceConfigurationWindow? editor = null;
        try
        {
            using var device = fixture.CreateCalibrationDevice();
            Assert.False(device.DisplayConfig.IsAdvancedExposure);
            device.DisplayConfig.ExpTimeR = 42;
            device.DisplayConfig.IsAdvancedExposure = true;
            editor = device.CreateConfigurationEditor(owner: null);
            var fields = Assert.IsType<StackPanel>(editor.FindName("ConfigurationFields")).Children.OfType<DockPanel>();
            Assert.Equal(new[] { "Name", "CameraCode", "UseLocalCalibration" }, fields.Select(field => Assert.IsAssignableFrom<PropertyInfo>(field.Tag).Name));

            var localBackend = TypeDescriptor.GetProperties(editor.Draft)[nameof(DisplayCalibrationConfig.UseLocalCalibration)]!;
            localBackend.SetValue(editor.Draft, false);
            Assert.True(device.DisplayConfig.UseLocalCalibration);

            Assert.IsType<Button>(editor.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(device.DisplayConfig.UseLocalCalibration);
            Assert.True(device.DisplayConfig.IsAdvancedExposure);
            Assert.Equal(42d, device.DisplayConfig.ExpTimeR);
            var file = JObject.Parse(File.ReadAllText(handler.ConfigFilePath));
            var saved = file[ConfigSectionNames.GetSectionName(typeof(DisplayConfigManager))]!.ToObject<DisplayConfigManager>()!
                .GetDisplayConfig<DisplayCalibrationConfig>(OriginalCode);
            Assert.False(saved.UseLocalCalibration);
            Assert.True(saved.IsAdvancedExposure);
            Assert.Equal(0, fixture.OriginalOpens);
            Assert.Empty(fixture.Restarts);
        }
        finally
        {
            editor?.Close();
            TestEnvironment.SetConfig(previousConfig);
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    });

    [Fact]
    public void AlgorithmEditorPersistsNameAndPhysicalCameraToItsOpeningDatabase() => AsAdministrator(() =>
    {
        using var fixture = new Fixture(ServiceTypes.Algorithm);
        using var device = fixture.CreateAlgorithmDevice();
        var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
            .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
        foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
        CameraBoundDeviceConfigurationWindow? editor = null;
        try
        {
            editor = device.CreateConfigurationEditor(owner: null);
            editor.Draft.Name = "renamed algorithm";
            editor.Draft.CameraCode = "physical-camera-code";
            Assert.Equal("original name", device.Config.Name);

            Assert.IsType<Button>(editor.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var saved = fixture.ReadRow("original");
            Assert.Equal("renamed algorithm", saved.Name);
            Assert.Equal("physical-camera-code", JsonConvert.DeserializeObject<ConfigAlgorithm>(saved.Value)!.SN);
            Assert.Equal("replacement name", fixture.ReadRow("replacement").Name);
            Assert.Equal(("type-original", "parent-original", OriginalCode), Assert.Single(fixture.Restarts));
        }
        finally
        {
            editor?.Close();
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    });

    [Fact]
    public void ChangingCodeUpdatesTheOriginalRowAndRestartsThroughItsParentAndDictionary() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        using var device = fixture.CreateDevice();
        DeviceConfigurationTarget openingTarget = device.CaptureConfigurationTarget();
        Assert.True(device.TryAdmitConfigurationEdit(openingTarget, owner: null));
        int notifications = 0;
        device.ConfigChanged += (_, _) => notifications++;

        device.CommitConfiguration(() =>
        {
            device.Config.Name = "renamed";
            device.Config.Code = "offline-new-code";
        }, expectedTarget: openingTarget);

        var stored = fixture.ReadRow("original");
        Assert.Equal(("offline-new-code", "renamed"), (stored.Code, stored.Name));
        Assert.Equal((stored.Code, stored.Name), (device.Code, device.Name));
        var savedConfig = JsonConvert.DeserializeObject<DeviceServiceConfig>(stored.Value!)!;
        Assert.Equal((stored.Code, stored.Name), (savedConfig.Code, savedConfig.Name));
        Assert.Equal("original-remark", stored.Remark);
        Assert.Equal(1, notifications);
        Assert.Equal(("type-original", "parent-original", "offline-new-code"), Assert.Single(fixture.Restarts));

        fixture.Restarts.Clear();
        device.RestartRCService();
        Assert.Equal(("type-original", "parent-original", "offline-new-code"), Assert.Single(fixture.Restarts));
        Assert.Equal(OriginalCode, fixture.ReadRow("replacement").Code);
        Assert.Equal(0, fixture.ReplacementOpens);
    });

    [Fact]
    public void RetiredDeviceCannotSaveOrRestartUsingACollidingResourceFromTheNewDatabase() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        using var device = fixture.CreateDevice();
        DeviceConfigurationTarget openingTarget = device.CaptureConfigurationTarget();
        fixture.RetireOriginal();
        int notifications = 0;
        device.ConfigChanged += (_, _) => notifications++;
        device.Config.Name = "late edit";
        device.Config.Code = "late-code";

        Assert.False(device.TryAdmitConfigurationEdit(openingTarget, owner: null));
        Assert.ThrowsAny<InvalidOperationException>(device.SaveConfig);
        Assert.ThrowsAny<InvalidOperationException>(device.Save);
        device.RestartRCService();

        Assert.Equal((OriginalCode, "original name"), (device.Code, device.Name));
        SysResourceModel original = fixture.ReadRow("original"), replacement = fixture.ReadRow("replacement");
        Assert.Equal((OriginalCode, "original name", "{}"), (original.Code, original.Name, original.Value));
        Assert.Equal((OriginalCode, "replacement name", "{}"), (replacement.Code, replacement.Name, replacement.Value));
        Assert.Equal(0, notifications);
        Assert.Equal(0, fixture.OriginalOpens);
        Assert.Equal(0, fixture.ReplacementOpens);
        Assert.Empty(fixture.Restarts);
    });

    [Fact]
    public void ReloadRetiresTheDeviceEvenWhenTheDatabaseIsStillCurrent() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        using var device = fixture.CreateDevice();
        var openingTarget = device.CaptureConfigurationTarget();
        device.RetireConfiguration();
        device.Config.Name = "old editor";

        Assert.True(fixture.Original.IsCurrent);
        Assert.False(device.TryAdmitConfigurationEdit(openingTarget, owner: null));
        Assert.ThrowsAny<InvalidOperationException>(device.Save);
        device.RestartRCService();

        Assert.Equal("original name", fixture.ReadRow("original").Name);
        Assert.Equal(0, fixture.OriginalOpens);
        Assert.Empty(fixture.Restarts);
    });

    [Fact]
    public void NegativeIdSavesToItsCapturedLocalStoreAfterTheGlobalStoreChanges() => AsAdministrator(() =>
    {
        using var fixture = new Fixture();
        bool connected = false;
        var original = new SysResourceDao(new LocalTemplateStore(fixture.PathFor("local-original")), () => connected);
        var replacement = new SysResourceDao(new LocalTemplateStore(fixture.PathFor("local-replacement")), () => false);
        SysResourceModel first = Resource("original"), other = Resource("replacement");
        original.Save(first);
        replacement.Save(other);
        Assert.True(first.Id <= -2);
        Assert.Equal(first.Id, other.Id);
        SysResourceDao previous = SysResourceDao.Instance;
        try
        {
            SysResourceDao.Instance = original;
            using var device = new DeviceService<OfflineConfiguration>(first) { RestartRequest = fixture.RecordRestart };
            SysResourceDao.Instance = replacement;
            connected = true;
            fixture.RetireOriginal();
            device.ResultDatabaseEndpoint = fixture.Replacement;
            device.Config.Name = "local edited name";
            device.Config.Code = "local-edited-code";

            device.Save();

            var stored = original.GetById(first.Id)!;
            Assert.Equal(("local-edited-code", "local edited name"), (stored.Code, stored.Name));
            Assert.Equal((stored.Code, stored.Name), (device.Code, device.Name));
            var untouched = replacement.GetById(other.Id)!;
            Assert.Equal((OriginalCode, "replacement name", "{}"), (untouched.Code, untouched.Name, untouched.Value));
            Assert.Equal(0, fixture.ReplacementOpens);
            Assert.Empty(fixture.Restarts);
        }
        finally { SysResourceDao.Instance = previous; }
    });

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

    private static SysResourceModel Resource(string store) => new()
    {
        Code = OriginalCode, Name = $"{store} name", Remark = $"{store}-remark", TenantId = 7,
        Type = (int)ServiceTypes.Calibration, Value = "{}"
    };

    private sealed class OfflineConfiguration : DeviceServiceConfig
    {
        public OfflineConfiguration() { }

        // Avoid opening the real physical-camera selector while building the generic editor.
        [Browsable(false)]
        public override string SN { get => base.SN; set => base.SN = value; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-device-configuration-");
        private readonly ServiceTypes serviceType;
        private MySqlConnectedEndpoint active;
        private readonly SysResourceModel first;

        internal Fixture(ServiceTypes serviceType = ServiceTypes.Calibration)
        {
            this.serviceType = serviceType;
            Original = new MySqlConnectedEndpoint(IsCurrent, () => { OriginalOpens++; return Open("original"); });
            Replacement = new MySqlConnectedEndpoint(IsCurrent, () => { ReplacementOpens++; return Open("replacement"); });
            active = Original;
            try
            {
                first = Seed("original");
                Assert.Equal(first.Id, Seed("replacement").Id);
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
        internal List<(string Type, string Parent, string Device)> Restarts { get; } = [];

        internal DeviceService<OfflineConfiguration> CreateDevice() => new(first)
        {
            ResultDatabaseEndpoint = Original, RestartRequest = RecordRestart
        };

        internal DeviceCalibration CreateCalibrationDevice() => new(first)
        {
            ResultDatabaseEndpoint = Original, RestartRequest = RecordRestart
        };

        internal DeviceAlgorithm CreateAlgorithmDevice() => new(first)
        {
            ResultDatabaseEndpoint = Original, RestartRequest = RecordRestart
        };

        internal void RetireOriginal() => active = Replacement;
        internal void RecordRestart(string type, string parent, string device) => Restarts.Add((type, parent, device));
        private bool IsCurrent(MySqlConnectedEndpoint endpoint) => ReferenceEquals(endpoint, active);

        internal SysResourceModel ReadRow(string store)
        {
            using var db = Open(store);
            return db.Queryable<SysResourceModel>().InSingle(first.Id);
        }

        private SysResourceModel Seed(string store)
        {
            using var db = Open(store);
            db.CodeFirst.InitTables<SysResourceModel>();
            db.Ado.ExecuteCommand("CREATE TABLE t_scgd_sys_dictionary (id INTEGER PRIMARY KEY, [key] TEXT, pid INTEGER, val INTEGER)");
            db.Ado.ExecuteCommand("INSERT INTO t_scgd_sys_dictionary ([key], pid, val) VALUES (@route, 1, @type)",
                new { route = $"type-{store}", type = (int)serviceType });
            var row = Resource(store);
            row.Type = (int)serviceType;
            row.Pid = db.Insertable(new SysResourceModel { Code = $"parent-{store}", Name = "Parent" }).ExecuteReturnIdentity();
            row.Id = db.Insertable(row).ExecuteReturnIdentity();
            return row;
        }

        internal string PathFor(string store) => Path.Combine(directory.FullName, store + ".db");

        private SqlSugarClient Open(string store) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={PathFor(store)};Pooling=False", DbType = DbType.Sqlite,
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
