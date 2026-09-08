using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalReadFollowUpAdmissionTests
{
    private static readonly string[] ToolNames = ["ReadLocalFile", "ListDirectory", "SearchFiles", "GrepText"];

    public static IEnumerable<object[]> Continuations =>
        from toolName in ToolNames
        from successful in new[] { true, false }
        from readOnlySandbox in new[] { true, false }
        select new object[] { toolName, successful, readOnlySandbox };

    [Theory]
    [MemberData(nameof(Continuations))]
    public void RecentWorkspaceReadRemainsAvailableForAReadOnlyContinuation(
        string toolName, bool successful, bool readOnlySandbox)
    {
        using var fixture = new WorkspaceFixture();
        var request = fixture.CreateRequest(
            fixture.CreateCheckpoint([toolName], successful),
            prompt: readOnlySandbox ? "继续" : "继续，只分析",
            readOnlySandbox: readOnlySandbox);

        var available = fixture.Registry.FindTools(request);

        Assert.Contains(available, tool => tool.Name == toolName);
        Assert.All(available, tool => Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access));
    }

    [Theory]
    [InlineData("ReadLocalFile")]
    [InlineData("ListDirectory")]
    [InlineData("SearchFiles")]
    [InlineData("GrepText")]
    public void ContinuationDoesNotInventAReadLeaseOrBypassCurrentAdmission(string toolName)
    {
        using var fixture = new WorkspaceFixture();
        var checkpoint = fixture.CreateCheckpoint([toolName]);
        var unrelatedCheckpoint = fixture.CreateCheckpoint(["InspectApplicationState"]);
        var expiredCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = checkpoint.ProfileKey,
            SerializedSessionJson = checkpoint.SerializedSessionJson,
            TaskEventJournal = checkpoint.TaskEventJournal,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
        };
        Assert.True(expiredCheckpoint.IsStructurallyValid());

        var requests = new (string Reason, CopilotAgentRequest Request)[]
        {
            ("no checkpoint", fixture.CreateRequest(null)),
            ("no history", fixture.CreateRequest(checkpoint, includeHistory: false)),
            ("current readable roots removed", fixture.CreateRequest(checkpoint, includeRoots: false)),
            ("only an unrelated tool was used", fixture.CreateRequest(unrelatedCheckpoint)),
            ("expired checkpoint", fixture.CreateRequest(expiredCheckpoint)),
            ("new topic", fixture.CreateRequest(checkpoint, prompt: "换个话题：解释一下这个概念")),
            ("delegated workspace evidence", fixture.CreateRequest(checkpoint, delegated: true)),
            ("Chat mode", fixture.CreateRequest(checkpoint, mode: CopilotAgentMode.Chat)),
        };

        foreach (var (reason, request) in requests)
        {
            Assert.False(fixture.Registry.FindTools(request).Any(tool => tool.Name == toolName),
                $"{toolName} must remain unavailable with {reason}.");
        }
    }

    [Fact]
    public void AReadInAnOlderRunDoesNotLeaseTheToolForTheLatestRun()
    {
        using var fixture = new WorkspaceFixture();
        var first = fixture.CreateCheckpoint(ToolNames);
        var latest = fixture.CreateCheckpoint([], previous: first.TaskEventJournal);

        Assert.Empty(fixture.Registry.FindTools(fixture.CreateRequest(latest)));
    }

    [Fact]
    public void ExplicitSingleFileScopeStillSuppressesDirectoryDiscovery()
    {
        using var fixture = new WorkspaceFixture();
        var request = fixture.CreateRequest(fixture.CreateCheckpoint(ToolNames), exactFile: fixture.FilePath);

        var names = fixture.Registry.FindTools(request).Select(tool => tool.Name).ToArray();

        Assert.Contains("ReadLocalFile", names);
        Assert.Contains("GrepText", names);
        Assert.DoesNotContain("ListDirectory", names);
        Assert.DoesNotContain("SearchFiles", names);
    }

    private sealed class WorkspaceFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"copilot-read-follow-up-{Guid.NewGuid():N}");
        private readonly CopilotProfileConfig _profile = new()
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            BaseUrl = "https://example.test/v1",
            ApiKey = "read-follow-up-test-key",
            Model = "test-model",
        };

        public WorkspaceFixture()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(FilePath, "A locally scoped follow-up fixture.");
        }

        public string FilePath => Path.Combine(_root, "sample.txt");

        public CopilotToolRegistry Registry { get; } = new([
            new CopilotReadLocalFileTool(),
            new CopilotListDirectoryTool(),
            new CopilotSearchFilesTool(),
            new CopilotGrepTextTool(),
        ]);

        public CopilotAgentRequest CreateRequest(
            CopilotAgentSessionCheckpoint? checkpoint,
            string prompt = "继续",
            bool readOnlySandbox = true,
            bool includeHistory = true,
            bool includeRoots = true,
            bool delegated = false,
            CopilotAgentMode mode = CopilotAgentMode.Auto,
            string? exactFile = null) => new()
        {
            Profile = _profile,
            UserText = prompt,
            Mode = mode,
            CodexSandboxMode = readOnlySandbox ? CopilotCodexSandboxMode.ReadOnly : CopilotCodexSandboxMode.WorkspaceWrite,
            SearchRootPaths = includeRoots ? [_root] : [],
            ReadableLocalFilePaths = exactFile == null ? [] : [exactFile],
            WritableLocalRootPaths = readOnlySandbox ? [] : [_root],
            History = includeHistory
                ? [new CopilotRequestMessage("user", "检查工作区"), new CopilotRequestMessage("assistant", "已取得第一批读取结果。")]
                : [],
            SessionCheckpoint = checkpoint,
            RequiresDelegatedWorkspaceEvidence = delegated,
        };

        public CopilotAgentSessionCheckpoint CreateCheckpoint(
            IReadOnlyList<string> toolNames,
            bool successful = true,
            CopilotAgentTaskEventJournalSnapshot? previous = null)
        {
            var journal = new CopilotAgentTaskEventJournalBuilder(previous);
            journal.RecordRunStarted();
            foreach (var toolName in toolNames)
            {
                var callId = $"read-follow-up-{toolName}";
                journal.Observe(CopilotAgentEvent.ToolStarted(Execution(toolName, callId, CopilotToolExecutionState.Running)));
                journal.Observe(CopilotAgentEvent.FromToolResult(new CopilotToolResult
                {
                    ToolName = toolName,
                    Success = successful,
                    Summary = successful ? "The bounded read completed." : "The bounded read failed before returning content.",
                    FailureKind = successful ? CopilotToolFailureKind.None : CopilotToolFailureKind.Validation,
                }, Execution(toolName, callId, successful ? CopilotToolExecutionState.Completed : CopilotToolExecutionState.Failed)));
            }
            journal.RecordStop(CopilotAgentStopReason.Completed);
            var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
                _profile, "{}", taskEventJournal: journal.Snapshot(), availableToolNames: ToolNames));
            Assert.True(checkpoint.IsStructurallyValid());
            return checkpoint;
        }

        private static CopilotToolExecutionInfo Execution(string toolName, string callId, CopilotToolExecutionState state) => new()
        {
            ToolName = toolName,
            CallId = callId,
            Round = 1,
            RuntimeName = "LocalReadFollowUpAdmissionTests",
            Access = CopilotToolAccess.ReadOnly,
            Idempotency = CopilotToolIdempotency.Idempotent,
            State = state,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        public void Dispose()
        {
            File.Delete(FilePath);
            Directory.Delete(_root);
        }
    }
}
