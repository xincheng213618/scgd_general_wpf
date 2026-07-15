using ColorVision.UI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public interface ICopilotSubagentRunner
    {
        Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken);
    }

    public sealed class CopilotSubagentRunRequest
    {
        public string RunId { get; init; } = string.Empty;

        public string Task { get; init; } = string.Empty;

        public int RequestTokenBudget { get; init; }

        public long QueueDurationMs { get; init; }
    }

    public sealed class CopilotSubagentResult
    {
        public string RoleId { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;

        public int RequestTokenBudget { get; init; }

        public long QueueDurationMs { get; init; }

        public string Answer { get; init; } = string.Empty;

        public CopilotAgentStopReason StopReason { get; init; }

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;

        public CopilotAgentBudgetSnapshot Budget { get; init; } = new();

        public IReadOnlyList<string> ToolNames { get; init; } = Array.Empty<string>();

        public bool WasTruncated { get; init; }
    }

    public sealed class CopilotSubagentRunner : ICopilotSubagentRunner
    {
        internal const int MaximumTaskCharacters = 4_000;
        private const int MaximumSearchRoots = 4;
        private readonly Func<CopilotProfileConfig, IChatClient> _chatClientFactory;

        public CopilotSubagentRunner()
            : this(CopilotMicrosoftAgentFrameworkRuntime.CreateChatClient)
        {
        }

        public CopilotSubagentRunner(Func<CopilotProfileConfig, IChatClient> chatClientFactory)
        {
            _chatClientFactory = chatClientFactory ?? throw new ArgumentNullException(nameof(chatClientFactory));
        }

        public async Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            Validate(parentRequest, role, runRequest);

            var tools = role.CreateTools();
            var registry = new CopilotToolRegistry(tools);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, role.Id, "ColorVision " + role.DisplayName, tools);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                registry,
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _chatClientFactory,
                EmptyExternalToolProvider.Instance,
                catalog);
            var childRequest = CreateChildRequest(parentRequest, role, runRequest);
            var answer = new StringBuilder();
            var result = await runtime.RunAsync(
                childRequest,
                agentEvent =>
                {
                    if (agentEvent.Type == CopilotAgentEventType.AnswerReset)
                    {
                        answer.Clear();
                    }
                    else if (agentEvent.Type == CopilotAgentEventType.AnswerDelta)
                    {
                        answer.Append(agentEvent.Text);
                    }
                },
                cancellationToken);

            var finalAnswer = answer.ToString().Trim();
            var wasTruncated = finalAnswer.Length > role.MaximumAnswerCharacters;
            if (wasTruncated)
                finalAnswer = finalAnswer[..role.MaximumAnswerCharacters].TrimEnd() + $"\n...<{role.DisplayName} answer truncated>";

            return new CopilotSubagentResult
            {
                RoleId = role.Id,
                RunId = runRequest.RunId.Trim(),
                RequestTokenBudget = runRequest.RequestTokenBudget,
                QueueDurationMs = Math.Max(0, runRequest.QueueDurationMs),
                Answer = finalAnswer,
                StopReason = result.StopReason,
                Usage = result.Usage,
                Budget = result.Budget,
                ToolNames = result.StepRecords
                    .Select(step => step.ToolCall.ToolName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                WasTruncated = wasTruncated,
            };
        }

        internal static CopilotAgentRequest CreateChildRequest(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            ArgumentNullException.ThrowIfNull(role);
            ArgumentNullException.ThrowIfNull(runRequest);

            var usesWorkspaceContext = role.ContextScope == CopilotSubagentContextScope.WorkspaceReadOnly;
            var roots = usesWorkspaceContext ? SelectExploreRoots(parentRequest) : Array.Empty<string>();
            var activeDocumentPath = usesWorkspaceContext
                && CopilotWorkspaceSearchSupport.IsPathWithinRoots(parentRequest.ActiveDocumentPath, roots)
                    ? parentRequest.ActiveDocumentPath
                    : string.Empty;
            var projectInstructions = usesWorkspaceContext
                ? (parentRequest.ProjectInstructions ?? Array.Empty<CopilotProjectInstructionDocument>())
                    .Where(document => document != null && CopilotWorkspaceSearchSupport.IsPathWithinRoots(document.Path, roots))
                    .Take(CopilotAgentProjectInstructions.MaxDocuments)
                    .ToArray()
                : Array.Empty<CopilotProjectInstructionDocument>();
            var parentBudget = CopilotAgentRunBudget.Resolve(parentRequest);

            return new CopilotAgentRequest
            {
                UserText = runRequest.Task.Trim(),
                Profile = parentRequest.Profile,
                History = Array.Empty<CopilotRequestMessage>(),
                Attachments = Array.Empty<CopilotAttachmentItem>(),
                ContextItems = Array.Empty<CopilotContextItem>(),
                SearchRootPaths = roots,
                ActiveDocumentPath = activeDocumentPath,
                ProjectInstructions = projectInstructions,
                ReadableLocalFilePaths = Array.Empty<string>(),
                ReadableLocalDirectoryPaths = Array.Empty<string>(),
                WritableLocalRootPaths = Array.Empty<string>(),
                WritableLocalFilePaths = Array.Empty<string>(),
                PreferBatchReadLocalFiles = usesWorkspaceContext,
                PreferredShell = CopilotShellKind.Auto,
                Mode = role.ChildMode,
                SessionCheckpoint = null,
                Recovery = null,
                RunControl = null,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = Math.Clamp(
                        runRequest.RequestTokenBudget,
                        CopilotAgentRunBudget.MinimumRequestTokenBudget,
                        CopilotSubagentCoordinator.MaximumRunTokenBudget),
                    MaxToolCalls = Math.Min(role.MaximumToolCalls, parentBudget.MaxToolCalls),
                    MaxAgentPasses = Math.Min(role.MaximumAgentPasses, parentBudget.MaxAgentPasses),
                    TotalDuration = parentBudget.TotalDuration < role.MaximumDuration ? parentBudget.TotalDuration : role.MaximumDuration,
                },
                ExternalMcpServers = Array.Empty<CopilotMcpClientServerConfig>(),
                ForceExternalMcpToolRefresh = false,
                RuntimeRoleInstructions = role.RuntimeInstructions,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
            };
        }

        private static void Validate(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            ArgumentNullException.ThrowIfNull(role);
            ArgumentNullException.ThrowIfNull(runRequest);
            var normalizedTask = (runRequest.Task ?? string.Empty).Trim();
            if (normalizedTask.Length == 0 || normalizedTask.Length > MaximumTaskCharacters)
                throw new ArgumentException($"Subagent task must contain 1 to {MaximumTaskCharacters} characters.", nameof(runRequest));
            if (string.IsNullOrWhiteSpace(runRequest.RunId))
                throw new ArgumentException("Subagent run id is required.", nameof(runRequest));
            if (runRequest.RequestTokenBudget < CopilotAgentRunBudget.MinimumRequestTokenBudget)
                throw new ArgumentException($"Subagent token budget must be at least {CopilotAgentRunBudget.MinimumRequestTokenBudget}.", nameof(runRequest));
            if (parentRequest.Profile == null)
                throw new ArgumentException("A subagent requires an active Copilot profile.", nameof(parentRequest));
            if (!role.IsAvailable(parentRequest))
                throw new InvalidOperationException($"The {role.DisplayName} role is not available for this parent request.");
        }

        private static string[] SelectExploreRoots(CopilotAgentRequest parentRequest)
        {
            var normalizedRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(parentRequest.SearchRootPaths);
            var activeRoot = normalizedRoots.FirstOrDefault(root =>
                CopilotWorkspaceSearchSupport.IsPathWithinRoots(parentRequest.ActiveDocumentPath, [root]));
            return (string.IsNullOrWhiteSpace(activeRoot)
                    ? normalizedRoots
                    : new[] { activeRoot }.Concat(normalizedRoots.Where(root =>
                        !string.Equals(root, activeRoot, StringComparison.OrdinalIgnoreCase))))
                .Take(MaximumSearchRoots)
                .ToArray();
        }

        private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
        {
            public static EmptyExternalToolProvider Instance { get; } = new();

            public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CopilotExternalToolLease());
            }
        }
    }

    public class CopilotDelegateSubagentTool : ICopilotAgentDrivenTool, ICopilotCapabilityCatalogIdentity, ICopilotCapabilityCatalogVersionIdentity
    {
        private readonly CopilotSubagentRoleDescriptor _role;
        private readonly ICopilotSubagentRunner _runner;

        protected CopilotDelegateSubagentTool(CopilotSubagentRoleDescriptor role, ICopilotSubagentRunner runner)
        {
            _role = role ?? throw new ArgumentNullException(nameof(role));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public string Name => _role.ToolName;

        public string Description => _role.Description;

        public string CatalogCapabilityKey => _role.Id;

        public string CatalogVersionFingerprint => _role.CapabilityFingerprint;

        internal CopilotSubagentRoleDescriptor Role => _role;

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Low,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.Idempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
            ExecutionTimeout = TimeSpan.FromSeconds(100),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
            EvidenceMode = CopilotToolEvidenceMode.RedactedExcerpt,
        };

        public CopilotToolInputSchema InputSchema { get; } = CreateInputSchema();

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null && _role.IsAvailable(request);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            return $"subagent:{_role.Id}:" + (toolInput?.GetStableArgumentsJson() ?? string.Empty);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!TryReadTask(toolInput?.Arguments, out var task))
                return Failure(CopilotToolFailureKind.Validation, "Argument 'task' must be a non-empty string.");

            var coordinator = CopilotSubagentCoordination.GetCoordinator(request);
            using var lease = await coordinator.TryAcquireAsync(_role.Id, cancellationToken);
            if (lease == null)
                return Failure(CopilotToolFailureKind.Conflict, "The request-scoped subagent token budget is exhausted.");

            var childRun = new CopilotSubagentRunRequest
            {
                RunId = lease.RunId,
                Task = task,
                RequestTokenBudget = lease.RequestTokenBudget,
                QueueDurationMs = lease.QueueDurationMs,
            };
            CopilotSubagentResult result;
            try
            {
                result = await _runner.RunAsync(request, _role, childRun, cancellationToken);
                lease.Commit(Math.Max(result.Budget.ConsumedTokens, result.Usage.EffectiveTotalTokens));
            }
            catch
            {
                lease.Commit(lease.RequestTokenBudget);
                throw;
            }

            var success = !string.IsNullOrWhiteSpace(result.Answer);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = success,
                Summary = success ? SuccessSummary() : $"{_role.DisplayName} 子 Agent 没有返回可用结果。",
                Content = FormatResultContent(result, childRun),
                ErrorMessage = success ? string.Empty : $"{_role.DisplayName} stopped with {result.StopReason} and produced no displayable answer.",
                FailureKind = success ? CopilotToolFailureKind.None : CopilotToolFailureKind.Internal,
                DelegatedRunUsage = new CopilotDelegatedRunUsage
                {
                    RoleId = _role.Id,
                    RunId = childRun.RunId,
                    RequestTokenBudget = childRun.RequestTokenBudget,
                    QueueDurationMs = childRun.QueueDurationMs,
                    StopReason = result.StopReason,
                    ToolCalls = result.Budget.ToolCalls,
                    Usage = result.Usage,
                    ConsumedTokens = result.Budget.ConsumedTokens,
                    ProviderCalls = result.Budget.ProviderCalls,
                    UsedEstimatedUsage = result.Budget.UsedEstimatedUsage,
                },
            };
        }

        private static CopilotToolInputSchema CreateInputSchema()
        {
            using var document = JsonDocument.Parse("""
                {
                  "type": "object",
                  "properties": {
                    "task": {
                      "type": "string",
                      "description": "Self-contained read-only investigation for the specialized subagent, including the evidence the parent needs back.",
                      "minLength": 1,
                      "maxLength": 4000
                    }
                  },
                  "required": ["task"],
                  "additionalProperties": false
                }
                """);
            return CopilotToolInputSchema.FromJsonSchema(document.RootElement);
        }

        private string FormatResultContent(CopilotSubagentResult result, CopilotSubagentRunRequest runRequest)
        {
            var builder = new StringBuilder();
            builder.Append('[').Append(_role.DisplayName).AppendLine(" subagent result]");
            builder.Append("role: ").AppendLine(_role.Id);
            builder.Append("run_id: ").AppendLine(runRequest.RunId);
            builder.Append("stop_reason: ").AppendLine(result.StopReason.ToString());
            builder.Append("request_token_budget: ").AppendLine(runRequest.RequestTokenBudget.ToString());
            builder.Append("queue_ms: ").AppendLine(Math.Max(0, runRequest.QueueDurationMs).ToString());
            builder.Append("output_truncated: ").AppendLine(result.WasTruncated ? "true" : "false");
            builder.Append("tools_used: ").AppendLine(result.ToolNames.Count == 0 ? "none" : string.Join(", ", result.ToolNames));
            builder.AppendLine("answer:");
            builder.Append(result.Answer);
            return builder.ToString();
        }

        private CopilotToolResult Failure(CopilotToolFailureKind failureKind, string errorMessage)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"{_role.DisplayName} 子 Agent 未启动。",
                ErrorMessage = errorMessage,
                FailureKind = failureKind,
            };
        }

        private string SuccessSummary()
        {
            return _role.ContextScope == CopilotSubagentContextScope.PublicWeb
                ? $"只读 {_role.DisplayName} 子 Agent 已返回外部资料。"
                : $"只读 {_role.DisplayName} 子 Agent 已返回调查结果。";
        }

        private static bool TryReadTask(IReadOnlyDictionary<string, object?>? arguments, out string task)
        {
            task = string.Empty;
            if (arguments == null)
                return false;
            var pair = arguments.FirstOrDefault(candidate => string.Equals(candidate.Key, "task", StringComparison.OrdinalIgnoreCase));
            task = pair.Value switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element => (element.GetString() ?? string.Empty).Trim(),
                _ => string.Empty,
            };
            return task.Length is > 0 and <= CopilotSubagentRunner.MaximumTaskCharacters;
        }
    }

    public sealed class CopilotDelegateExploreTool : CopilotDelegateSubagentTool
    {
        public CopilotDelegateExploreTool()
            : this(new CopilotSubagentRunner())
        {
        }

        public CopilotDelegateExploreTool(ICopilotSubagentRunner runner)
            : base(CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId), runner)
        {
        }
    }

    public sealed class CopilotDelegateScoutTool : CopilotDelegateSubagentTool
    {
        public CopilotDelegateScoutTool()
            : this(new CopilotSubagentRunner())
        {
        }

        public CopilotDelegateScoutTool(ICopilotSubagentRunner runner)
            : base(CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ScoutRoleId), runner)
        {
        }
    }

    internal sealed class CopilotRegisteredSubagentTool : CopilotDelegateSubagentTool
    {
        public CopilotRegisteredSubagentTool(CopilotSubagentRoleDescriptor role)
            : base(role, new CopilotSubagentRunner())
        {
        }
    }
}
