using ColorVision.Database;

namespace ColorVision.UI.Tests;

public sealed class ExportMySqlInitTablesTests
{
    [Fact]
    public void SqlSugarTimestampDefaultIsMadeCompatibleBeforeAnyDatabaseConnection()
    {
        using var db = MySqlTableInitializer.CreateInitializationClient("Server=127.0.0.1;Database=unused;User ID=unused");
        string? capturedSql = null;
        db.Aop.CheckConnectionExecuting = _ => throw new Xunit.Sdk.XunitException("No database may be opened.");
        db.Aop.OnLogExecuting = (sql, _) => { capturedSql = sql; throw new InvalidOperationException("SQL captured before execution."); };

        Assert.ThrowsAny<Exception>(() => db.DbMaintenance.AddDefaultValue("t_scgd_measure_result_sensor", "create_date", "CURRENT_TIMESTAMP"));

        Assert.Equal("ALTER TABLE `t_scgd_measure_result_sensor` ALTER COLUMN `create_date` SET DEFAULT CURRENT_TIMESTAMP", capturedSql);
    }

    [Theory]
    [InlineData("ALTER TABLE `t` ALTER COLUMN `created` SET DEFAULT (CURRENT_TIMESTAMP(3));", "ALTER TABLE `t` ALTER COLUMN `created` SET DEFAULT CURRENT_TIMESTAMP(3);")]
    [InlineData("ALTER TABLE `t` ALTER COLUMN `created` SET DEFAULT CURRENT_TIMESTAMP", "ALTER TABLE `t` ALTER COLUMN `created` SET DEFAULT CURRENT_TIMESTAMP")]
    [InlineData("ALTER TABLE `t` ALTER COLUMN `text` SET DEFAULT '(CURRENT_TIMESTAMP)'", "ALTER TABLE `t` ALTER COLUMN `text` SET DEFAULT '(CURRENT_TIMESTAMP)'")]
    [InlineData("SELECT 'SET DEFAULT (CURRENT_TIMESTAMP)'", "SELECT 'SET DEFAULT (CURRENT_TIMESTAMP)'")]
    public void InitializationCompatibilityPreservesOtherSqlAndTimestampPrecision(string sql, string expected)
    {
        Assert.Equal(expected, MySqlTableInitializer.NormalizeInitializationSql(sql));
    }

    [Fact]
    public void PerTableFailureContinuesRemainingTablesAndFailsOperation()
    {
        List<Type> attemptedTypes = new();

        AggregateException exception = Assert.Throws<AggregateException>(() =>
            MySqlTableInitializer.InitTableTypes(
                [typeof(FailingTable), typeof(SucceedingTable)],
                type =>
                {
                    attemptedTypes.Add(type);
                    if (type == typeof(FailingTable))
                    {
                        throw new InvalidOperationException("simulated database failure");
                    }
                }));

        Assert.Equal([typeof(FailingTable), typeof(SucceedingTable)], attemptedTypes);
        InvalidOperationException tableFailure = Assert.IsType<InvalidOperationException>(Assert.Single(exception.InnerExceptions));
        Assert.Contains(typeof(FailingTable).FullName!, tableFailure.Message, StringComparison.Ordinal);
        Assert.Equal("simulated database failure", tableFailure.InnerException?.Message);
    }

    [Fact]
    public void SuccessfulTablesCompleteWithoutAggregateFailure()
    {
        List<Type> attemptedTypes = new();

        MySqlTableInitializer.InitTableTypes(
            [typeof(FailingTable), typeof(SucceedingTable)],
            attemptedTypes.Add);

        Assert.Equal([typeof(FailingTable), typeof(SucceedingTable)], attemptedTypes);
    }

    private sealed class FailingTable : IInitTables
    {
    }

    private sealed class SucceedingTable : IInitTables
    {
    }
}
