using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDatabaseSqlPolicyTests
{
    [Theory]
    [InlineData(CopilotAgentMode.Auto, "查询数据库今天记录数，并解释 SQL。", true)]
    [InlineData(CopilotAgentMode.Auto, "Query today's database row count, then explain SQL.", true)]
    [InlineData(CopilotAgentMode.Chat, "查询数据库今天记录数，并解释 SQL。", false)]
    public void QueryAndExplanationKeepDatabaseReadAvailable(CopilotAgentMode mode, string prompt, bool expectedAvailable)
    {
        var read = new CopilotQueryDatabaseSqlTool();
        var write = new CopilotExecuteDatabaseSqlTool();
        var registry = new CopilotToolRegistry([read, write]);
        var request = new CopilotAgentRequest { Mode = mode, UserText = prompt };

        var registered = registry.FindTools(request);
        var merged = CopilotMicrosoftAgentFrameworkRuntime.MergeAvailableTools(request, registered, [], _ => { });

        Assert.Equal(expectedAvailable, registered.Contains(read));
        Assert.Equal(expectedAvailable, merged.Contains(read));
        Assert.DoesNotContain(write, registered);
        Assert.DoesNotContain(write, merged);
    }

    [Fact]
    public async Task AvailableDatabaseWriteStillRequiresNativeApproval()
    {
        var write = new CopilotExecuteDatabaseSqlTool();
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, UserText = "更新数据库中的测试记录。" };
        Assert.Contains(write, new CopilotToolRegistry([write]).FindTools(request));
        Assert.True(write.Capability.RequiresNativeApproval);

        var result = await write.ExecuteAsync(request, new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["sql"] = "UPDATE measurements SET reviewed = 1 WHERE id = 42" },
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
    }

    [Theory]
    [InlineData("SELECT id FROM users FOR UPDATE")]
    [InlineData("SELECT id FROM users FOR SHARE")]
    [InlineData("SELECT id FROM users LOCK IN SHARE MODE")]
    public void ReadOnlyQueriesRejectRowLockingStatements(string sql)
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(sql, out var analysis, out var error);

        Assert.False(accepted);
        Assert.Null(analysis);
        Assert.Contains("Row-locking", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyQueriesRejectVariableIntoTargets()
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(
            "SELECT id INTO @user_id FROM users",
            out var analysis,
            out var error);

        Assert.False(accepted);
        Assert.Null(analysis);
        Assert.Contains("SELECT INTO", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EXPLAIN ANALYZE SELECT * FROM users")]
    [InlineData("DESCRIBE ANALYZE UPDATE users SET enabled = 0")]
    [InlineData("DESC ANALYZE DELETE FROM users")]
    public void ReadOnlyQueriesRejectExplainAnalyzeBecauseItExecutesTheTarget(string sql)
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(sql, out var analysis, out var error);

        Assert.False(accepted);
        Assert.Null(analysis);
        Assert.Contains("executes its target statement", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainStillAllowsAColumnNamedAnalyze()
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(
            "EXPLAIN SELECT analyze FROM users",
            out var analysis,
            out var error);

        Assert.True(accepted, error);
        Assert.NotNull(analysis);
        Assert.Equal(CopilotDatabaseSqlStatementKind.Query, analysis.Kind);
    }

    [Theory]
    [InlineData("SELECT @user_id := id FROM users")]
    [InlineData("SELECT id FROM users WHERE id = GET_LOCK('copilot', 1)")]
    [InlineData("SELECT LAST_INSERT_ID(42)")]
    public void ReadOnlyQueriesRejectSessionStateSideEffects(string sql)
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(sql, out var analysis, out var error);

        Assert.False(accepted);
        Assert.Null(analysis);
        Assert.Contains("not available", error, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotedTextAndCommentsDoNotTriggerLockOrIntoRejection()
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(
            "SELECT 'FOR UPDATE' AS note /* INTO is data */ FROM users",
            out var analysis,
            out var error);

        Assert.True(accepted, error);
        Assert.NotNull(analysis);
        Assert.Equal(CopilotDatabaseSqlStatementKind.Query, analysis.Kind);
    }

    [Fact]
    public void QuotedTextAndCommentsDoNotTriggerAssignmentRejection()
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(
            "SELECT 'id := value' AS note /* GET_LOCK should stay text */ FROM users",
            out var analysis,
            out var error);

        Assert.True(accepted, error);
        Assert.NotNull(analysis);
        Assert.Equal(CopilotDatabaseSqlStatementKind.Query, analysis.Kind);
    }

    [Fact]
    public void SideEffectFunctionNamesUsedAsColumnsRemainReadable()
    {
        var accepted = CopilotDatabaseSqlPolicy.TryAnalyze(
            "SELECT GET_LOCK, `LAST_INSERT_ID` FROM users",
            out var analysis,
            out var error);

        Assert.True(accepted, error);
        Assert.NotNull(analysis);
        Assert.Equal(CopilotDatabaseSqlStatementKind.Query, analysis.Kind);
    }

    [Fact]
    public async Task AmbiguousMutationFailureRequiresStateVerificationBeforeRetry()
    {
        const string sql = "UPDATE measurements SET reviewed = 1 WHERE id = 42";
        var service = new CopilotDatabaseSqlService(new OutcomeUnknownExecutor());
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["sql"] = sql },
        };

        var result = await service.ExecuteApprovedAsync(input, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, result.FailureKind);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, result.FailureCode);
        Assert.Contains(CopilotDatabaseSqlPolicy.CreateFingerprint(sql), result.Summary, StringComparison.Ordinal);
        Assert.Contains("Do not retry", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task CompleteQueryOutputReportsDisplayedRowsAndPreservesRedaction(int rowCount)
    {
        IReadOnlyList<string>[] rows = Enumerable.Range(0, rowCount)
            .Select(index => (IReadOnlyList<string>)[index.ToString(), "first\nsecond\tlast", "must-not-be-disclosed"])
            .ToArray();

        var result = await QueryAsync(new CopilotDatabaseQueryResult(["id", "note", "password"], rows, false));

        Assert.True(result.Success);
        Assert.Contains($"read {rowCount} row(s), displayed {rowCount} row(s).", result.Summary, StringComparison.Ordinal);
        Assert.Contains($"rows_returned: {rowCount}", result.Content, StringComparison.Ordinal);
        Assert.Contains($"rows_read: {rowCount}", result.Content, StringComparison.Ordinal);
        Assert.Contains($"rows_displayed: {rowCount}", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncated: false", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncation_reasons: none", result.Content, StringComparison.Ordinal);
        Assert.Empty(result.PartialResultMessage);
        Assert.DoesNotContain("must-not-be-disclosed", result.Content, StringComparison.Ordinal);
        if (rowCount > 0)
            Assert.Contains("first second last\t<redacted>", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryOutputBudgetReportsOnlyCompleteDisplayedRowsInSummaryAndMetadata()
    {
        var cell = new string('x', 512);
        IReadOnlyList<string>[] rows = Enumerable.Range(0, 100)
            .Select(_ => (IReadOnlyList<string>)[cell])
            .ToArray();

        var result = await QueryAsync(new CopilotDatabaseQueryResult(["data"], rows, false));

        var data = result.Content.Split("data_tsv:" + Environment.NewLine, StringSplitOptions.None)[1]
            .Split(Environment.NewLine, StringSplitOptions.None).Skip(1).SkipLast(1).ToArray();
        Assert.NotEmpty(data);
        Assert.True(data.Length < rows.Length);
        Assert.All(data, line => Assert.Equal(cell, line));
        Assert.True(result.Content.Length <= 32_000);
        Assert.Contains("rows_returned: 100", result.Content, StringComparison.Ordinal);
        Assert.Contains("rows_read: 100", result.Content, StringComparison.Ordinal);
        Assert.Contains($"rows_displayed: {data.Length}", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncated: true", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncation_reasons: output_budget", result.Content, StringComparison.Ordinal);
        Assert.Contains($"read 100 row(s), displayed {data.Length} row(s) (truncated).", result.Summary, StringComparison.Ordinal);
        Assert.NotEmpty(result.PartialResultMessage);
        var captured = CopilotToolResultContract.Capture(result.ToolName, result);
        Assert.True(captured.Success);
        Assert.Equal(result.PartialResultMessage, CopilotToolObservation.FromResult(captured).PartialResultMessage);
    }

    [Theory]
    [InlineData(512, false)]
    [InlineData(513, true)]
    public async Task QueryCellLimitIsReportedWithoutLosingTheDisplayedRow(int length, bool truncated)
    {
        var cell = new string('x', length);

        var result = await QueryAsync(new CopilotDatabaseQueryResult(["data"], [[cell]], false));

        Assert.Contains("rows_read: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains("rows_displayed: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains($"cells_truncated: {(truncated ? 1 : 0)}", result.Content, StringComparison.Ordinal);
        Assert.Contains($"truncated: {truncated.ToString().ToLowerInvariant()}", result.Content, StringComparison.Ordinal);
        Assert.Contains($"truncation_reasons: {(truncated ? "cell_limit" : "none")}", result.Content, StringComparison.Ordinal);
        Assert.Equal(truncated ? new string('x', 511) + "…" : cell, result.Content.Split(Environment.NewLine)[^2]);
        Assert.Equal(truncated, result.Summary.Contains("(truncated)", StringComparison.Ordinal));
        Assert.Equal(truncated, !string.IsNullOrEmpty(result.PartialResultMessage));
    }

    [Fact]
    public async Task QueryCellTruncationDoesNotSplitASurrogatePair()
    {
        var prefix = new string('x', 510);
        var result = await QueryAsync(new CopilotDatabaseQueryResult(["data"], [[prefix + "😀z"]], false));

        Assert.Equal(prefix + "…", result.Content.Split(Environment.NewLine)[^2]);
        Assert.Contains("cells_truncated: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncation_reasons: cell_limit", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, 0, "row_limit")]
    [InlineData(false, true, 0, "column_limit")]
    [InlineData(false, false, 1, "cell_limit")]
    [InlineData(false, false, 0, "query_result")]
    public async Task QueryOutputPreservesSourceTruncationReasons(bool rowsTruncated, bool columnsTruncated, int cellCount, string reason)
    {
        var cell = new string('x', 511) + "…";
        var source = new CopilotDatabaseQueryResult(["data"], [[cell]], true)
        {
            RowsTruncated = rowsTruncated,
            ColumnsTruncated = columnsTruncated,
            TruncatedCellCount = cellCount,
        };

        var result = await QueryAsync(source);

        Assert.Contains("rows_displayed: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains($"cells_truncated: {cellCount}", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncated: true", result.Content, StringComparison.Ordinal);
        Assert.Contains($"truncation_reasons: {reason}", result.Content, StringComparison.Ordinal);
        Assert.Contains("read 1 row(s), displayed 1 row(s) (truncated).", result.Summary, StringComparison.Ordinal);
        Assert.NotEmpty(result.PartialResultMessage);
    }

    [Fact]
    public async Task QueryOutputBudgetIncludesMetadataAndColumnHeaders()
    {
        var result = await QueryAsync(new CopilotDatabaseQueryResult([new string('h', 31_900)], [], false));

        Assert.True(result.Content.Length <= 32_000);
        Assert.Contains("rows_displayed: 0", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncation_reasons: output_budget", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("data_tsv:", result.Content, StringComparison.Ordinal);
        Assert.Contains("(truncated)", result.Summary, StringComparison.Ordinal);
    }

    private static Task<CopilotToolResult> QueryAsync(CopilotDatabaseQueryResult queryResult)
    {
        var service = new CopilotDatabaseSqlService(new QueryResultExecutor(queryResult));
        return service.QueryAsync(new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["sql"] = "SELECT id FROM measurements" },
        }, CancellationToken.None);
    }

    private sealed class QueryResultExecutor(CopilotDatabaseQueryResult result) : ICopilotDatabaseSqlExecutor
    {
        public bool IsAvailable => true;

        public Task<CopilotDatabaseQueryResult> QueryAsync(string sql, int maxRows, int timeoutSeconds, CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<CopilotDatabaseMutationResult> ExecuteAsync(string sql, CopilotDatabaseSqlAnalysis analysis, int timeoutSeconds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OutcomeUnknownExecutor : ICopilotDatabaseSqlExecutor
    {
        public bool IsAvailable => true;

        public Task<CopilotDatabaseQueryResult> QueryAsync(
            string sql,
            int maxRows,
            int timeoutSeconds,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CopilotDatabaseMutationResult> ExecuteAsync(
            string sql,
            CopilotDatabaseSqlAnalysis analysis,
            int timeoutSeconds,
            CancellationToken cancellationToken) => Task.FromException<CopilotDatabaseMutationResult>(
                new CopilotDatabaseMutationOutcomeUnknownException(new IOException("commit acknowledgement lost")));
    }
}
