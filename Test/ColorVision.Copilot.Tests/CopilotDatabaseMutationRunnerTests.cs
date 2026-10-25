using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDatabaseMutationRunnerTests
{
    public static IEnumerable<object[]> RollbackCases =>
        from evidence in new[] { "clean", "warning-1196", "other-warning", "warning-read-failed", "rollback-failed" }
        from cancelled in new[] { false, true }
        select new object[] { evidence, cancelled };

    [Theory]
    [MemberData(nameof(RollbackCases))]
    public async Task FailedDmlRequiresACleanRollbackAndWarningCheck(string evidence, bool cancelled)
    {
        var failure = CreateFailure(cancelled);
        var session = new FakeSession
        {
            CommandFailure = failure,
            RollbackWarning = evidence switch { "warning-1196" => 1196, "other-warning" => 9999, _ => null },
            WarningReadFailure = evidence == "warning-read-failed" ? new IOException("Warning check failed.") : null,
            RollbackFailure = evidence == "rollback-failed" ? new IOException("Rollback failed.") : null,
        };

        var thrown = await Record.ExceptionAsync(() => CopilotDatabaseMutationRunner.ExecuteAsync(
            session, "UPDATE measurements SET reviewed = 1 WHERE id = 42", Analyze("UPDATE measurements SET reviewed = 1 WHERE id = 42"),
            19, CancellationToken.None));

        if (evidence == "clean")
            Assert.Same(failure, thrown);
        else
            Assert.Same(failure, Assert.IsType<CopilotDatabaseMutationOutcomeUnknownException>(thrown).InnerException);
        Assert.Equal(1, session.RollbackCalls);
        Assert.Equal(evidence == "rollback-failed" ? 0 : 1, session.WarningCheckCalls);
        if (session.WarningCheckCalls > 0)
            Assert.Equal(19, session.WarningCheckTimeout);
    }

    [Theory]
    [InlineData("commit", false)]
    [InlineData("commit", true)]
    [InlineData("ddl", false)]
    [InlineData("ddl", true)]
    public async Task CommitAndNontransactionalFailuresRemainUnknownWithoutAssumingRollback(string phase, bool cancelled)
    {
        var failure = CreateFailure(cancelled);
        var sql = phase == "ddl" ? "CREATE TABLE measurements_archive (id INT)" : "UPDATE measurements SET reviewed = 1 WHERE id = 42";
        var session = new FakeSession
        {
            CommandFailure = phase == "ddl" ? failure : null,
            CommitFailure = phase == "commit" ? failure : null,
        };

        var thrown = await Assert.ThrowsAsync<CopilotDatabaseMutationOutcomeUnknownException>(() =>
            CopilotDatabaseMutationRunner.ExecuteAsync(session, sql, Analyze(sql), 19, CancellationToken.None));

        Assert.Same(failure, thrown.InnerException);
        Assert.Equal(0, session.RollbackCalls);
        Assert.Equal(0, session.WarningCheckCalls);
    }

    [Theory]
    [InlineData("UPDATE measurements SET reviewed = 1 WHERE id = 42", true)]
    [InlineData("CREATE TABLE measurements_archive (id INT)", false)]
    public async Task SuccessfulMutationPreservesAffectedRowsAndTransactionEvidence(string sql, bool transactional)
    {
        var session = new FakeSession();

        var result = await CopilotDatabaseMutationRunner.ExecuteAsync(session, sql, Analyze(sql), 19, CancellationToken.None);

        Assert.Equal(3, result.AffectedRows);
        Assert.Equal(transactional, result.Transactional);
        Assert.Equal(transactional ? 1 : 0, session.CommitCalls);
        Assert.Equal(0, session.RollbackCalls);
        Assert.Equal(0, session.WarningCheckCalls);
    }

    private static Exception CreateFailure(bool cancelled) => cancelled
        ? new OperationCanceledException("The command was cancelled.")
        : new InvalidOperationException("The command failed.");

    private static CopilotDatabaseSqlAnalysis Analyze(string sql)
    {
        Assert.True(CopilotDatabaseSqlPolicy.TryAnalyze(sql, out var analysis, out var error), error);
        return analysis!;
    }

    private sealed class FakeSession : ICopilotDatabaseMutationSession
    {
        public Exception? CommandFailure { get; init; }
        public Exception? CommitFailure { get; init; }
        public Exception? RollbackFailure { get; init; }
        public Exception? WarningReadFailure { get; init; }
        public int? RollbackWarning { get; init; }
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }
        public int WarningCheckCalls { get; private set; }
        public int WarningCheckTimeout { get; private set; }

        public Task BeginAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> ExecuteAsync(string sql, int timeoutSeconds, CancellationToken cancellationToken) =>
            CommandFailure == null ? Task.FromResult(3) : Task.FromException<int>(CommandFailure);

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitCalls++;
            return CommitFailure == null ? Task.CompletedTask : Task.FromException(CommitFailure);
        }

        public Task RollbackAsync()
        {
            RollbackCalls++;
            return RollbackFailure == null ? Task.CompletedTask : Task.FromException(RollbackFailure);
        }

        public Task<bool> HasRollbackWarningsAsync(int timeoutSeconds)
        {
            WarningCheckCalls++;
            WarningCheckTimeout = timeoutSeconds;
            return WarningReadFailure == null ? Task.FromResult(RollbackWarning.HasValue) : Task.FromException<bool>(WarningReadFailure);
        }
    }
}
