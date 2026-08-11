using ColorVision.Database;

namespace ColorVision.UI.Tests;

public class BatchExecuteNonQueryTests
{
    [Fact]
    public void CreateExecutorFailureSurfacesSafeTypedFailureWithoutStartingATransaction()
    {
        const string secret = "UserPwd=do-not-log";

        BatchExecuteNonQueryException exception = Assert.Throws<BatchExecuteNonQueryException>(() =>
            MySqlControl.BatchExecuteNonQuery(
                $"INSERT {secret};",
                () => throw new InvalidOperationException($"factory failure included {secret}")));

        Assert.Equal(BatchExecutionStage.CreateExecutor, exception.Stage);
        Assert.Null(exception.StatementIndex);
        Assert.Equal(nameof(InvalidOperationException), exception.FailureType);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, exception.GetDiagnosticSummary(), StringComparison.Ordinal);
    }

    [Fact]
    public void BeginFailureRollsBackAndSurfacesTypedFailure()
    {
        var executor = new FakeBatchSqlExecutor { BeginFailure = new InvalidOperationException("begin secret") };

        BatchExecuteNonQueryException exception = ExecuteFailure("first;", executor);

        Assert.Equal(BatchExecutionStage.BeginTransaction, exception.Stage);
        Assert.Null(exception.StatementIndex);
        Assert.Equal(nameof(InvalidOperationException), exception.FailureType);
        Assert.Equal(1, executor.BeginCount);
        Assert.Equal(1, executor.RollbackCount);
        Assert.Equal(0, executor.CommitCount);
        Assert.Equal(1, executor.DisposeCount);
        Assert.DoesNotContain("begin secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SecondStatementFailureRollsBackStopsTheBatchAndDoesNotExposePartialCountOrSql()
    {
        const string secret = "UserPwd=do-not-log";
        var executor = new FakeBatchSqlExecutor([4, new InvalidOperationException($"failed SQL: {secret}"), 9]);

        BatchExecuteNonQueryException exception = ExecuteFailure($"first; INSERT {secret}; third;", executor);

        Assert.Equal(BatchExecutionStage.ExecuteStatement, exception.Stage);
        Assert.Equal(2, exception.StatementIndex);
        Assert.Equal(nameof(InvalidOperationException), exception.FailureType);
        Assert.Equal(["first", $"INSERT {secret}"], executor.ExecutedStatements);
        Assert.Equal(1, executor.RollbackCount);
        Assert.Equal(0, executor.CommitCount);
        Assert.Equal(1, executor.DisposeCount);
        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, exception.GetDiagnosticSummary(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        Assert.Null(typeof(BatchExecuteNonQueryException).GetProperty("SqlStatement"));
        Assert.Null(typeof(BatchExecuteNonQueryException).GetProperty("AffectedRowsBeforeRollback"));
    }

    [Fact]
    public void CommitFailureRollsBackAndDoesNotReturnTheAccumulatedCount()
    {
        var executor = new FakeBatchSqlExecutor([2, 3])
        {
            CommitFailure = new InvalidOperationException("commit failed after five rows")
        };

        BatchExecuteNonQueryException exception = ExecuteFailure("first; second;", executor);

        Assert.Equal(BatchExecutionStage.CommitTransaction, exception.Stage);
        Assert.Null(exception.StatementIndex);
        Assert.Equal(1, executor.CommitCount);
        Assert.Equal(1, executor.RollbackCount);
        Assert.Equal(1, executor.DisposeCount);
        Assert.DoesNotContain("five rows", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RollbackFailurePreservesTheStatementFailureAsPrimary()
    {
        var executor = new FakeBatchSqlExecutor([new InvalidOperationException("primary SQL secret")])
        {
            RollbackFailure = new ApplicationException("rollback secret")
        };

        BatchExecuteNonQueryException exception = ExecuteFailure("secret statement;", executor);

        Assert.Equal(BatchExecutionStage.ExecuteStatement, exception.Stage);
        Assert.Equal(1, exception.StatementIndex);
        Assert.Equal(nameof(InvalidOperationException), exception.FailureType);
        Assert.Equal(nameof(ApplicationException), exception.RollbackFailureType);
        Assert.NotNull(exception.RollbackErrorCode);
        Assert.Equal(1, executor.RollbackCount);
        Assert.DoesNotContain("primary SQL secret", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("rollback secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DisposeFailureDoesNotReplaceTheTransactionPrimaryFailure()
    {
        var executor = new FakeBatchSqlExecutor([new InvalidOperationException("primary secret")])
        {
            RollbackFailure = new NotSupportedException("rollback secret"),
            DisposeFailure = new ApplicationException("dispose secret")
        };

        BatchExecuteNonQueryException exception = ExecuteFailure("secret statement;", executor);

        Assert.Equal(BatchExecutionStage.ExecuteStatement, exception.Stage);
        Assert.Equal(nameof(InvalidOperationException), exception.FailureType);
        Assert.Equal(nameof(NotSupportedException), exception.RollbackFailureType);
        Assert.Equal(nameof(ApplicationException), exception.DisposeFailureType);
        Assert.NotNull(exception.RollbackErrorCode);
        Assert.NotNull(exception.DisposeErrorCode);
        Assert.Equal(1, executor.DisposeCount);
        Assert.DoesNotContain("primary secret", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("rollback secret", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("dispose secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DisposeFailureAfterCommitReturnsCommittedCount()
    {
        var executor = new FakeBatchSqlExecutor([2])
        {
            DisposeFailure = new ApplicationException("dispose secret")
        };

        int count = MySqlControl.BatchExecuteNonQuery("first;", () => executor);

        Assert.Equal(2, count);
        Assert.Equal(1, executor.CommitCount);
        Assert.Equal(0, executor.RollbackCount);
        Assert.Equal(1, executor.DisposeCount);
    }

    [Fact]
    public void CommittedCleanupWarningContainsOnlySafeMetadata()
    {
        const string secret = "dispose secret";
        var executor = new FakeBatchSqlExecutor([2])
        {
            DisposeFailure = new ApplicationException(secret)
        };

        int count = MySqlControl.BatchExecuteNonQuery("first;", () => executor, out BatchCommittedCleanupWarning? warning);

        Assert.Equal(2, count);
        Assert.NotNull(warning);
        Assert.Equal(BatchExecutionStage.DisposeExecutor, warning.Stage);
        Assert.Equal(nameof(ApplicationException), warning.FailureType);
        Assert.DoesNotContain(secret, warning.GetDiagnosticSummary(), StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulBatchCommitsDisposesAndReturnsTheTotalAffectedRows()
    {
        var executor = new FakeBatchSqlExecutor([2, 3, 5]);

        int count = MySqlControl.BatchExecuteNonQuery(" first ; second; ; third ", () => executor);

        Assert.Equal(10, count);
        Assert.Equal(["first", "second", "third"], executor.ExecutedStatements);
        Assert.Equal(1, executor.BeginCount);
        Assert.Equal(1, executor.CommitCount);
        Assert.Equal(0, executor.RollbackCount);
        Assert.Equal(1, executor.DisposeCount);
    }

    private static BatchExecuteNonQueryException ExecuteFailure(string sqlBatch, FakeBatchSqlExecutor executor)
    {
        return Assert.Throws<BatchExecuteNonQueryException>(() => MySqlControl.BatchExecuteNonQuery(sqlBatch, () => executor));
    }
}

internal sealed class FakeBatchSqlExecutor : IBatchSqlExecutor
{
    private readonly Queue<object> _outcomes;

    public FakeBatchSqlExecutor(IEnumerable<object>? outcomes = null)
    {
        _outcomes = new Queue<object>(outcomes ?? []);
    }

    public Exception? BeginFailure { get; init; }

    public Exception? CommitFailure { get; init; }

    public Exception? RollbackFailure { get; init; }

    public Exception? DisposeFailure { get; init; }

    public List<string> ExecutedStatements { get; } = [];

    public int BeginCount { get; private set; }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public int DisposeCount { get; private set; }

    public void BeginTransaction()
    {
        BeginCount++;
        if (BeginFailure != null)
            throw BeginFailure;
    }

    public int ExecuteNonQuery(string sql)
    {
        ExecutedStatements.Add(sql);
        object outcome = _outcomes.Dequeue();
        if (outcome is Exception exception)
            throw exception;

        return (int)outcome;
    }

    public void CommitTransaction()
    {
        CommitCount++;
        if (CommitFailure != null)
            throw CommitFailure;
    }

    public void RollbackTransaction()
    {
        RollbackCount++;
        if (RollbackFailure != null)
            throw RollbackFailure;
    }

    public void Dispose()
    {
        DisposeCount++;
        if (DisposeFailure != null)
            throw DisposeFailure;
    }
}
