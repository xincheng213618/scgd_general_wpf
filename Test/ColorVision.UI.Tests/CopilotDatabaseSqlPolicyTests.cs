using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotDatabaseSqlPolicyTests
{
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
}
