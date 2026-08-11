using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Archive.Dao;

namespace ColorVision.UI.Tests;

public class BatchSqlConsumerTests
{
    [Fact]
    public void ArchiveConfigurationMigrationExecutesOneAlterWithAllThreeColumnAdditions()
    {
        string? executedSql = null;
        bool editorOpened = false;

        int count = ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
            () => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            sql =>
            {
                executedSql = sql;
                return 0;
            },
            () => { editorOpened = true; });

        Assert.Equal(0, count);
        Assert.NotNull(executedSql);
        Assert.StartsWith($"ALTER TABLE `{ArchiveConfigurationSchemaMigration.TableName}`", executedSql, StringComparison.Ordinal);
        Assert.Equal(1, executedSql.Split("ALTER TABLE", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, executedSql.Split("ADD COLUMN", StringSplitOptions.None).Length - 1);
        Assert.True(editorOpened);
    }

    [Fact]
    public void CommittedCleanupWarningStillRunsTheConsumerFollowUp()
    {
        const string secret = "dispose secret";
        var executor = new FakeBatchSqlExecutor([3])
        {
            DisposeFailure = new ApplicationException(secret)
        };
        BatchCommittedCleanupWarning? warning = null;
        bool reloadRan = false;

        int count = BatchSqlConsumer.ExecuteAfterCommit(
            "update committed data;",
            sql => MySqlControl.BatchExecuteNonQuery(sql, () => executor, out warning),
            () => { reloadRan = true; });

        Assert.Equal(3, count);
        Assert.True(reloadRan);
        Assert.NotNull(warning);
        Assert.Equal(BatchExecutionStage.DisposeExecutor, warning.Stage);
        Assert.DoesNotContain(secret, warning.GetDiagnosticSummary(), StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateSettingFailureDoesNotReloadSymbols()
    {
        bool reloadRan = false;

        Assert.Throws<BatchExecuteNonQueryException>(() =>
            BatchSqlConsumer.ExecuteAfterCommit(
                "reset template tables;",
                _ => throw CreateBatchFailure(),
                () => reloadRan = true));

        Assert.False(reloadRan);
    }

    [Fact]
    public void ArchiveSchemaFailureDoesNotOpenEditor()
    {
        bool editorOpened = false;

        Assert.Throws<BatchExecuteNonQueryException>(() =>
            ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
                () => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                _ => throw CreateBatchFailure(),
                () => { editorOpened = true; }));

        Assert.False(editorOpened);
    }

    [Fact]
    public void ArchiveServerFailureDoesNotReloadConfiguration()
    {
        bool reloadRan = false;

        Assert.Throws<BatchExecuteNonQueryException>(() =>
            BatchSqlConsumer.ExecuteAfterCommit(
                "insert archive configuration;",
                _ => throw CreateBatchFailure(),
                () =>
                {
                    reloadRan = true;
                    return new object();
                }));

        Assert.False(reloadRan);
    }

    [Fact]
    public void UiAndLogFeedbackContainOnlySafeFailureMetadata()
    {
        const string secretSql = "INSERT UserPwd=do-not-display";
        var exception = new BatchExecuteNonQueryException(
            BatchExecutionStage.ExecuteStatement,
            2,
            new InvalidOperationException($"provider included SQL: {secretSql}"),
            new ApplicationException("rollback included secret SQL"));

        string uiMessage = BatchSqlConsumer.FormatFailureMessage("更新归档配置数据库结构", exception);
        string logMessage = BatchSqlConsumer.FormatDiagnosticSummary("更新归档配置数据库结构", exception);

        Assert.DoesNotContain(secretSql, uiMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("rollback included secret SQL", uiMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(secretSql, logMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("rollback included secret SQL", logMessage, StringComparison.Ordinal);
        Assert.Contains("StatementIndex=2", logMessage, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), uiMessage, StringComparison.Ordinal);
        Assert.Contains(nameof(ApplicationException), logMessage, StringComparison.Ordinal);
    }

    private static BatchExecuteNonQueryException CreateBatchFailure()
    {
        return new BatchExecuteNonQueryException(
            BatchExecutionStage.ExecuteStatement,
            2,
            new InvalidOperationException("provider failure"),
            null);
    }
}
