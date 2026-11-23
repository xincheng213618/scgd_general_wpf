using ColorVision.Database;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Tests;

public sealed class LicenseExpirationTests
{
    private static readonly DateTime Cutoff = new(2026, 9, 18, 12, 0, 0);

    [Theory]
    [InlineData(null, LicenseStatus.Unknown)]
    [InlineData(-1, LicenseStatus.Expired)]
    [InlineData(0, LicenseStatus.ExpiringSoon)]
    [InlineData(1, LicenseStatus.ExpiringSoon)]
    [InlineData(2591999, LicenseStatus.ExpiringSoon)]
    [InlineData(2592000, LicenseStatus.Normal)]
    public void StatusUsesExactExpiryAndThirtyDayBoundaries(int? seconds, LicenseStatus expected)
    {
        DateTime? expiry = seconds.HasValue ? Cutoff.AddSeconds(seconds.Value) : null;
        Assert.Equal(expected, LicenseViewModel.GetStatus(expiry, Cutoff));
    }

    [Fact]
    public void ServiceDeviceExpiryUsesOnlyMatchingLicensePayloads()
    {
        using var db = CreateDb();
        DateTimeOffset now = new(Cutoff, TimeSpan.Zero);
        LicenseModel expired = License(1, 0, Cutoff.AddDays(1));
        expired.LicenseValue = LicensePayload(now);
        LicenseModel invalid = License(2, 1, Cutoff.AddDays(-1));
        invalid.LicenseValue = "invalid payload";
        db.Insertable(new[] { expired, invalid }).ExecuteCommand();

        Assert.True(PhyLicenseDao.AreBoundLicensesExpired(db, expired.MacAddress!, now));
        Assert.False(PhyLicenseDao.AreBoundLicensesExpired(db, invalid.MacAddress!, now));
        Assert.False(PhyLicenseDao.AreBoundLicensesExpired(db, "missing", now));

        LicenseModel renewed = License(3, 1, Cutoff.AddDays(-1));
        renewed.MacAddress = expired.MacAddress;
        renewed.LicenseValue = LicensePayload(now.AddDays(1));
        db.Insertable(renewed).ExecuteCommand();

        Assert.False(PhyLicenseDao.AreBoundLicensesExpired(db, expired.MacAddress!, now));
    }

    [Fact]
    public async Task BoundExpiryUpdatesWpfContentWhileTheServiceStillReportsUnauthorized()
    {
        await WpfTestHost.Invoke(async () =>
        {
            using var device = new LicenseDisplayDevice();
            device.ResultDatabaseEndpoint = new(_ => true, () =>
            {
                var db = CreateDb();
                LicenseModel expired = License(1, 0, Cutoff);
                expired.LicenseValue = LicensePayload(DateTimeOffset.UtcNow.AddDays(-1));
                db.Insertable(expired).ExecuteCommand();
                return db;
            });
            var service = device.GetMQTTService();
            var button = new Button();
            button.SetBinding(ContentControl.ContentProperty, new Binding(nameof(DeviceService.LicenseFailureText)) { Source = device });
            Assert.Equal(Resources.DeviceStatusUnauthorized, button.Content);

            var labelChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            device.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DeviceService.LicenseFailureText)) labelChanged.TrySetResult();
            };
            service.DeviceStatus = DeviceStatusType.Unauthorized;
            device.RefreshLicenseFailure(service, service.DeviceStatus);
            await labelChanged.Task.WaitAsync(TimeSpan.FromSeconds(8));

            Assert.Equal(Resources.LicenseListTagExpired, button.Content);
            Assert.Equal(DeviceStatusType.Unauthorized, service.DeviceStatus);

