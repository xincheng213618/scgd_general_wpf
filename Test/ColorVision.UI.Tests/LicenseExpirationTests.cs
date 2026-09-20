using ColorVision.Engine.Services.PhyCameras.Licenses;
using SqlSugar;

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

    private static LicenseModel License(int id, int type, DateTime? expiry) => new()
    {
        Id = id,
        LiceType = type,
        MacAddress = $"TEST-SN-{id}",
        ExpiryDate = expiry,
        CreateDate = Cutoff.AddYears(-1)
    };

    private static SqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
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
        db.CodeFirst.InitTables<LicenseModel>();
        return db;
    }
}
