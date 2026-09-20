using System.Diagnostics;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests.Evaluation;

public sealed class CopilotLiveEvaluationAttribute : FactAttribute
{
    public CopilotLiveEvaluationAttribute()
    {
        if (Environment.GetEnvironmentVariable("COLORVISION_COPILOT_EVAL_ENABLED") != "1")
            Skip = "Explicit opt-in required: Scripts/evaluate_copilot.ps1 uses a paid model API.";
    }
}

public sealed class CopilotBusinessEvaluationTests
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };

    [CopilotLiveEvaluation]
    [Trait("Category", "LiveCopilotEvaluation")]
    public async Task EvaluateConfiguredProfile()
    {
        var selection = RequiredEnvironment("COLORVISION_COPILOT_EVAL_CASES").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var cases = SelectCases(selection);
        Assert.NotEmpty(cases);
        var output = Path.GetFullPath(RequiredEnvironment("COLORVISION_COPILOT_EVAL_OUTPUT"));
        Directory.CreateDirectory(output);
        var reportPath = Path.Combine(output, "report.json");
        Assert.False(File.Exists(reportPath), "Use a fresh output directory; evaluation reports are not overwritten.");
        var profile = LoadSelectedProfile();
        var timeout = ReadBoundedInteger("COLORVISION_COPILOT_EVAL_TIMEOUT", 120, 30, 300);
        var tokenBudget = ReadBoundedInteger("COLORVISION_COPILOT_EVAL_TOKEN_BUDGET", 98304, 8192, 262144);
        var repetitions = ReadBoundedInteger("COLORVISION_COPILOT_EVAL_REPETITIONS", 1, 1, 10);
        var reports = new List<CaseReport>();
        var started = DateTimeOffset.UtcNow;
        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            // Each repetition owns fresh workspaces and checkpoints. Only the
            // explicitly linked turns inside that repetition share a conversation.
            var conversations = new Dictionary<string, EvaluationConversation>(StringComparer.Ordinal);
            var repetitionOutput = repetitions == 1 ? output : Path.Combine(output, $"repeat-{repetition:D2}");
            foreach (var scenario in cases)
            {
                var conversation = scenario.ContinueAfter is { } previousId
                    ? conversations[previousId]
                    : new EvaluationConversation(Path.Combine(repetitionOutput, scenario.Id, "workspace"));
                reports.Add(await RunCaseAsync(scenario, profile, repetitionOutput, timeout, tokenBudget, conversation, repetition));
                conversations.Add(scenario.Id, conversation);
                // Write after every case so a later interrupted run retains completed evidence.
                var report = new
                {
                    SchemaVersion = 6,
                    Scope = "live provider, synthetic business exports, production file tools; no hardware/database/UI acceptance",
                    StartedAtUtc = started,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Profile = new { profile.Name, profile.Model, Provider = profile.ProviderType.ToString(), profile.MaxTokens, Reasoning = profile.ReasoningMode.ToString() },
                    Settings = new { TimeoutSecondsPerCase = timeout, RequestTokenBudgetPerCase = tokenBudget, MaxToolCallsPerCase = 12, Repetitions = repetitions },
                    PlannedCases = cases.Count * repetitions,
                    CompletedCases = reports.Count,
                    PassedCases = reports.Count(r => r.Failures.Count == 0),
                    ReportedTokens = reports.Sum(r => (long)r.Budget.ReportedTotalTokens),
                    EstimatedBudgetTokens = reports.Sum(r => r.Budget.ConsumedTokens),
                    ToolCalls = reports.Sum(r => r.ToolCalls),
                    FailedToolCalls = reports.Sum(r => r.FailedToolCalls),
                    HumanInputRequests = reports.Sum(r => r.HumanInputRequests),
                    Cases = reports,
                };
                var pending = reportPath + ".tmp";
                await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(report, ReportJson));
                File.Move(pending, reportPath, overwrite: true);
            }
        }
        Assert.True(reports.All(r => r.Failures.Count == 0),
            $"{reports.Count(r => r.Failures.Count == 0)}/{reports.Count} scenarios passed. Evidence: {reportPath}");
    }

    internal static IReadOnlyList<CopilotBusinessScenario> SelectCases(IReadOnlyList<string> selection)
    {
        if (selection.SequenceEqual(["all"])) return CopilotBusinessScenarios.All;
        var selected = new List<CopilotBusinessScenario>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in selection) Add(id);
        return selected;

        void Add(string id)
        {
            if (!visited.Add(id)) return;
            var scenario = CopilotBusinessScenarios.All.Single(c => c.Id == id);
            if (scenario.ContinueAfter is { } previousId) Add(previousId);
            selected.Add(scenario);
        }
    }

    private static async Task<CaseReport> RunCaseAsync(CopilotBusinessScenario scenario, CopilotProfileConfig profile, string output, int timeout, int tokenBudget, EvaluationConversation conversation, int repetition)
    {
        var caseDirectory = Path.Combine(output, scenario.Id);
        var workspace = conversation.Workspace;
        Assert.False(Directory.Exists(caseDirectory), "Scenario directories must be fresh.");
        Directory.CreateDirectory(caseDirectory);
        Directory.CreateDirectory(workspace);
        using var solutionScope = new EvaluationWorkspaceScope(workspace);
        foreach (var (name, content) in scenario.Files)
        {
            var path = ResolveScenarioFilePath(workspace, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content, ResolveFileEncoding(scenario, name));
            conversation.SourceFiles.Add(path);
        }
        if (scenario.ContinueAfter == null)
            await File.WriteAllTextAsync(Path.Combine(workspace, "guard.txt"), "KEEP-ORIGINAL-7341\n");
        var hashes = CaptureFileHashes(workspace);
        var expectedHashes = new Dictionary<string, string>(hashes, StringComparer.OrdinalIgnoreCase);
        var patchStore = new CopilotWorkspacePatchStore();
        ICopilotTool[] tools =
        [
            new CopilotReadLocalFileTool(), new CopilotListDirectoryTool(),
            new CopilotSearchFilesTool(), new CopilotGrepTextTool(),
            new CopilotPreviewWorkspacePatchEnvelopeTool(patchStore),
            new CopilotApplyWorkspacePatchEnvelopeTool(patchStore),
        ];
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "business-evaluation", "Business evaluation", tools);
        var providerErrors = new List<string>();
        var providerRequests = new List<object>();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry(tools),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
            p => new EvidenceRecordingChatClient(CopilotMicrosoftAgentFrameworkRuntime.CreateChatClient(p),
                ex => providerErrors.Add(Redact(ex.ToString(), profile)), (messages, options) =>
                {
                    var messageList = messages.ToArray();
                    var instructions = string.Join("\n", messageList.Where(m => m.Role == ChatRole.System || m.Role == new ChatRole("developer"))
                        .Select(m => m.Text).Prepend(options?.Instructions ?? ""));
                    providerRequests.Add(new
                    {
                        Sequence = providerRequests.Count + 1,
                        HasJsonOnlyInstruction = instructions.Contains("If the user requests only JSON, return valid JSON without Markdown fences or surrounding commentary.", StringComparison.Ordinal),
                        HasCurrentPrompt = messageList.Any(m => m.Role == ChatRole.User && m.Text.Contains(scenario.Prompt, StringComparison.Ordinal)),
                        HasSteering = scenario.SteeringAfterRead is { } steering
                            && messageList.Any(m => m.Role == ChatRole.User && m.Text.Contains(steering.Message, StringComparison.Ordinal)),
                        InstructionCharacters = instructions.Length,
                        MessageCount = messageList.Length,
                    });
                }), new EmptyExternalTools(), catalog,
            new CopilotAgentSkillUsageStore(Path.Combine(caseDirectory, "skills")));
        var taskId = Guid.NewGuid().ToString("N");
        var conversationId = conversation.Id;
        var access = new CopilotAgentAccessContext();
        if (scenario.ExpectedWrites is { Count: > 0 })
            access.PrepareFullAccess(conversationId, workspace, taskId, DateTimeOffset.UtcNow.AddMinutes(10));
        var prompt = scenario.Prompt + "\n最后用一个 JSON 对象返回要求的字段。";
        var request = new CopilotAgentRequest
        {
            Profile = profile.Clone(), ConversationId = conversationId, TaskId = taskId,
            WorkspacePath = workspace, SearchRootPaths = [workspace],
            ReadableLocalFilePaths = scenario.ExposeSourceFiles ? conversation.SourceFiles.ToArray() : [],
            WritableLocalFilePaths = scenario.ExpectedWrites?.Keys.Select(name => ResolveScenarioFilePath(workspace, name)).ToArray() ?? [],
            WritableLocalRootPaths = scenario.ExpectedWrites is { Count: > 0 } && !scenario.RestrictWritesToExpectedFiles ? [workspace] : [],
            UserText = prompt, TaskIntentText = prompt, Mode = scenario.Mode,
            History = conversation.History.ToArray(), SessionCheckpoint = conversation.Checkpoint,
            AccessContext = access, HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted),
            CodexShellToolEnabled = false, CodexHooksEnabled = false, CodexPluginsEnabled = false,
            RunBudgetOverride = new() { RequestTokenBudget = tokenBudget, MaxToolCalls = 12, MaxAgentPasses = 2, TotalDuration = TimeSpan.FromSeconds(timeout) },
        };
        var answer = new StringBuilder();
        var events = new List<object>();
        var emittedResults = new List<CopilotToolResult>();
        var budget = new CopilotAgentBudgetSnapshot();
        var humanInputRequests = 0;
        var steeringTriggered = 0;
        var steeringAccepted = 0;
        CopilotSteeringAdmissionResult? steeringAdmission = null;
        var fixtureFailures = new List<string>();
        var postSteeringCallIds = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        CopilotAgentRunResult? result = null;
        string? exceptionType = null;
        string? exceptionStack = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeout + 15));
        try
        {
            result = await runtime.RunAsync(request, e =>
            {
                if (e.Type == CopilotAgentEventType.ToolStarted && e.ToolExecution != null && Volatile.Read(ref steeringAccepted) == 1)
                    postSteeringCallIds.TryAdd(e.ToolExecution.CallId, 0);
                if (e.Type == CopilotAgentEventType.AnswerReset) answer.Clear();
                if (e.Type == CopilotAgentEventType.AnswerDelta) answer.Append(e.Text);
                if (e.Budget != null) budget = e.Budget;
                if (e.Type == CopilotAgentEventType.UserQuestionRequested) humanInputRequests++;
                if (e.Type == CopilotAgentEventType.ToolResult && e.ToolResult != null) emittedResults.Add(e.ToolResult);
                if (e.Type is CopilotAgentEventType.ToolResult or CopilotAgentEventType.RuntimeDiagnostic or CopilotAgentEventType.Error)
                    events.Add(new { Type = e.Type.ToString(), Text = Redact(e.Text, profile), Tool = e.ToolResult?.ToolName, Success = e.ToolResult?.Success });
                if (scenario.SteeringAfterRead is { } steering && e.Type == CopilotAgentEventType.ToolResult && e.ToolResult?.Success == true
                    && e.ToolResult.SuccessfullyReadLocalFilePaths.Any(p => string.Equals(Path.GetFullPath(p), ResolveScenarioFilePath(workspace, steering.File), StringComparison.OrdinalIgnoreCase))
                    && Interlocked.CompareExchange(ref steeringTriggered, 1, 0) == 0)
                {
                    // This is a controlled external update, not a model write. Keep the
                    // initial hashes and separately record the new protected baseline.
                    foreach (var (name, content) in steering.UpdatedFiles)
                    {
                        var path = ResolveScenarioFilePath(workspace, name);
                        var relative = RelativeFileName(workspace, path);
                        if (!expectedHashes.TryGetValue(relative, out var expectedHash) || HashFile(path) != expectedHash)
                            fixtureFailures.Add("source_changed_before_steering:" + name);
                        File.WriteAllText(path, content, ResolveFileEncoding(scenario, name));
                        expectedHashes[relative] = HashFile(path);
                    }
                    steeringAdmission = runtime.EnqueueSteeringMessage(taskId, steering.Message);
                    Volatile.Write(ref steeringAccepted, steeringAdmission.Value.IsAccepted ? 1 : 0);
                }
            }, cancellation.Token);
            if (result.Budget.ProviderCalls >= budget.ProviderCalls) budget = result.Budget;
        }
        catch (Exception ex)
        {
            // Provider exceptions can embed request credentials. Never persist their raw message/body.
            exceptionType = ex.GetType().Name;
            exceptionStack = Redact(ex.StackTrace ?? "", profile);
        }
        stopwatch.Stop();
        var steps = result?.StepRecords ?? [];
        var failures = Grade(scenario, workspace, expectedHashes, answer.ToString(), steps);
        failures.AddRange(fixtureFailures);
        if (scenario.SteeringAfterRead is { } expectedSteering)
        {
            failures.AddRange(GradeSteering(expectedSteering, workspace, steeringAccepted == 1, postSteeringCallIds.Keys, steps));
            if (result?.SessionCheckpoint?.ConversationMemory.Any(m => m.IsSteering && m.Role == "user" && m.Content == expectedSteering.Message) != true)
                failures.Add("delivered_steering_missing_from_checkpoint");
        }
        if (exceptionType != null) failures.Add("runtime_exception:" + exceptionType);
        if (result?.StopReason != CopilotAgentStopReason.Completed) failures.Add("stop_reason:" + result?.StopReason);
        var currentRunEvents = result?.TaskEventJournal.Events.Reverse()
            .TakeWhile(e => e.Type != CopilotAgentTaskEventType.RunStarted).ToArray() ?? [];
        var sessionResumed = currentRunEvents.Any(e => e.Type == CopilotAgentTaskEventType.SessionResumed);
        var sessionReplanned = currentRunEvents.Any(e => e.Type == CopilotAgentTaskEventType.ReplanRequired);
        failures.AddRange(GradeSessionContinuation(scenario, conversation.Checkpoint != null, sessionResumed, sessionReplanned));
        var report = new CaseReport(scenario.Id, failures, stopwatch.ElapsedMilliseconds,
            result?.StopReason.ToString() ?? "Exception", budget, emittedResults.Count,
            emittedResults.Count(r => !r.Success), humanInputRequests, scenario.ContinueAfter, sessionResumed, sessionReplanned, repetition);
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "evidence.json"), JsonSerializer.Serialize(new
        {
            Scenario = scenario, Prompt = prompt, Workspace = workspace, Report = report, Answer = Redact(answer.ToString(), profile),
            SourceHashes = hashes,
            ExpectedSourceHashes = expectedHashes,
            SteeringAdmission = steeringAdmission?.Reason.ToString(),
            PostSteeringCallIds = postSteeringCallIds.Keys,
            CheckpointSteering = result?.SessionCheckpoint?.ConversationMemory.Where(m => m.IsSteering)
                .Select(m => Redact(m.Content, profile)),
            FinalHashes = CaptureFileHashes(workspace),
            Tools = steps.Select(s => new
            {
                s.ToolCall.ToolName, s.Execution.CallId, s.Round, Arguments = s.ToolCall.ToolInput, s.Observation.Success,
                s.Observation.FailureKind, Error = Redact(s.Observation.ErrorMessage, profile),
                s.Observation.SuccessfullyReadLocalFilePaths, s.Observation.LocalFileReadScopes,
                State = s.Execution.State.ToString(), s.Execution.DurationMs,
                ApprovalMode = s.Execution.ApprovalMode.ToString(),
            }),
            Events = events, ProviderRequests = providerRequests, ProviderErrors = providerErrors, ExceptionStack = exceptionStack,
        }, ReportJson));
        conversation.History.Add(new CopilotRequestMessage("user", prompt));
        conversation.History.Add(new CopilotRequestMessage("assistant", answer.ToString()));
        conversation.Checkpoint = result?.SessionCheckpoint;
        return report;
    }

    private static Encoding ResolveFileEncoding(CopilotBusinessScenario scenario, string name) => scenario.FileEncodings?.GetValueOrDefault(name) switch
    {
        null => new UTF8Encoding(false),
        "utf16-le" => Encoding.Unicode,
        "utf16-be" => Encoding.BigEndianUnicode,
        "utf32-le" => Encoding.UTF32,
        var unsupported => throw new InvalidOperationException($"Unsupported evaluation encoding: {unsupported}"),
    };

    internal static string ResolveScenarioFilePath(string workspace, string name)
    {
        var root = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, name));
        if (Path.IsPathRooted(name) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || path == root)
            throw new ArgumentException("Evaluation files must stay inside the isolated workspace.", nameof(name));
        return path;
    }

    private static string RelativeFileName(string workspace, string path) => Path.GetRelativePath(workspace, path).Replace('\\', '/');

    internal static Dictionary<string, string> CaptureFileHashes(string workspace) =>
        Directory.GetFiles(workspace, "*", SearchOption.AllDirectories)
            .ToDictionary(p => RelativeFileName(workspace, p), HashFile, StringComparer.OrdinalIgnoreCase);

    internal static List<string> GradeSteering(CopilotBusinessSteering steering, string workspace, bool accepted,
        IEnumerable<string> postSteeringCallIds, IReadOnlyList<CopilotAgentStepRecord> steps)
    {
        var failures = new List<string>();
        if (!accepted) failures.Add("steering_not_accepted");
        var calls = postSteeringCallIds.ToHashSet(StringComparer.Ordinal);
        foreach (var name in steering.UpdatedFiles.Keys)
        {
            var path = ResolveScenarioFilePath(workspace, name);
            if (!steps.Any(s => calls.Contains(s.Execution.CallId) && s.Observation.Success
                && s.Observation.SuccessfullyReadLocalFilePaths.Any(p => string.Equals(Path.GetFullPath(p), path, StringComparison.OrdinalIgnoreCase))))
                failures.Add("missing_post_steering_read:" + name);
        }
        return failures;
    }

    internal static List<string> GradeSessionContinuation(CopilotBusinessScenario scenario, bool hasPreviousCheckpoint,
        bool sessionResumed, bool sessionReplanned)
    {
        var failures = new List<string>();
        if (scenario.ContinueAfter != null && !hasPreviousCheckpoint) failures.Add("previous_checkpoint_missing");
        if (scenario.RequireSessionResume && !sessionResumed) failures.Add("session_not_resumed");
        if (scenario.RequireSessionReplan && (!sessionReplanned || sessionResumed)) failures.Add("session_not_replanned");
        return failures;
    }

    internal static List<string> Grade(CopilotBusinessScenario scenario, string workspace,
        IReadOnlyDictionary<string, string> originalHashes, string answer, IReadOnlyList<CopilotAgentStepRecord> steps)
    {
        var failures = new List<string>();
        var candidates = ParseAnswers(answer);
        if (scenario.DeferredSteeringFact is { } fact && (answer.Contains(fact, StringComparison.Ordinal)
            || candidates.Any(c => c?.ToJsonString().Contains(fact, StringComparison.Ordinal) == true)))
            failures.Add("deferred_steering_fact_leaked_to_visible_history");
        if (scenario.RequireJsonOnly)
        {
            try
            {
                if (JsonNode.Parse(answer.Trim()) is not JsonObject) failures.Add("answer_not_json_only");
            }
            catch (JsonException) { failures.Add("answer_not_json_only"); }
        }
        var expected = JsonNode.Parse(scenario.ExpectedAnswer);
        var matchingSchema = candidates.Where(c => c is JsonObject actual && expected is JsonObject required
            && required.All(p => actual.ContainsKey(p.Key))).ToArray();
        if (matchingSchema.Length == 0) failures.Add(candidates.Count == 0 ? "answer_json_missing" : "answer_schema_mismatch");
        else if (matchingSchema.Any(c => !ContainsExpected(c, expected))) failures.Add("answer_facts_mismatch");
        foreach (var name in scenario.RequiredReads ?? scenario.Files.Keys.ToArray())
        {
            var path = ResolveScenarioFilePath(workspace, name);
            if (!steps.Any(s => s.Observation.Success && s.Observation.SuccessfullyReadLocalFilePaths.Any(p => string.Equals(Path.GetFullPath(p), path, StringComparison.OrdinalIgnoreCase))))
                failures.Add("missing_read_evidence:" + name);
        }
        foreach (var toolName in scenario.RequiredTools ?? [])
            if (!steps.Any(s => s.ToolCall.ToolName == toolName && s.Observation.Success))
                failures.Add("missing_tool_evidence:" + toolName);
        var expectedWritePaths = (scenario.ExpectedWrites?.Keys.ToArray() ?? [])
            .Select(name => ResolveScenarioFilePath(workspace, name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, hash) in originalHashes)
            if (!expectedWritePaths.Contains(ResolveScenarioFilePath(workspace, name)) && HashFile(ResolveScenarioFilePath(workspace, name)) != hash)
                failures.Add("protected_source_changed:" + name);
        foreach (var (name, content) in scenario.ExpectedWrites ?? [])
        {
            var path = ResolveScenarioFilePath(workspace, name);
            if (!File.Exists(path) || !JsonEquals(File.ReadAllText(path), content)) failures.Add("artifact_mismatch:" + name);
        }
        if (scenario.ExpectedWrites is { Count: > 0 } && !steps.Any(s => s.ToolCall.ToolName == "ApplyWorkspacePatchEnvelope" && s.Observation.Success))
            failures.Add("missing_apply_evidence");
        if (scenario.RequirePostWriteRead)
        {
            var lastApply = Enumerable.Range(0, steps.Count).LastOrDefault(i =>
                steps[i].ToolCall.ToolName == "ApplyWorkspacePatchEnvelope" && steps[i].Observation.Success, -1);
            foreach (var name in scenario.ExpectedWrites?.Keys.ToArray() ?? [])
            {
                var path = ResolveScenarioFilePath(workspace, name);
                if (lastApply < 0 || !steps.Skip(lastApply + 1).Any(s => s.Observation.Success
                    && s.Observation.SuccessfullyReadLocalFilePaths.Any(p => string.Equals(Path.GetFullPath(p), path, StringComparison.OrdinalIgnoreCase))))
                    failures.Add("missing_post_write_read:" + name);
            }
        }
        foreach (var path in Directory.GetFiles(workspace, "*", SearchOption.AllDirectories))
        {
            var relative = RelativeFileName(workspace, path);
            if (!originalHashes.ContainsKey(relative) && !expectedWritePaths.Contains(Path.GetFullPath(path)))
                failures.Add("unexpected_file:" + relative);
        }
        return failures;
    }

    private static List<JsonNode?> ParseAnswers(string answer)
    {
        // Keep top-level objects so source examples cannot replace the requested
        // answer schema. Conflicting answers with that schema still fail grading.
        var objects = new List<JsonNode?>();
        for (var start = answer.IndexOf('{'); start >= 0; start = answer.IndexOf('{', start + 1))
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(answer[start..]);
                var reader = new Utf8JsonReader(bytes);
                objects.Add(JsonNode.Parse(ref reader));
                start += Encoding.UTF8.GetCharCount(bytes.AsSpan(0, checked((int)reader.BytesConsumed))) - 1;
            }
            catch (JsonException) { }
        }
        return objects;
    }

    private static bool ContainsExpected(JsonNode? actual, JsonNode? expected)
    {
        if (expected is JsonObject properties)
            return actual is JsonObject obj && properties.All(p => obj.TryGetPropertyValue(p.Key, out var value) && ContainsExpected(value, p.Value));
        return JsonNode.DeepEquals(actual, expected);
    }

    private static bool JsonEquals(string actual, string expected)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)); }
        catch (JsonException) { return false; }
    }

    private static string HashFile(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "missing";
    private static string Redact(string text, CopilotProfileConfig profile) => CopilotMcpAuditLogger.RedactText(text.Replace(profile.ApiKey, "[redacted]", StringComparison.Ordinal));
    private static string RequiredEnvironment(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"Missing {name}.");
    private static int ReadBoundedInteger(string name, int fallback, int minimum, int maximum)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (!int.TryParse(text, out var value) || value < minimum || value > maximum) throw new InvalidOperationException($"Invalid {name}.");
        return value;
    }

    private static CopilotProfileConfig LoadSelectedProfile()
    {
        var name = RequiredEnvironment("COLORVISION_COPILOT_EVAL_PROFILE");
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config", "ColorVisionConfig.json");
        var root = JObject.Parse(File.ReadAllText(path));
        var config = root["ColorVision.Copilot.CopilotConfig"] ?? root["CopilotConfig"];
        var matches = (config?["Profiles"] ?? new JArray()).Children()
            .Where(p => string.Equals((string?)p["Id"], name, StringComparison.Ordinal) || string.Equals((string?)p["Name"], name, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Select exactly one saved profile by name or ID.");
        var profile = matches[0].ToObject<CopilotProfileConfig>() ?? throw new InvalidOperationException("Profile could not be loaded.");
        if (!CopilotCredentialProtector.TryUnprotect(profile.ApiKey, out var key, out _)) throw new InvalidOperationException("Selected profile credential could not be decrypted.");
        profile.ApiKey = key;
        if (!profile.IsConfigured) throw new InvalidOperationException("Selected profile is not configured.");
        profile.MaxTokens = Math.Min(profile.MaxTokens, 4096);
        return profile;
    }

    private sealed class EmptyExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) => Task.FromResult(new CopilotExternalToolLease());
    }

    // Approval revalidation intentionally checks the live workspace owner. Supply
    // that owner in this isolated test process without opening WPF or the user's
    // most recent solution. Production approval rules remain unchanged.
    private sealed class EvaluationWorkspaceScope : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previous = Instance.GetValue(null);
        private readonly SolutionManager _manager = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public EvaluationWorkspaceScope(string workspace)
        {
            var explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(explorer, new DirectoryInfo(workspace));
            typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_manager, explorer);
            Instance.SetValue(null, _manager);
        }

        public void Dispose() => Instance.SetValue(null, _previous);
    }

    private sealed class EvidenceRecordingChatClient(IChatClient inner, Action<Exception> record,
        Action<IEnumerable<ChatMessage>, ChatOptions?> recordRequest) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var messageList = messages.ToArray();
            recordRequest(messageList, options);
            try { return await base.GetResponseAsync(messageList, options, cancellationToken); }
            catch (Exception ex) { record(ex); throw; }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messageList = messages.ToArray();
            recordRequest(messageList, options);
            await using var stream = base.GetStreamingResponseAsync(messageList, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool next;
                try { next = await stream.MoveNextAsync(); }
                catch (Exception ex) { record(ex); throw; }
                if (!next) yield break;
                yield return stream.Current;
            }
        }
    }

    private sealed class EvaluationConversation(string workspace)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Workspace { get; } = workspace;
        public HashSet<string> SourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<CopilotRequestMessage> History { get; } = [];
        public CopilotAgentSessionCheckpoint? Checkpoint { get; set; }
    }

    private sealed record CaseReport(string Id, List<string> Failures, long DurationMs, string StopReason,
        CopilotAgentBudgetSnapshot Budget, int ToolCalls, int FailedToolCalls, int HumanInputRequests,
        string? PreviousTurnId, bool SessionResumed, bool SessionReplanned, int Repetition);
}
