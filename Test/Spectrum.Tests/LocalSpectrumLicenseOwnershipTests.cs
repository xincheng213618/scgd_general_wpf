using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using cvColorVision;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using CaptureSettings = ColorVision.Engine.Services.Devices.Spectrum.Configs.GetDataConfig;
using Sp100Settings = ColorVision.Engine.Services.Devices.Spectrum.Configs.SetEmissionSP100Config;

namespace Spectrum.Tests;

[Collection("LocalSpectrumDriver")]
public sealed class LocalSpectrumLicenseOwnershipTests
{
    [Fact]
    public void RenewalDuringNativeInitDoesNotChangeThePreparedPayloadOrExpiry()
    {
        using var fixture = new LicenseFixture();
        var owner = fixture.CreateDevice(1);
        fixture.Bind(owner, fixture.SeedRemote("a.db", "original-expired", expired: true));
        var native = new OpenOnlyNative(() =>
        {
            Assert.Equal("original-expired", File.ReadAllText(fixture.LicensePath));
            fixture.RenewRemote("a.db", "renewed-in-init");
        });
        using var session = new LocalSpectrumSession(native);
        var source = fixture.Capture(owner);

        Assert.Throws<LocalSpectrumLicenseException>(() => session.Open(owner.Config, licenseSource: source));

        Assert.Equal("original-expired", File.ReadAllText(fixture.LicensePath));
        Assert.Equal(new[] { "a.db" }, fixture.RemoteReads);
        AssertReleased(session, native);
    }

    [Fact]
    public void RebindingDuringNativeInitClosesTheHandleWithoutReadingTheReplacementDatabase()
    {
        using var fixture = new LicenseFixture();
        var owner = fixture.CreateDevice(1);
        var admitted = fixture.SeedRemote("a.db", "original-source");
        var replacement = fixture.SeedRemote("b.db", "replacement-source");
        fixture.Bind(owner, admitted);
        var native = new OpenOnlyNative(() => fixture.Bind(owner, replacement));
        using var session = new LocalSpectrumSession(native);
        var source = fixture.Capture(owner);

        Assert.Throws<InvalidOperationException>(() => session.Open(owner.Config, licenseSource: source));

        Assert.Equal("original-source", File.ReadAllText(fixture.LicensePath));
        Assert.Equal(new[] { "a.db" }, fixture.RemoteReads);
        AssertReleased(session, native);
    }

    [Fact]
    public void ChangingTheConfiguredSerialDuringInitReleasesTheOriginalHandle()
    {
        using var fixture = new LicenseFixture();
        var owner = fixture.CreateDevice(1);
        fixture.Bind(owner, fixture.SeedRemote("a.db", "admitted-source"));
        var source = fixture.Capture(owner);
        var native = new OpenOnlyNative(() => owner.Config.SN = "REPLACEMENT-SPECTRUM");
        using var session = new LocalSpectrumSession(native);

        var failure = Assert.Throws<InvalidOperationException>(() => session.Open(owner.Config, licenseSource: source));

        Assert.Contains("原许可证来源", failure.Message);
        Assert.Equal(new[] { "a.db" }, fixture.RemoteReads);
        AssertReleased(session, native);
    }

    [Fact]
    public void RebindingBeforePromotionPreservesTheInstalledLicenseBytes()
    {
        using var fixture = new LicenseFixture();
        var owner = fixture.CreateDevice(1);
        var admitted = fixture.SeedRemote("a.db", "admitted-source");
        var replacement = fixture.SeedRemote("b.db", "replacement-source");
        fixture.Bind(owner, admitted);
        Directory.CreateDirectory(fixture.LicenseDirectory);
        byte[] installed = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("installed-license")];
        File.WriteAllBytes(fixture.LicensePath, installed);

        bool switched = false;
        var source = fixture.Capture(owner, () =>
        {
            if (Directory.EnumerateFiles(fixture.LicenseDirectory, "*.tmp").Any())
            {
                fixture.Bind(owner, replacement);
                switched = true;
            }
            return true;
        });
        var native = new OpenOnlyNative();
        using var session = new LocalSpectrumSession(native);

        Assert.Throws<InvalidOperationException>(() => session.Open(owner.Config, licenseSource: source));

