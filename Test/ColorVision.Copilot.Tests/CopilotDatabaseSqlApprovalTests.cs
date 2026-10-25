using ColorVision.Copilot.Mcp;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotDatabaseSqlApprovalTests
{
    private const string Sql = "UPDATE measurements SET reviewed=1 WHERE id=42";
    private const string TargetA = "server=synthetic-a;port=3306;database=inspection;user id=reviewer;password=synthetic-password-a";
    private const string TargetB = "server=synthetic-b;port=3306;database=inspection;user id=reviewer;password=synthetic-password-b";

    [Fact]
    public async Task NativeApprovalIncludesTheCompleteRedactedSqlAndTarget()
    {
        var executor = new BoundExecutor(TargetA);
        var tool = new CopilotExecuteDatabaseSqlTool(new CopilotDatabaseSqlService(executor));
        var sql = "UPDATE measurements SET reviewed=1 /* " + new string('x', 1600)
            + " token='synthetic-head;synthetic-tail' \u202E */ WHERE id=42";
        var input = Input(sql, 45);
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = coordinator.RequestApproval(tool, Request(), input, "sql-full-review", CancellationToken.None);
        try
        {
            Assert.DoesNotContain("WHERE id=42", handle.Action.Description, StringComparison.Ordinal);
            Assert.Contains("WHERE id=42", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("server: synthetic-a", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("database: inspection", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("timeoutSeconds: 45", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(@"\u202E", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-head", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-tail", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-password-a", JsonSerializer.Serialize(handle.Action), StringComparison.Ordinal);
            Assert.Equal(executor.CaptureTarget().Fingerprint, handle.Action.ExecutionTargetFingerprint);
            Assert.Equal(0, executor.Executions);
        }
        finally
        {
            coordinator.Cancel(handle);
            await handle.Decision;
        }
        Assert.Empty(handle.Action.ReviewDetails);
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("UPDATE measurements SET reviewed=1; DELETE FROM measurements")]
    public void InvalidWriteCannotCreatePendingApproval(string sql)
    {
        var tool = new CopilotExecuteDatabaseSqlTool(new CopilotDatabaseSqlService(new BoundExecutor(TargetA)));
        var callId = "invalid-sql-" + Guid.NewGuid().ToString("N");

        Assert.Throws<InvalidOperationException>(() => new CopilotFrameworkApprovalCoordinator().RequestApproval(
            tool, Request(), Input(sql), callId, CancellationToken.None));

        Assert.DoesNotContain(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => action.AgentCallId == callId);
    }

    [Fact]
    public async Task IdenticalSqlApprovalsKeepTheirOwnDatabaseTargetDuringExecution()
    {
        var executor = new BoundExecutor(TargetA);
        var service = new CopilotDatabaseSqlService(executor);
        var tool = new CopilotExecuteDatabaseSqlTool(service);
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var request = Request();
        var input = Input(Sql);
        var first = coordinator.RequestApproval(tool, request, input, "sql-target-a", CancellationToken.None);
        executor.ConnectionString = TargetB;
        var second = coordinator.RequestApproval(tool, request, input, "sql-target-b", CancellationToken.None);
        try
        {
            var reviewContext = new CopilotConfirmationReviewContext(request.ConversationId, request.TaskId, request.WorkspacePath);
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(first.Action.ActionId, reviewContext, out _));
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(second.Action.ActionId, reviewContext, out _));

            var rejected = await ExecuteWithActionAsync(tool, request, input, first.Action);
            Assert.False(rejected.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, rejected.FailureKind);
            Assert.Equal("database_target_changed", rejected.FailureCode);
            Assert.Equal(0, executor.Executions);

            var accepted = await ExecuteWithActionAsync(tool, request, input, second.Action);
            Assert.True(accepted.Success, accepted.ErrorMessage);
            Assert.Equal(1, executor.Executions);
            Assert.Equal(CopilotDatabaseSqlTarget.FromConnectionString(TargetB).Fingerprint, executor.ExecutedTarget);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.Cancel(first.Action.ActionId, out _, "SQL test cleanup");
            CopilotMcpConfirmationStore.Instance.Cancel(second.Action.ActionId, out _, "SQL test cleanup");
            await first.Decision;
            await second.Decision;
        }
    }

    [Fact]
    public async Task DriverRejectsTargetDriftBeforeOpeningAConnection()
    {
        var calls = 0;
        var executor = new CopilotMySqlDatabaseSqlExecutor(() => { calls++; return TargetB; });
        Assert.True(CopilotDatabaseSqlPolicy.TryAnalyze(Sql, out var analysis, out var error), error);

        await Assert.ThrowsAsync<CopilotDatabaseTargetChangedException>(() => executor.ExecuteBoundAsync(
            Sql, analysis!, 30, CopilotDatabaseSqlTarget.FromConnectionString(TargetA).Fingerprint, CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void TargetIdentityIncludesAccountAndDatabaseButExcludesPasswords()
    {
        var first = CopilotDatabaseSqlTarget.FromConnectionString(TargetA);
        var rotatedPassword = CopilotDatabaseSqlTarget.FromConnectionString(TargetA.Replace("synthetic-password-a", "synthetic-password-rotated", StringComparison.Ordinal));
        Assert.Equal(first.Fingerprint, rotatedPassword.Fingerprint);
        Assert.NotEqual(first.Fingerprint, CopilotDatabaseSqlTarget.FromConnectionString(TargetA.Replace("inspection", "other", StringComparison.Ordinal)).Fingerprint);
        Assert.NotEqual(first.Fingerprint, CopilotDatabaseSqlTarget.FromConnectionString(TargetA.Replace("reviewer", "another-user", StringComparison.Ordinal)).Fingerprint);
        Assert.DoesNotContain("synthetic-password", first.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidConnectionSettingsDoNotExposeCredentialFragments()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CopilotDatabaseSqlTarget.FromConnectionString(
            "server=synthetic-a;password=first;synthetic-credential-fragment"));

        Assert.DoesNotContain("synthetic-credential-fragment", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("password=first", exception.ToString(), StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> SensitiveSqlCases =>
    [
        ["UPDATE measurements SET `token`='synthetic-head''synthetic-tail' WHERE id=918273645",
            new[] { "synthetic-head", "synthetic-tail", "918273645" }],
        ["UPDATE measurements SET token='synthetic-head\\'synthetic-tail' WHERE id=918273645",
            new[] { "synthetic-head", "synthetic-tail", "918273645" }],
        ["INSERT INTO measurements (`token`, reviewed) VALUES ('synthetic-first', 918273645),\n('synthetic-second', 0xCAFEBABE),\n(X'CAFEF00D', B'10110110')",
            new[] { "synthetic-first", "synthetic-second", "918273645", "CAFEBABE", "CAFEF00D", "10110110" }],
        ["UPDATE measurements SET token=0xCAFEBABE, reviewed=-918273645.8765e+27 /* synthetic-block-secret */ WHERE id=.987654321 # synthetic-hash-secret\nAND reviewed=1 -- synthetic-dash-secret",
            new[] { "CAFEBABE", "918273645", "8765", "987654321", "synthetic-block-secret", "synthetic-hash-secret", "synthetic-dash-secret" }],
        ["UPDATE measurements SET token=0xCAFEBABE, reviewed=0b10110110",
            new[] { "CAFEBABE", "10110110" }],
        ["UPDATE measurements SET password=\"synthetic-double\"\"synthetic-tail\" WHERE id=918273645",
            new[] { "synthetic-double", "synthetic-tail", "918273645" }],
    ];

    [Theory]
    [MemberData(nameof(SensitiveSqlCases))]
    public async Task SensitiveSqlLiteralsAreHiddenAcrossApprovalSurfacesWithoutChangingTheExactBinding(string sql, string[] secrets)
    {
        var tool = new CopilotExecuteDatabaseSqlTool(new CopilotDatabaseSqlService(new BoundExecutor(TargetA)));
        var input = Input(sql);
        var exactBinding = CopilotAgentToolInputExactBinding.Create(input);
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = coordinator.RequestApproval(tool, Request(), input, "sensitive-sql-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        try
        {
            var action = handle.Action;
            var surfaces = new[] { action.Description, action.ReviewDetails, action.ArgumentsSummary,
                action.ReviewDisplayText, action.ConfirmActionPayloadJson, JsonSerializer.Serialize(action),
                CopilotMcpConfirmationDecision.BuildApprovalPrompt(action) };
            foreach (var surface in surfaces)
                foreach (var secret in secrets)
                    Assert.DoesNotContain(secret, surface, StringComparison.Ordinal);
            Assert.Contains("measurements", action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("<redacted>", action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(CopilotDatabaseSqlPolicy.CreateFingerprint(sql), action.ReviewDetails, StringComparison.Ordinal);
            Assert.Equal(exactBinding, CopilotAgentToolInputExactBinding.Create(input));
            foreach (var secret in secrets)
                Assert.Contains(secret, exactBinding, StringComparison.Ordinal);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exactBinding))).ToLowerInvariant(), action.ArgumentsDigest);
            var altered = Input(sql.Replace(secrets[0], "different-sensitive-value", StringComparison.Ordinal));
            Assert.NotEqual(action.ArgumentsDigest, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CopilotAgentToolInputExactBinding.Create(altered)))).ToLowerInvariant());
        }
        finally
        {
            coordinator.Cancel(handle);
            await handle.Decision;
        }
    }

    [Theory]
    [InlineData("UPDATE measurements SET token='synthetic-unterminated")]
    [InlineData("INSERT INTO measurements(token) VALUES ('synthetic-value'); DELETE FROM measurements")]
    public void UnanalyzableSqlCannotFallBackToRawApprovalText(string sql)
    {
        Assert.Equal("<SQL hidden: could not be safely analyzed>", CopilotDatabaseSqlPolicy.RedactForApproval(sql));
        Assert.DoesNotContain("synthetic-", CopilotToolApprovalArgumentFormatter.Create(Input(sql)), StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinarySqlKeepsItsLiteralAndCommentDisplay()
    {
        const string sql = "UPDATE measurements SET reviewed=1 /* measurement review */ WHERE id=42";
        Assert.Equal(sql, CopilotDatabaseSqlPolicy.RedactForApproval(sql));
        Assert.Contains("id=42", CopilotToolApprovalArgumentFormatter.Create(Input(sql)), StringComparison.Ordinal);
    }

    private static async Task<CopilotToolResult> ExecuteWithActionAsync(CopilotExecuteDatabaseSqlTool tool, CopilotAgentRequest request, CopilotAgentToolInput input, ConfirmableAction action)
    {
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        Assert.True(coordinator.BeginIfRequired(action.ActionId, request, request.WorkspacePath, action.ArgumentsDigest, action.AgentCallId));
        using var context = CopilotToolInvocationContext.Enter(new CopilotToolInvocation
        {
            Tool = tool, AgentRequest = request, ToolInput = input, CallId = action.AgentCallId,
            ApprovalActionId = action.ActionId, FrameworkApprovalGranted = true,
        });
        var result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(request, input, CancellationToken.None);
        coordinator.Complete(action.ActionId, result);
        return result;
    }

    private static CopilotAgentRequest Request() => new()
    {
        ConversationId = "sql-approval-tests", TaskId = "sql-approval-task", Mode = CopilotAgentMode.Auto,
        WorkspacePath = Path.GetFullPath(Path.GetTempPath()), UserText = "修改指定测量数据",
    };

    private static CopilotAgentToolInput Input(string sql, int timeout = 30) => new()
    {
        Arguments = new Dictionary<string, object?> { ["sql"] = sql, ["timeoutSeconds"] = timeout },
    };

    private sealed class BoundExecutor(string connectionString) : ICopilotBoundDatabaseSqlExecutor
    {
        public string ConnectionString { get; set; } = connectionString;
        public int Executions { get; private set; }
        public string ExecutedTarget { get; private set; } = string.Empty;
        public bool IsAvailable => true;
        public CopilotDatabaseSqlTarget CaptureTarget() => CopilotDatabaseSqlTarget.FromConnectionString(ConnectionString);
        public Task<CopilotDatabaseQueryResult> QueryAsync(string sql, int maxRows, int timeoutSeconds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CopilotDatabaseMutationResult> ExecuteAsync(string sql, CopilotDatabaseSqlAnalysis analysis, int timeoutSeconds, CancellationToken cancellationToken) =>
            ExecuteBoundAsync(sql, analysis, timeoutSeconds, string.Empty, cancellationToken);
        public Task<CopilotDatabaseMutationResult> ExecuteBoundAsync(string sql, CopilotDatabaseSqlAnalysis analysis, int timeoutSeconds, string expectedTargetFingerprint, CancellationToken cancellationToken)
        {
            CopilotDatabaseSqlTarget.EnsureMatches(ConnectionString, expectedTargetFingerprint);
            Executions++;
            ExecutedTarget = CaptureTarget().Fingerprint;
            return Task.FromResult(new CopilotDatabaseMutationResult(1, true));
        }
    }
}
