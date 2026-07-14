using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotDatabaseSqlTests
{
    [Theory]
    [InlineData("SELECT 'a;b' AS value;", "SELECT", CopilotDatabaseSqlStatementKind.Query)]
    [InlineData("WITH current_rows AS (SELECT id FROM jobs) SELECT * FROM current_rows", "SELECT", CopilotDatabaseSqlStatementKind.Query)]
    [InlineData("WITH old_rows AS (SELECT id FROM jobs) DELETE FROM jobs WHERE id IN (SELECT id FROM old_rows)", "DELETE", CopilotDatabaseSqlStatementKind.DataChange)]
    [InlineData("TRUNCATE TABLE logs", "TRUNCATE", CopilotDatabaseSqlStatementKind.Definition)]
    public void PolicyClassifiesOneSupportedStatement(string sql, string keyword, CopilotDatabaseSqlStatementKind kind)
    {
        Assert.True(CopilotDatabaseSqlPolicy.TryAnalyze(sql, out var analysis, out var error), error);
        Assert.NotNull(analysis);
        Assert.Equal(keyword, analysis.RootKeyword);
        Assert.Equal(kind, analysis.Kind);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE jobs")]
    [InlineData("/*!50000 DROP TABLE jobs */ SELECT 1")]
    [InlineData("GRANT ALL ON *.* TO user")]
    [InlineData("SET GLOBAL local_infile = 1")]
    [InlineData("SELECT * FROM jobs INTO OUTFILE 'c:/jobs.txt'")]
    [InlineData("SELECT LOAD_FILE('c:/secret.txt')")]
    [InlineData("DROP DATABASE production")]
    [InlineData("CREATE USER demo IDENTIFIED BY 'secret'")]
    [InlineData("RENAME USER demo TO admin")]
    [InlineData("ALTER INSTANCE ROTATE INNODB MASTER KEY")]
    [InlineData("CREATE EVENT cleanup_event ON SCHEDULE EVERY 1 DAY DO DELETE FROM logs")]
    [InlineData("ALTER TABLE jobs IMPORT TABLESPACE")]
    public void PolicyRejectsMultiStatementAdministrativeAndFileSql(string sql)
    {
        Assert.False(CopilotDatabaseSqlPolicy.TryAnalyze(sql, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("DELETE FROM jobs", true, true)]
    [InlineData("UPDATE jobs SET state = 0", true, true)]
    [InlineData("DELETE FROM jobs WHERE id = 1", false, false)]
    [InlineData("TRUNCATE TABLE jobs", true, false)]
    [InlineData("DROP TABLE jobs", true, false)]
    public void PolicyIdentifiesDestructiveAndUnboundedChanges(string sql, bool destructive, bool unbounded)
    {
        Assert.True(CopilotDatabaseSqlPolicy.TryAnalyze(sql, out var analysis, out var error), error);
        Assert.Equal(destructive, analysis!.IsDestructive);
        Assert.Equal(unbounded, analysis.IsUnboundedChange);
    }

    [Fact]
    public void ToolSchemasRejectMissingOversizedAndUnknownInputsBeforeExecution()
    {
        var query = new CopilotQueryDatabaseSqlTool(new CopilotDatabaseSqlService(new FakeExecutor()));
        Assert.False(query.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out var missingError));
        Assert.Contains("argument 'sql' is missing", missingError, StringComparison.OrdinalIgnoreCase);
        Assert.False(query.InputSchema.TryBind(new Dictionary<string, object?> { ["sql"] = string.Empty }, out _, out var emptyError));
        Assert.Contains("argument 'sql' is missing", emptyError, StringComparison.OrdinalIgnoreCase);
        Assert.False(query.InputSchema.TryBind(new Dictionary<string, object?> { ["sql"] = new string('x', CopilotDatabaseSqlPolicy.MaximumSqlLength + 1) }, out _, out var lengthError));
        Assert.Contains("at most", lengthError, StringComparison.Ordinal);
        Assert.False(query.InputSchema.TryBind(new Dictionary<string, object?> { ["sql"] = "SELECT 1", ["connectionString"] = "secret" }, out _, out var unknownError));
        Assert.Contains("Unknown argument", unknownError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryToolAcceptsOnlyReadsAndReturnsBoundedRedactedRows()
    {
        var executor = new FakeExecutor
        {
            QueryResult = new CopilotDatabaseQueryResult(
                ["id", "api_token", "note"],
                [["1", "plain-secret", "password=hunter2\nnext"]],
                true),
        };
        var service = new CopilotDatabaseSqlService(executor);

        var result = await service.QueryAsync(Input("SELECT id, api_token, note FROM users", ("maxRows", 25)), CancellationToken.None);
        var rejected = await service.QueryAsync(Input("DELETE FROM users"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, executor.QueryCount);
        Assert.Equal(25, executor.MaximumRows);
        Assert.Contains("api_token", result.Content, StringComparison.Ordinal);
        Assert.Contains("<redacted>", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-secret", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.Content, StringComparison.Ordinal);
        Assert.Contains("truncated: true", result.Content, StringComparison.Ordinal);
        Assert.False(rejected.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, rejected.FailureKind);
        Assert.Equal(1, executor.QueryCount);
    }

    [Fact]
    public async Task QueryRedactsAllValuesWhenSensitiveIdentifierIsAliasedOrQuoted()
    {
        var executor = new FakeExecutor
        {
            QueryResult = new CopilotDatabaseQueryResult(["harmless_name"], [["raw-value"]], false),
        };
        var service = new CopilotDatabaseSqlService(executor);

        var result = await service.QueryAsync(Input("SELECT `password` AS harmless_name FROM users"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("<redacted>", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-value", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MutationToolRequiresApprovalAndApprovedCallExecutesExactSqlOnce()
    {
        var executor = new FakeExecutor { MutationResult = new CopilotDatabaseMutationResult(12, true) };
        var service = new CopilotDatabaseSqlService(executor);
        var tool = new CopilotExecuteDatabaseSqlTool(service);
        var request = Request("执行 SQL 清理旧记录");
        var input = Input("DELETE FROM logs WHERE create_date < '2026-01-01'");

        var denied = await tool.ExecuteAsync(request, input, CancellationToken.None);
        var approved = await tool.ExecuteApprovedAsync(request, input, CancellationToken.None);

        Assert.False(denied.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, denied.FailureKind);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal("DELETE FROM logs WHERE create_date < '2026-01-01'", executor.ExecutedSql);
        Assert.True(approved.Success);
        Assert.Contains("affected_rows: 12", approved.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UPDATE t_scgd_mod_param_master SET is_enable = 0 WHERE id = 1")]
    [InlineData("DELETE FROM `t_scgd_buz_product_detail` WHERE id = 1")]
    [InlineData("TRUNCATE TABLE t_scgd_algorithm_poi_template_detail")]
    [InlineData("DROP TABLE color_vision.t_scgd_mod_param_detail")]
    public async Task MutationToolRejectsVersionManagedServiceSettingTables(string sql)
    {
        var executor = new FakeExecutor();
        var service = new CopilotDatabaseSqlService(executor);

        var result = await service.ExecuteApprovedAsync(Input(sql), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
        Assert.Contains("version-managed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, executor.ExecuteCount);
    }

    [Fact]
    public async Task QueryToolCanReadVersionManagedServiceSettingTables()
    {
        var executor = new FakeExecutor();
        var service = new CopilotDatabaseSqlService(executor);

        var result = await service.QueryAsync(Input("SELECT id, name FROM t_scgd_mod_param_master LIMIT 10"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, executor.QueryCount);
    }

    [Fact]
    public void ApprovalPresentationIncludesFingerprintWarningAndRedactedSql()
    {
        var tool = new CopilotExecuteDatabaseSqlTool(new CopilotDatabaseSqlService(new FakeExecutor()));

        var presentation = tool.CreateApprovalPresentation(Input("DELETE FROM users WHERE password='secret-value'"));
        var unbounded = tool.CreateApprovalPresentation(Input("DELETE FROM users"));

        Assert.Contains("Fingerprint", presentation.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", presentation.Description, StringComparison.Ordinal);
        Assert.Contains("<redacted>", presentation.Description, StringComparison.Ordinal);
        Assert.Contains("no top-level WHERE", unbounded.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolIntentSeparatesQueriesChangesAndConceptualQuestions()
    {
        var registry = new CopilotToolRegistry([
            new CopilotQueryDatabaseSqlTool(new CopilotDatabaseSqlService(new FakeExecutor())),
            new CopilotExecuteDatabaseSqlTool(new CopilotDatabaseSqlService(new FakeExecutor())),
        ]);

        Assert.Contains(registry.FindTools(Request("查询数据库，执行 SELECT COUNT(*) FROM jobs")), tool => tool.Name == "QueryDatabaseSql");
        Assert.Contains(registry.FindTools(Request("数据库里现在数据有多少")), tool => tool.Name == "QueryDatabaseSql");
        Assert.DoesNotContain(registry.FindTools(Request("解释一下畸变校正")), tool => tool.Name == "QueryDatabaseSql");
        Assert.DoesNotContain(registry.FindTools(Request("执行 SQL：SELECT COUNT(*) FROM jobs")), tool => tool.Name == "ExecuteDatabaseSql");
        Assert.Contains(registry.FindTools(Request("清理数据库，DELETE FROM jobs WHERE id < 10")), tool => tool.Name == "ExecuteDatabaseSql");
        Assert.Contains(registry.FindTools(Request("WITH old AS (SELECT id FROM jobs) DELETE FROM jobs WHERE id IN (SELECT id FROM old)")), tool => tool.Name == "ExecuteDatabaseSql");
        Assert.DoesNotContain(registry.FindTools(Request("SQL 是什么，如何写 SQL")), tool => tool.Name == "QueryDatabaseSql");
        Assert.DoesNotContain(registry.FindTools(Request("SQL 是什么，如何写 SQL")), tool => tool.Name == "ExecuteDatabaseSql");
    }

    private static CopilotAgentToolInput Input(string sql, params (string Name, object Value)[] additional)
    {
        var arguments = new Dictionary<string, object?> { ["sql"] = JsonSerializer.SerializeToElement(sql) };
        foreach (var (name, value) in additional)
            arguments[name] = JsonSerializer.SerializeToElement(value);
        return new CopilotAgentToolInput { Arguments = arguments };
    }

    private static CopilotAgentRequest Request(string text) => new() { UserText = text, Mode = CopilotAgentMode.Auto };

    private sealed class FakeExecutor : ICopilotDatabaseSqlExecutor
    {
        public bool IsAvailable { get; init; } = true;

        public CopilotDatabaseQueryResult QueryResult { get; init; } = new([], [], false);

        public CopilotDatabaseMutationResult MutationResult { get; init; } = new(0, true);

        public int QueryCount { get; private set; }

        public int ExecuteCount { get; private set; }

        public int MaximumRows { get; private set; }

        public string ExecutedSql { get; private set; } = string.Empty;

        public Task<CopilotDatabaseQueryResult> QueryAsync(string sql, int maxRows, int timeoutSeconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryCount++;
            MaximumRows = maxRows;
            return Task.FromResult(QueryResult);
        }

        public Task<CopilotDatabaseMutationResult> ExecuteAsync(string sql, CopilotDatabaseSqlAnalysis analysis, int timeoutSeconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            ExecutedSql = sql;
            return Task.FromResult(MutationResult);
        }
    }
}
