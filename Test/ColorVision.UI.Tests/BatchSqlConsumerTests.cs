using ColorVision.Database;
using ColorVision.Engine;

namespace ColorVision.UI.Tests;

public class BatchSqlConsumerTests
{
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
    public void FailedBatchDoesNotRunTheConsumerFollowUp()
    {
        bool reloadRan = false;

        Assert.Throws<BatchExecuteNonQueryException>(() =>
            BatchSqlConsumer.ExecuteAfterCommit(
                "update data;",
                _ => throw CreateBatchFailure(),
                () => reloadRan = true));

        Assert.False(reloadRan);
    }

    [Fact]
    public void FailedBatchDoesNotReloadConfiguration()
    {
        bool reloadRan = false;

        Assert.Throws<BatchExecuteNonQueryException>(() =>
            BatchSqlConsumer.ExecuteAfterCommit(
                "insert configuration;",
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

        string uiMessage = BatchSqlConsumer.FormatFailureMessage("更新配置数据库结构", exception);
        string logMessage = BatchSqlConsumer.FormatDiagnosticSummary("更新配置数据库结构", exception);

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