        Assert.True(switched);
        Assert.Equal(new[] { "a.db" }, fixture.RemoteReads);
        Assert.Equal(installed, File.ReadAllBytes(fixture.LicensePath));
        Assert.Equal(fixture.LicensePath, Assert.Single(Directory.GetFiles(fixture.LicenseDirectory)));
        Assert.Empty(native.Calls);
        Assert.False(session.IsOpen);
        using var available = SpectrometerDriverLease.TryAcquire();
        Assert.NotNull(available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledFileWithoutALicenseRecordRequiresExperimentalBypass(bool experimental)
    {
        using var fixture = new LicenseFixture();
        ExperimentalFeaturesConfig.Instance.IsEnabled = experimental;
        var owner = fixture.CreateDevice(1);
        fixture.Bind(owner, fixture.SeedRemote("empty.db", value: null));
        Directory.CreateDirectory(fixture.LicenseDirectory);
        File.WriteAllText(fixture.LicensePath, "installed-license");
        var source = fixture.Capture(owner);
        var native = new OpenOnlyNative(() => Assert.Equal("installed-license", File.ReadAllText(fixture.LicensePath)));
        using var session = new LocalSpectrumSession(native);

        if (experimental)
        {
            session.Open(owner.Config, licenseSource: source);
            ExperimentalFeaturesConfig.Instance.IsEnabled = false;
            session.Open(owner.Config, licenseSource: fixture.Capture(owner));
            Assert.True(session.IsOpen);
            Assert.Equal(new[] { "create", "init", "serial", "configure" }, native.Calls);
        }
        else
        {
            Assert.Throws<LocalSpectrumLicenseException>(() => session.Open(owner.Config, licenseSource: source));
            AssertReleased(session, native);
        }

        Assert.Equal(new[] { "empty.db" }, fixture.RemoteReads);
        Assert.Equal("installed-license", File.ReadAllText(fixture.LicensePath));
    }

    [Fact]
    public void OccupiedDriverRejectsTheOpenBeforeReadingOrOverwritingALicense()
    {
        using var fixture = new LicenseFixture();
        var owner = fixture.CreateDevice(1);
        fixture.Bind(owner, fixture.SeedRemote("a.db", "must-not-be-installed"));
        Directory.CreateDirectory(fixture.LicenseDirectory);
        File.WriteAllText(fixture.LicensePath, "installed-license");
        var native = new OpenOnlyNative();
        using var session = new LocalSpectrumSession(native);
        using var occupied = SpectrometerDriverLease.TryAcquire();
        Assert.NotNull(occupied);
        var source = fixture.Capture(owner);

        Assert.Throws<InvalidOperationException>(() => session.Open(owner.Config, licenseSource: source));

        Assert.Empty(fixture.RemoteReads);
        Assert.Equal("installed-license", File.ReadAllText(fixture.LicensePath));
        Assert.Empty(native.Calls);
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void NegativeResourceLoadsItsAttachedSqliteLicenseWhenGlobalStoresDiffer()
    {
        using var fixture = new LicenseFixture();
        var owner = fixture.CreateDevice(-2);
        var ownerStore = fixture.SeedLocal("original-local.db", "attached-local-license");
        owner.LocalConfigurationResourceDao = new SysResourceDao(ownerStore, () => false);
        SysResourceDao previousResources = SysResourceDao.Instance;
        PhyLicenseDao previousLicenses = PhyLicenseDao.Instance;
        try
        {
            var unrelatedStore = fixture.SeedLocal("global-local.db", "wrong-global-license");
            SysResourceDao.Instance = new SysResourceDao(unrelatedStore, () => false);
            PhyLicenseDao.Instance = new PhyLicenseDao(unrelatedStore, () => false);
            var source = fixture.Capture(owner);
            var native = new OpenOnlyNative(() => Assert.Equal("attached-local-license", File.ReadAllText(fixture.LicensePath)));
            using var session = new LocalSpectrumSession(native);

            session.Open(owner.Config, licenseSource: source);

            Assert.True(session.IsOpen);
            Assert.Empty(fixture.RemoteReads);
            Assert.Equal("attached-local-license", File.ReadAllText(fixture.LicensePath));
            Assert.Equal(new[] { "create", "init", "serial", "configure" }, native.Calls);
        }
        finally
        {
            PhyLicenseDao.Instance = previousLicenses;
            SysResourceDao.Instance = previousResources;
        }
    }

    private static void AssertReleased(LocalSpectrumSession session, OpenOnlyNative native)
    {
        Assert.False(session.IsOpen);
        Assert.False(session.IsQuarantined);
        Assert.Equal(new[] { "create", "init", "serial", "configure", "close", "release" }, native.Calls);
        using var available = SpectrometerDriverLease.TryAcquire();
        Assert.NotNull(available);
    }

    private sealed class LicenseFixture : IDisposable
    {
        internal const string Serial = "TEST-LICENSE-SNAPSHOT";
        private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("cv-spectrum-license-owner-");
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private MySqlConnectedEndpoint? active;
        internal List<string> RemoteReads { get; } = [];
        internal string LicenseDirectory => Path.Combine(root.FullName, "files");
        internal string LicensePath => Path.Combine(LicenseDirectory, Serial + ".lic");

        internal LicenseFixture() => ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });

        internal DeviceSpectrum CreateDevice(int id)
        {
            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            device.Config = new ConfigSpectrum { Code = "SPECTRUM.LICENSE.TEST", SN = Serial };
            device.SysResourceModel = new SysResourceModel { Id = id, Code = device.Config.Code };
            return device;
        }

        internal LocalSpectrumLicenseSource Capture(DeviceSpectrum owner, Func<bool>? ownsConfiguration = null)
        {
            LocalSpectrumOpticalScope optics = owner.SnapshotLocalOptics("Open");
            return LocalSpectrumLicense.Capture(owner, () => (ownsConfiguration?.Invoke() ?? true) && optics.IsDeviceCurrent,
                directory: LicenseDirectory);
        }

        internal void Bind(DeviceSpectrum owner, MySqlConnectedEndpoint endpoint)
        {
            active = endpoint;
            owner.ResultDatabaseEndpoint = endpoint;
        }

        internal MySqlConnectedEndpoint SeedRemote(string name, string? value, bool expired = false)
        {
            using var db = OpenSqlite(name);
            db.CodeFirst.InitTables<LicenseModel>();
            if (value != null) db.Insertable(CreateLicense(value, expired)).ExecuteCommand();
            return new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, active), () =>
            {
                RemoteReads.Add(name);
                return OpenSqlite(name);
            });
        }

        internal void RenewRemote(string name, string value)
        {
            using var db = OpenSqlite(name);
            DateTime renewedExpiry = DateTime.Now.AddDays(30);
            int changed = db.Updateable<LicenseModel>()
                .SetColumns(_ => new LicenseModel { LicenseValue = value, ExpiryDate = renewedExpiry })
                .Where(row => row.MacAddress == Serial && row.LiceType == 1).ExecuteCommand();
            Assert.Equal(1, changed);
        }

        internal LocalTemplateStore SeedLocal(string name, string value)
        {
            var store = new LocalTemplateStore(Path.Combine(root.FullName, name));
            var dao = new PhyLicenseDao(store, () => false);
            Assert.True(dao.Save(CreateLicense(value), local: true) > 0);
            return store;
        }

        private static LicenseModel CreateLicense(string value, bool expired = false) => new()
        {
            MacAddress = Serial, LiceType = 1, LicenseValue = value,
            ExpiryDate = DateTime.Now.AddDays(expired ? -30 : 30),
        };

        private SqlSugarClient OpenSqlite(string name) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(root.FullName, name)};Pooling=False",
            DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (_, column) => { if (!column.IsPrimarykey) column.IsNullable = true; },
            },
        });

        public void Dispose()
        {
            ConfigService.SetInstance(previousConfig);
            if (Directory.Exists(LicenseDirectory))
            {
                foreach (string file in Directory.GetFiles(LicenseDirectory)) File.Delete(file);
                Directory.Delete(LicenseDirectory);
            }
            foreach (var file in root.EnumerateFiles()) file.Delete();
            root.Delete();
        }
    }

    private sealed class OpenOnlyNative(Action? onInit = null) : ILocalSpectrumNative
    {
        internal List<string> Calls { get; } = [];
        public IntPtr Create(int type, Spectrometer.Emission_CallBack callback) { Calls.Add("create"); return (IntPtr)123; }
        public int Init(IntPtr handle, int port, int baud) { Calls.Add("init"); onInit?.Invoke(); return 1; }
        public int SerialNumber(IntPtr handle, StringBuilder serial) { Calls.Add("serial"); serial.Append(LicenseFixture.Serial); return 1; }
        public int Close(IntPtr handle) { Calls.Add("close"); return 1; }
        public int Release(IntPtr handle) { Calls.Add("release"); return 1; }
        public int LoadWavelength(IntPtr handle, string path) => throw new NotSupportedException();
        public int LoadMagnitude(IntPtr handle, string path) => throw new NotSupportedException();
        public int Configure(IntPtr handle, Sp100Settings config) { Calls.Add("configure"); return 1; }
        public int ConfigureSmoothing(IntPtr handle, SpectrumSmoothingMethod method, int filterWidth) => throw new NotSupportedException();
        public int AutoTime(IntPtr handle, ref float time, ConfigSpectrum config, CancellationToken cancellationToken) => throw new NotSupportedException();
        public int Dark(IntPtr handle, float time, int average, int filter, float[] data, bool adaptive) => throw new NotSupportedException();
        public int InitAutoDark(IntPtr handle, SelfAdaptionInitDark settings) => throw new NotSupportedException();
        public LocalSpectrumCapture Capture(IntPtr handle, LocalSpectrumParameters parameters, CaptureSettings settings, float[] dark) => throw new NotSupportedException();
    }
}