            service.DeviceStatus = DeviceStatusType.Closed;
            device.RefreshLicenseFailure(service, service.DeviceStatus);
            Assert.Equal(Resources.DeviceStatusUnauthorized, button.Content);
        });
    }

    [Fact]
    public async Task CleanupRemovesOnlyConfirmedExpiredRecordsAcrossDeviceTypes()
    {
        using var db = CreateDb();
        db.Insertable(new[]
        {
            License(1, 0, Cutoff.AddSeconds(-1)),
            License(2, 1, Cutoff.AddDays(-1)),
            License(3, 0, Cutoff),
            License(4, 1, Cutoff.AddDays(10)),
            License(5, 0, Cutoff.AddYears(1)),
            License(6, 1, null),
            License(7, 0, Cutoff.AddDays(-5))
        }).ExecuteCommand();

        int deleted = await PhyLicenseDao.DeleteExpiredAsync(db, [1, 2, 3, 4, 5, 6], Cutoff);

        Assert.Equal(2, deleted);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, db.Queryable<LicenseModel>().OrderBy(x => x.Id).Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task CleanupKeepsLicensesRenewedAfterConfirmation()
    {
        using var db = CreateDb();
        db.Insertable(License(1, 1, Cutoff.AddDays(-1))).ExecuteCommand();
        int[] confirmedIds = [1];
        DateTime renewedExpiry = Cutoff.AddYears(1);
        db.Updateable<LicenseModel>().SetColumns(x => x.ExpiryDate == renewedExpiry).Where(x => x.Id == 1).ExecuteCommand();

        Assert.Equal(0, await PhyLicenseDao.DeleteExpiredAsync(db, confirmedIds, Cutoff));
        Assert.Equal(Cutoff.AddYears(1), db.Queryable<LicenseModel>().Single().ExpiryDate);
    }

    [Fact]
    public async Task EmptyConfirmationCannotDeleteAnyRecords()
    {
        using var db = CreateDb();
        db.Insertable(License(1, 0, Cutoff.AddYears(-1))).ExecuteCommand();

        Assert.Equal(0, await PhyLicenseDao.DeleteExpiredAsync(db, [], Cutoff));
        Assert.Equal(1, db.Queryable<LicenseModel>().Count());
    }

    [Fact]
    public async Task DatabaseFailurePropagatesInsteadOfReportingSuccess()
    {
        using var db = CreateDb();
        db.DbMaintenance.DropTable<LicenseModel>();

        await Assert.ThrowsAnyAsync<Exception>(() => PhyLicenseDao.DeleteExpiredAsync(db, [1], Cutoff));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task DeletionUsesTheCatalogDisplayedBeforeConfirmation(bool allExpired, bool switchAtConfirmation, bool switchAfterDeletion)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-license-delete-");
        try
        {
            string originalConnection = $"Data Source={Path.Combine(directory.FullName, "original.db")};Pooling=False";
            string replacementConnection = $"Data Source={Path.Combine(directory.FullName, "replacement.db")};Pooling=False";
            using var originalDb = CreateDb(originalConnection);
            using var replacementDb = CreateDb(replacementConnection);
            DateTime now = DateTime.Now;
            originalDb.Insertable(new[] { License(1, 0, now.AddDays(-5)), License(2, 1, now.AddYears(1)) }).ExecuteCommand();
            var replacementLicense = License(1, 1, now.AddDays(-5));
            replacementLicense.MacAddress = "REPLACEMENT-SN";
            replacementDb.Insertable(replacementLicense).ExecuteCommand();

            MySqlConnectedEndpoint? active = null;
            int originalOpens = 0, replacementOpens = 0;
            var replacement = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, active), () =>
            {
                replacementOpens++;
                return OpenDb(replacementConnection);
            });
            var original = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, active), () =>
            {
                originalOpens++;
                var db = OpenDb(originalConnection);
                if (switchAfterDeletion)
                {
                    db.Aop.OnLogExecuted = (sql, _) =>
                    {
                        if (sql.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) active = replacement;
                    };
                }
                return db;
            });
            active = original;

            await WpfTestHost.Invoke(async () =>
            {
                int confirmations = 0;
                Action? duringConfirmation = null;
                var manager = new LicenseManagerViewModel(() => new LicenseCatalogSource(active!), (_, _, _, _) =>
                {
                    confirmations++;
                    duringConfirmation!();
                    return true;
                });
                manager.SelectedLicense = manager.Licenses.Single(item => item.Id == 1);
                duringConfirmation = () =>
                {
                    Assert.False(manager.DeleteSelectedCommand.CanExecute(null));
                    Assert.False(manager.ClearExpiredCommand.CanExecute(null));
                    if (switchAtConfirmation) active = replacement;
                    else manager.SelectedLicense = manager.Licenses.Single(item => item.Id == 2);
                };

                if (allExpired) await manager.ClearExpiredAsync();
                else manager.DeleteSelected();

                Assert.Equal(1, confirmations);
                Assert.Equal(0, replacementOpens);
                if (switchAtConfirmation)
                {
                    Assert.Equal(1, originalOpens);
                    Assert.Contains(LicenseCatalogSource.SourceChanged().Message, manager.OperationMessage);
                    Assert.Equal("TEST-SN-1", manager.SelectedLicense!.MacAddress);
                }
                else if (switchAfterDeletion)
                {
                    Assert.Equal(2, originalOpens);
                    Assert.Contains(string.Format(ColorVision.Engine.Properties.Resources.LicenseClearExpiredSuccess, 1), manager.OperationMessage);
                    Assert.Contains(LicenseCatalogSource.SourceChanged().Message, manager.OperationMessage);
                }

                if (switchAtConfirmation || switchAfterDeletion)
                {
                    manager.LoadLicenses();
                    Assert.Equal("REPLACEMENT-SN", Assert.Single(manager.Licenses).MacAddress);
                    Assert.Null(manager.SelectedLicense);
                }
                else
                {
                    Assert.Equal(2, Assert.Single(manager.Licenses).Id);
                    Assert.Equal(2, manager.SelectedLicense!.Id);
                }
            });

            Assert.Equal(switchAtConfirmation ? new[] { 1, 2 } : new[] { 2 },
                originalDb.Queryable<LicenseModel>().OrderBy(item => item.Id).Select(item => item.Id).ToArray());
            Assert.Equal("REPLACEMENT-SN", replacementDb.Queryable<LicenseModel>().Single().MacAddress);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Fact]
    public void SingleDeletionReportsMissingRecordEvenWhenTheListCannotBeRefreshed()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-license-missing-");
        try
        {
            string connection = $"Data Source={Path.Combine(directory.FullName, "licenses.db")};Pooling=False";
            using var db = CreateDb(connection);
            db.Insertable(License(1, 1, Cutoff)).ExecuteCommand();
            const string refreshFailure = "Fixture refresh failure";
            int opens = 0;
            var endpoint = new MySqlConnectedEndpoint(_ => true, () =>
            {
                if (++opens == 3) throw new InvalidOperationException(refreshFailure);
                return OpenDb(connection);
            });

            WpfTestHost.Invoke(() =>
            {
                int concurrentDeletes = 0;
                var manager = new LicenseManagerViewModel(() => new LicenseCatalogSource(endpoint), (_, _, _, _) =>
                {
                    concurrentDeletes = db.Deleteable<LicenseModel>().Where(item => item.Id == 1).ExecuteCommand();
                    return true;
                });
                manager.SelectedLicense = Assert.Single(manager.Licenses);

                manager.DeleteSelected();

                Assert.Equal(1, concurrentDeletes);
                Assert.Equal(3, opens);
                Assert.Contains(ColorVision.Engine.EngineLocalization.Get("原许可证记录已不存在或已变更。"), manager.OperationMessage);
                Assert.Contains(refreshFailure, manager.OperationMessage);
                Assert.DoesNotContain(ColorVision.Engine.EngineLocalization.Get("已完成删除。"), manager.OperationMessage);
            });
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static string LicensePayload(DateTimeOffset expiry) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
        $$"""{"device_mode":"TEST","expiry_date":"{{expiry.ToUnixTimeSeconds()}}"}"""));

    private sealed class LicenseDisplayDevice : DeviceService
    {
        private readonly DeviceServiceConfig config = new() { SN = "TEST-SN-1" };
        private readonly MQTTServiceBase service = (MQTTServiceBase)RuntimeHelpers.GetUninitializedObject(typeof(MQTTServiceBase));

        public override object GetConfig() => config;
        public override MQTTServiceBase GetMQTTService() => service;
        public override CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }

    private static LicenseModel License(int id, int type, DateTime? expiry) => new()
    {
        Id = id,
        LiceType = type,
        MacAddress = $"TEST-SN-{id}",
        ExpiryDate = expiry,
        CreateDate = Cutoff.AddYears(-1)
    };

    private static SqlSugarClient CreateDb(string connectionString = "Data Source=:memory:")
    {
        var db = OpenDb(connectionString);
        db.CodeFirst.InitTables<LicenseModel>();
        return db;
    }

    private static SqlSugarClient OpenDb(string connectionString) => new(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false,
        InitKeyType = InitKeyType.Attribute,
        ConfigureExternalServices = new ConfigureExternalServices
        {
            EntityService = (_, column) =>
            {
                if (!column.IsPrimarykey)
                    column.IsNullable = true;
            }
        }
    });
}
