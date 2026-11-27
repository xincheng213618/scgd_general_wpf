using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.PhySpectrums;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SpectrumLicenseImportTests
{
    [Fact]
    public void DatabaseRemovalKeepsRenewedLicensesAndRollsBackWhenTheSourceChanges()
    {
        using var db = CreateLicenseDatabase();
        db.Insertable(RemovalTestLicenses()).ExecuteCommand();
        var selection = db.Queryable<LicenseModel>().First(row => row.LicenseValue == "selected");
        db.Updateable<LicenseModel>().SetColumns(row => row.LicenseValue == "renewed").Where(row => row.Id == selection.Id).ExecuteCommand();

        Assert.False(PhySpectrumStore.TryRemoveLicenseRecords(db, selection, () => true));
        Assert.Equal(4, db.Queryable<LicenseModel>().Count());

        selection = db.Queryable<LicenseModel>().InSingle(selection.Id);
        int sourceChecks = 0;
        Assert.False(PhySpectrumStore.TryRemoveLicenseRecords(db, selection, () => ++sourceChecks < 3));
        Assert.Equal(4, db.Queryable<LicenseModel>().Count());

        Assert.True(PhySpectrumStore.TryRemoveLicenseRecords(db, selection, () => true));
        Assert.Equal(new[] { "camera", "other" }, db.Queryable<LicenseModel>().OrderBy(row => row.Id).Select(row => row.LicenseValue).ToArray());
    }

    [Fact]
    public void LocalRemovalDeletesDuplicateLicensesForOnlyTheSelectedSpectrometer()
    {
        string file = Path.Combine(Path.GetTempPath(), $"spectrum-remove-{Guid.NewGuid():N}.db");
        try
        {
            var store = new LocalTemplateStore(file);
            var dao = new PhyLicenseDao(store, () => false);
            var resources = new SysResourceDao(store, () => false);
            var registered = new SysResourceModel { Code = "TEST-SN", Type = (int)ServiceTypes.PhySpectrums };
            resources.Save(registered, local: true);
            var licenses = RemovalTestLicenses();
            foreach (var license in licenses) dao.Save(license, local: true);
            var selection = dao.GetById(licenses[1].Id)!;
            licenses[1].LicenseValue = "renewed";
            dao.Save(licenses[1], local: true);

            Assert.False(dao.TryRemoveSpectrumLicense(selection));
            Assert.False(dao.TryRemoveSpectrumLicense(licenses[2]));
            Assert.Equal(4, dao.GetLocal().Count);

            Assert.True(dao.TryRemoveSpectrumLicense(licenses[1]));
            var remaining = dao.GetLocal();
            Assert.Equal(new[] { "camera", "other" }, remaining.Select(row => row.LicenseValue).OrderBy(value => value).ToArray());
            var selected = Assert.Single(PhySpectrumStore.Merge(resources.GetLocal(), remaining, []), item => item.SN == "TEST-SN");
            Assert.Equal(registered.Id, selected.ResourceId);
            Assert.Null(selected.License);
        }
        finally { File.Delete(file); }
    }

    private static LicenseModel[] RemovalTestLicenses() =>
    [
        new() { MacAddress = " test-sn ", LiceType = 1, LicenseValue = "duplicate" },
        new() { MacAddress = "TEST-SN", LiceType = 1, LicenseValue = "selected" },
        new() { MacAddress = "TEST-SN", LiceType = 0, LicenseValue = "camera" },
        new() { MacAddress = "ANY-OTHER-SN", LiceType = 1, LicenseValue = "other" }
    ];

    [Fact]
    public void DatabaseRenewalUpdatesDuplicateSpectraWithoutChangingAssociationsOrCameraRecords()
    {
        using var db = CreateLicenseDatabase();
        DateTime created = new(2026, 1, 1);
        db.Insertable(new[]
        {
            new LicenseModel { MacAddress = "TEST-SN", LiceType = 1, LicenseValue = "old-a", DevCameraId = 363, CreateDate = created },
            new LicenseModel { MacAddress = "TEST-SN", LiceType = 1, LicenseValue = "old-b", DevCaliId = 12, CreateDate = created },
            new LicenseModel { MacAddress = "TEST-SN", LiceType = 0, LicenseValue = "camera" },
            new LicenseModel { MacAddress = "OTHER", LiceType = 1, LicenseValue = "other" }
        }).ExecuteCommand();
        int[] originalIds = db.Queryable<LicenseModel>().OrderBy(item => item.Id).Select(item => item.Id).ToArray();
        DateTime expiry = DateTime.Today.AddYears(1);
        Assert.Equal(2, PhySpectrumStore.UpdateLicenseRecords(db, new LicenseModel
        {
            MacAddress = "TEST-SN", LiceType = 1, LicenseValue = "renewed", Model = "SP-NEW", ExpiryDate = expiry
        }));
        var rows = db.Queryable<LicenseModel>().OrderBy(item => item.Id).ToArray();
        Assert.Equal(originalIds, rows.Select(item => item.Id));
        Assert.All(rows.Take(2), item =>
        {
            Assert.Equal("renewed", item.LicenseValue);
            Assert.Equal("SP-NEW", item.Model);
            Assert.Equal(expiry, item.ExpiryDate);
            Assert.Equal(created, item.CreateDate);
        });
        Assert.Equal(363, rows[0].DevCameraId);
        Assert.Equal(12, rows[1].DevCaliId);
        Assert.Equal("camera", rows[2].LicenseValue);
        Assert.Equal("other", rows[3].LicenseValue);
    }

    private static SqlSugarClient CreateLicenseDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (_, column) => { if (!column.IsPrimarykey) column.IsNullable = true; }
            }
        });
        db.CodeFirst.InitTables<LicenseModel>();
        return db;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedImportCanBeRetriedAndTheFirstValidImportRefreshesTheSelectedSerial(bool renewal)
    {
        await WpfTestHost.Invoke(async () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), $"spectrum-license-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var previousResources = SysResourceDao.Instance;
            var previousLicenses = PhyLicenseDao.Instance;
            List<ResourceDictionary> dictionaries = [];
            EditSpectrum? window = null;
            try
            {
                var store = new LocalTemplateStore(Path.Combine(directory, "config.db"));
                SysResourceDao.Instance = new SysResourceDao(store, () => false);
                PhyLicenseDao.Instance = new PhyLicenseDao(store, () => false);
                const string serial = "SPECTRUM-TEST-01";
                if (renewal)
                    PhySpectrumStore.SaveLicense(new LicenseModel { MacAddress = serial, LiceType = 1, LicenseValue = "old", ExpiryDate = DateTime.Today.AddDays(-1) }, true);
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                window = new EditSpectrum(new ConfigSpectrum { SN = "PREVIOUS-SN" }, new DisplaySpectrumConfig())
                {
                    Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                var busy = typeof(EditSpectrum).GetField("isBusy", BindingFlags.Instance | BindingFlags.NonPublic)!;
                while ((bool)busy.GetValue(window)!) await Task.Delay(10);
                var discoveries = (Dictionary<string, string>)typeof(EditSpectrum).GetField("discoveries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                discoveries[serial] = "CMvSpectra · USB";
                typeof(EditSpectrum).GetMethod("RefreshChoices", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                var choices = (ComboBox)window.FindName("SerialChoices");
                choices.SelectedItem = choices.Items.Cast<PhySpectrum>().Single(item => item.SN == serial);
                var upload = (Button)window.FindName("ImportButton");
                Assert.True(upload.IsEnabled);
                Assert.Equal(serial, window.EditConfig.SN);
                string value = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"device_mode":"SP-TEST","expiry_date":"2100000000","licensee":"Test"}"""));
                string file = Path.Combine(directory, serial + ".lic");
                File.WriteAllText(file, value);
                var run = typeof(EditSpectrum).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                string wrongFile = Path.Combine(directory, "OTHER.lic");
                File.WriteAllText(wrongFile, value);
                Func<Task> rejected = () => window.ImportLicenseAsync(wrongFile, serial);
                await (Task)run.Invoke(window, [rejected])!;
                Assert.False(string.IsNullOrWhiteSpace(((TextBlock)window.FindName("StatusText")).Text));
                Assert.True(upload.IsEnabled);
                Assert.Equal(renewal ? "old" : null, PhyLicenseDao.Instance.GetByMAC(serial, true)?.LicenseValue);
                Func<Task> import = () => window.ImportLicenseAsync(file, serial);
                await (Task)run.Invoke(window, [import])!;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

                Assert.Equal(serial, window.EditConfig.SN);
                Assert.Equal(serial, choices.Text);
                Assert.True(upload.IsEnabled);
                var summary = Assert.IsType<PhySpectrum>(((StackPanel)window.FindName("LicenseSummary")).DataContext);
                Assert.Equal(serial, summary.SN);
                Assert.Equal(value, summary.License!.LicenseValue);
                Assert.Equal("SP-TEST", summary.DisplayModel);
                Assert.False(summary.NeedsAttention);
                Assert.Equal(value, PhyLicenseDao.Instance.GetByMAC(serial, true)!.LicenseValue);
            }
            finally
            {
                window?.Close();
                SysResourceDao.Instance = previousResources;
                PhyLicenseDao.Instance = previousLicenses;
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        });
    }
}
