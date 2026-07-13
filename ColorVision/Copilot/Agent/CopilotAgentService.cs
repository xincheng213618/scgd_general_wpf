#pragma warning disable CA1826,CA1859
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentService : ICopilotAgentRuntime
    {
        private readonly CopilotChatService _chatService;
        private readonly CopilotAgentPlanner _planner;
        private readonly CopilotToolRegistry _toolRegistry;
        private readonly CopilotAgentContextBuilder _contextBuilder;
        private readonly CopilotToolExecutor _toolExecutor;

        public CopilotAgentService(
            CopilotChatService chatService,
            CopilotToolRegistry toolRegistry,
            CopilotAgentContextBuilder contextBuilder)
            : this(chatService, toolRegistry, contextBuilder, new CopilotToolExecutor())
        {
        }

        public CopilotAgentService(
            CopilotChatService chatService,
            CopilotToolRegistry toolRegistry,
            CopilotAgentContextBuilder contextBuilder,
            CopilotToolExecutor toolExecutor)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _planner = new CopilotAgentPlanner(_chatService, _contextBuilder);
        }

        public async Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);

            onEvent(CopilotAgentEvent.Status("Analyzing task..."));

            var toolResults = new List<CopilotToolResult>();
            var stepRecords = new List<CopilotAgentStepRecord>();
            var readableLocalFilePaths = new HashSet<string>(
                (request.ReadableLocalFilePaths ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path)),
                StringComparer.OrdinalIgnoreCase);
            var maxToolRounds = request.Profile?.MaxToolRounds > 0
                ? request.Profile.MaxToolRounds
                : CopilotProfileConfig.DefaultMaxToolRounds;
            var executedStepSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var executedAnyTool = false;
            var totalUsage = CopilotTokenUsage.Empty;

            for (var round = 1; round <= maxToolRounds; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var roundRequest = CreateRoundRequest(request, readableLocalFilePaths);
                var tools = _toolRegistry.FindTools(roundRequest)
                    .ToArray();

                if (tools.Length == 0)
                {
                    onEvent(CopilotAgentEvent.Status(executedAnyTool
                        ? "Tool phase converged; generating answer."
                        : "No extra tools are needed for this task; generating answer."));
                    break;
                }

                CopilotAgentPlan plan;
                if (TryCreateRequiredWebPlan(roundRequest, tools, stepRecords, out var requiredWebPlan))
                {
                    plan = requiredWebPlan;
                    onEvent(CopilotAgentEvent.Status($"Round {round}: applying required web evidence policy."));
                }
                else
                {
                    onEvent(CopilotAgentEvent.Status($"Round {round}: planning next step."));
                    var planResult = await _planner.PlanNextAsync(
                        roundRequest,
                        tools,
                        stepRecords,
                        readableLocalFilePaths,
                        cancellationToken);
                    totalUsage = totalUsage.Add(planResult.Usage);
                    plan = planResult.Plan;
                }

                if (plan.Action == CopilotAgentPlanAction.Finish)
                {
                    onEvent(CopilotAgentEvent.Status($"Round {round}: finishing tool phase. {plan.Reason}"));
                    break;
                }

                plan = NormalizePlanForExecution(request, readableLocalFilePaths, toolResults, plan);

                var selectedTool = tools.FirstOrDefault(tool => string.Equals(tool.Name, plan.ToolName, StringComparison.OrdinalIgnoreCase))
                    ?? tools[0];
                var executionRequest = CreateRoundRequest(request, readableLocalFilePaths);
                var executionInput = plan.ToolInput ?? CopilotAgentToolInput.Empty;
                var stepSignature = BuildToolExecutionSignature(selectedTool.Name, executionRequest, executionInput);
                if (!executedStepSignatures.Add(stepSignature))
                {
                    onEvent(CopilotAgentEvent.Status($"Round {round}: repeated the same tool call and arguments; finishing tool phase."));
                    break;
                }

                executedAnyTool = true;
                onEvent(CopilotAgentEvent.Status(plan.IsFallback
                    ? $"Round {round}: planner fallback selected {selectedTool.Name}. {plan.Reason}{BuildPlanDetail(plan)}"
                    : $"Round {round}: planner selected {selectedTool.Name}. {plan.Reason}{BuildPlanDetail(plan)}"));

                var outcome = await _toolExecutor.ExecuteAsync(new CopilotToolInvocation
                {
                    Round = round,
                    RuntimeName = "built-in",
                    Tool = selectedTool,
                    AgentRequest = executionRequest,
                    ToolInput = executionInput,
                    ToolCall = CopilotToolCall.FromPlan(plan, selectedTool.Name),
                }, onEvent, cancellationToken);
                var result = outcome.Result;
                toolResults.Add(result);
                stepRecords.Add(outcome.StepRecord);

                var discoveredNewReadableFiles = TryMergeReadableLocalFilePaths(readableLocalFilePaths, result.SuggestedReadableLocalFilePaths);
                if (discoveredNewReadableFiles)
                    onEvent(CopilotAgentEvent.Status($"Round {round}: added newly discovered candidate files; continuing planning."));
            }

            var finalRequest = CreateRoundRequest(request, readableLocalFilePaths);
            var preparedPrompt = _contextBuilder.BuildAnswerMessages(finalRequest, stepRecords);
            onEvent(CopilotAgentEvent.Status("Generating answer..."));

            var finalUsage = await _chatService.StreamReplyAsync(
                request.Profile,
                preparedPrompt.Messages,
                delta =>
                {
                    if (delta.HasReasoning)
                        onEvent(CopilotAgentEvent.ReasoningDelta(delta.ReasoningContent));

                    if (delta.HasContent)
                        onEvent(CopilotAgentEvent.AnswerDelta(delta.Content));
                },
                cancellationToken);
            totalUsage = totalUsage.Add(finalUsage);

            onEvent(CopilotAgentEvent.Completed());
            return new CopilotAgentRunResult
            {
                PreparedUserMessageContent = preparedPrompt.PreparedUserMessageContent,
                StepRecords = stepRecords.ToArray(),
                Usage = totalUsage,
            };
        }

        private static bool TryCreateRequiredWebPlan(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            IReadOnlyList<CopilotAgentStepRecord> stepRecords,
            out CopilotAgentPlan plan)
        {
            plan = new CopilotAgentPlan();
            var urls = CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText);
            if (urls.Count == 0 || HasWebAccessOptOut(request.UserText))
                return false;

            var fetchStep = stepRecords.FirstOrDefault(step => string.Equals(step.ToolCall.ToolName, "FetchUrl", StringComparison.OrdinalIgnoreCase));
            if (fetchStep == null)
            {
                var fetchTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "FetchUrl", StringComparison.OrdinalIgnoreCase));
                if (fetchTool != null)
                {
                    plan = new CopilotAgentPlan
                    {
                        Action = CopilotAgentPlanAction.Tool,
                        ToolName = fetchTool.Name,
                        ToolInput = new CopilotAgentToolInput { Query = string.Join(" ", urls.Take(3)) },
                        Reason = "The user provided a web page URL, so direct page evidence is required before answering.",
                        IsFallback = true,
                    };
                    return true;
                }
            }

            var fetchFailed = fetchStep != null && !fetchStep.Observation.Success;
            if (fetchFailed && !stepRecords.Any(step => string.Equals(step.ToolCall.ToolName, "WebSearch", StringComparison.OrdinalIgnoreCase)))
            {
                var webSearchTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "WebSearch", StringComparison.OrdinalIgnoreCase));
                if (webSearchTool != null)
                {
                    plan = new CopilotAgentPlan
                    {
                        Action = CopilotAgentPlanAction.Tool,
                        ToolName = webSearchTool.Name,
                        ToolInput = new CopilotAgentToolInput { Query = request.UserText },
                        Reason = "Direct page retrieval failed, so public web search is the next read-only fallback.",
                        IsFallback = true,
                    };
                    return true;
                }
            }

            return false;
        }

        private static bool HasWebAccessOptOut(string text)
        {
            var value = text ?? string.Empty;
            string[] markers =
            {
                "不要访问", "别访问", "不要打开", "无需访问", "不要联网",
                "do not access", "don't access", "do not open", "don't open", "without opening",
            };
            return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static CopilotAgentRequest CreateRoundRequest(
            CopilotAgentRequest request,
            HashSet<string> readableLocalFilePaths)
        {
            return new CopilotAgentRequest
            {
                UserText = request.UserText,
                Profile = request.Profile,
                History = request.History,
                Attachments = request.Attachments,
                ContextItems = request.ContextItems,
                SearchRootPaths = request.SearchRootPaths,
                ActiveDocumentPath = request.ActiveDocumentPath,
                ReadableLocalFilePaths = readableLocalFilePaths.ToArray(),
                ReadableLocalDirectoryPaths = request.ReadableLocalDirectoryPaths,
                PreferBatchReadLocalFiles = request.PreferBatchReadLocalFiles,
                Mode = request.Mode,
                SessionCheckpoint = request.SessionCheckpoint,
                Recovery = request.Recovery,
                RunControl = request.RunControl,
                ExternalMcpServers = request.ExternalMcpServers,
            };
        }

        private static CopilotAgentPlan NormalizePlanForExecution(
            CopilotAgentRequest request,
            IReadOnlyCollection<string> readableLocalFilePaths,
            IReadOnlyList<CopilotToolResult> toolResults,
            CopilotAgentPlan plan)
        {
            if (!request.PreferBatchReadLocalFiles
                || !string.Equals(plan.ToolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)
                || readableLocalFilePaths.Count <= 1
                || toolResults.Any(result => string.Equals(result.ToolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase) && result.Success)
                || string.IsNullOrWhiteSpace(plan.LocalFilePath))
            {
                return plan;
            }

            return new CopilotAgentPlan
            {
                Action = plan.Action,
                ToolName = plan.ToolName,
                ToolInput = CopilotAgentToolInput.Empty,
                Reason = string.IsNullOrWhiteSpace(plan.Reason)
                    ? "Multiple candidate files are available; switching this round to batch read."
                    : plan.Reason + " Multiple candidate files are available; switching this round to batch read.",
                IsFallback = plan.IsFallback,
            };
        }

        private static bool TryMergeReadableLocalFilePaths(
            HashSet<string> readableLocalFilePaths,
            IReadOnlyList<string> suggestedReadableLocalFilePaths)
        {
            if (suggestedReadableLocalFilePaths == null || suggestedReadableLocalFilePaths.Count == 0)
                return false;

            var added = false;
            foreach (var path in suggestedReadableLocalFilePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!File.Exists(path))
                    continue;

                added |= readableLocalFilePaths.Add(path);
            }

            return added;
        }

        private static string BuildPlanDetail(CopilotAgentPlan plan)
        {
            if (string.Equals(plan.ToolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(plan.LocalFilePath))
            {
                var builder = new System.Text.StringBuilder();
                builder.Append(" target file: ").Append(Path.GetFileName(plan.LocalFilePath));

                if (plan.StartLine.HasValue)
                {
                    builder.Append(" lines: ").Append(plan.StartLine.Value);
                    if (plan.EndLine.HasValue)
                        builder.Append('-').Append(plan.EndLine.Value);
                }

                return builder.ToString();
            }

            if (string.Equals(plan.ToolName, "ListDirectory", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(plan.LocalFilePath))
            {
                var directoryName = Path.GetFileName(plan.LocalFilePath);
                if (string.IsNullOrWhiteSpace(directoryName))
                    directoryName = plan.LocalFilePath;

                return $" target directory: {directoryName}";
            }

            if ((string.Equals(plan.ToolName, "SearchFiles", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.ToolName, "GrepText", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.ToolName, "WebSearch", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.ToolName, "GetRecentLog", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.ToolName, "FetchUrl", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(plan.ToolQuery))
            {
                if (string.Equals(plan.ToolName, "FetchUrl", StringComparison.OrdinalIgnoreCase))
                {
                    var url = CopilotWebPageToolSupport.ExtractHttpUrls(plan.ToolQuery).FirstOrDefault() ?? plan.ToolQuery;
                    return $" target page: {url}";
                }

                return $" query: {plan.ToolQuery}";
            }

            return string.Empty;
        }

        private static string BuildToolExecutionSignature(string toolName, CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            if (string.Equals(toolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("|", new[]
                {
                    toolName,
                    toolInput?.Path ?? string.Empty,
                    toolInput?.StartLine?.ToString() ?? string.Empty,
                    toolInput?.EndLine?.ToString() ?? string.Empty,
                });
            }

            if (string.Equals(toolName, "SearchFiles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "GrepText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "WebSearch", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("|", new[]
                {
                    toolName,
                    toolInput?.Query ?? string.Empty,
                });
            }

            if (string.Equals(toolName, "ListDirectory", StringComparison.OrdinalIgnoreCase))
            {
                var directoryPath = toolInput?.Path;
                if (string.IsNullOrWhiteSpace(directoryPath))
                    directoryPath = request.ReadableLocalDirectoryPaths.FirstOrDefault() ?? string.Empty;

                return string.Join("|", new[]
                {
                    toolName,
                    directoryPath,
                });
            }

            if (string.Equals(toolName, "GetRecentLog", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("|", new[]
                {
                    toolName,
                    toolInput?.Query ?? string.Empty,
                });
            }

            if (string.Equals(toolName, "FetchUrl", StringComparison.OrdinalIgnoreCase))
            {
                var query = toolInput?.Query ?? string.Empty;
                if (string.IsNullOrWhiteSpace(query))
                    query = string.Join(";", CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Take(3));

                return string.Join("|", new[]
                {
                    toolName,
                    query,
                });
            }

            return toolName;
        }
    }
}
