#pragma warning disable CA1826,CA1859
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            var runBudget = CopilotAgentRunBudget.Resolve(request);
            var stopwatch = Stopwatch.StartNew();
            var progress = new BuiltInRunProgress(request.ReadableLocalFilePaths);
            using var timeBudgetCancellation = new CancellationTokenSource(runBudget.TotalDuration);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeBudgetCancellation.Token);
            try
            {
                return await RunCoreAsync(request, onEvent, runBudget, progress, stopwatch, linkedCancellation.Token);
            }
            catch (OperationCanceledException) when (request.RunControl?.Intent is CopilotAgentControlIntent.Pause or CopilotAgentControlIntent.Cancel)
            {
                var controlIntent = request.RunControl.Intent;
                var stopReason = controlIntent == CopilotAgentControlIntent.Pause
                    ? CopilotAgentStopReason.Paused
                    : CopilotAgentStopReason.Cancelled;
                onEvent(CopilotAgentEvent.RuntimeDiagnostic(controlIntent == CopilotAgentControlIntent.Pause
                    ? "Built-in Agent pause requested; returning the partial compatibility-run state."
                    : "Built-in Agent cancellation requested; returning the partial compatibility-run state."));
                var result = CreateInterruptedResult(request, runBudget, progress, stopwatch.Elapsed, stopReason, timeBudgetExhausted: false);
                onEvent(CopilotAgentEvent.Completed());
                return result;
            }
            catch (OperationCanceledException) when (timeBudgetCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                onEvent(CopilotAgentEvent.RuntimeDiagnostic("Built-in Agent total-time budget exhausted; the compatibility run was stopped."));
                var result = CreateInterruptedResult(
                    request,
                    runBudget,
                    progress,
                    stopwatch.Elapsed,
                    CopilotAgentStopReason.BudgetExhausted,
                    timeBudgetExhausted: true);
                onEvent(CopilotAgentEvent.Completed());
                return result;
            }
        }

        private async Task<CopilotAgentRunResult> RunCoreAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CopilotAgentRunBudget runBudget,
            BuiltInRunProgress progress,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            onEvent(CopilotAgentEvent.Status("Analyzing task..."));

            var toolResults = new List<CopilotToolResult>();
            var executedStepSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var executedAnyTool = false;
            var toolPhaseTerminated = false;
            var stopReason = CopilotAgentStopReason.Completed;

            for (var round = 1; round <= runBudget.MaxToolCalls; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (progress.Usage.EffectiveTotalTokens >= runBudget.RequestTokenBudget)
                {
                    onEvent(CopilotAgentEvent.RuntimeDiagnostic("Built-in Agent request-token budget reached; finishing with the collected observations."));
                    stopReason = CopilotAgentStopReason.BudgetExhausted;
                    toolPhaseTerminated = true;
                    break;
                }

                var roundRequest = CreateRoundRequest(request, progress.ReadableLocalFilePaths);
                var tools = _toolRegistry.FindTools(roundRequest)
                    .ToArray();

                if (tools.Length == 0)
                {
                    onEvent(CopilotAgentEvent.Status(executedAnyTool
                        ? "Tool phase converged; generating answer."
                        : "No extra tools are needed for this task; generating answer."));
                    toolPhaseTerminated = true;
                    break;
                }

                CopilotAgentPlan plan;
                if (TryCreateRequiredWebPlan(roundRequest, tools, progress.StepRecords, out var requiredWebPlan))
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
                        progress.StepRecords,
                        progress.ReadableLocalFilePaths,
                        cancellationToken);
                    progress.ProviderCalls++;
                    progress.Usage = progress.Usage.Add(planResult.Usage);
                    plan = planResult.Plan;
                    if (progress.Usage.EffectiveTotalTokens >= runBudget.RequestTokenBudget)
                    {
                        onEvent(CopilotAgentEvent.RuntimeDiagnostic("Built-in Agent request-token budget was exhausted while planning; no additional tool or answer call will be started."));
                        stopReason = CopilotAgentStopReason.BudgetExhausted;
                        toolPhaseTerminated = true;
                        break;
                    }
                }

                if (plan.Action == CopilotAgentPlanAction.Finish)
                {
                    onEvent(CopilotAgentEvent.Status($"Round {round}: finishing tool phase. {plan.Reason}"));
                    toolPhaseTerminated = true;
                    break;
                }

                plan = NormalizePlanForExecution(request, progress.ReadableLocalFilePaths, toolResults, plan);

                var selectedTool = tools.FirstOrDefault(tool => string.Equals(tool.Name, plan.ToolName, StringComparison.OrdinalIgnoreCase))
                    ?? tools[0];
                var executionRequest = CreateRoundRequest(request, progress.ReadableLocalFilePaths);
                var executionInput = plan.ToolInput ?? CopilotAgentToolInput.Empty;
                var stepSignature = BuildToolExecutionSignature(selectedTool.Name, executionRequest, executionInput);
                if (!executedStepSignatures.Add(stepSignature))
                {
                    onEvent(CopilotAgentEvent.RuntimeDiagnostic($"Round {round}: repeated the same tool call and arguments; stopping the compatibility loop as incomplete."));
                    stopReason = CopilotAgentStopReason.TaskPassLimit;
                    toolPhaseTerminated = true;
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
                progress.StepRecords.Add(outcome.StepRecord);

                var discoveredNewReadableFiles = TryMergeReadableLocalFilePaths(progress.ReadableLocalFilePaths, result.SuggestedReadableLocalFilePaths);
                if (discoveredNewReadableFiles)
                    onEvent(CopilotAgentEvent.Status($"Round {round}: added newly discovered candidate files; continuing planning."));
            }

            if (!toolPhaseTerminated)
            {
                stopReason = CopilotAgentStopReason.TaskPassLimit;
                onEvent(CopilotAgentEvent.RuntimeDiagnostic($"Built-in Agent reached the {runBudget.MaxToolCalls}-tool-call compatibility limit; generating a partial answer from collected observations."));
            }

            var finalRequest = CreateRoundRequest(request, progress.ReadableLocalFilePaths);
            var preparedPrompt = _contextBuilder.BuildAnswerMessages(finalRequest, progress.StepRecords);
            if (stopReason == CopilotAgentStopReason.BudgetExhausted)
            {
                var exhaustedResult = CreateRunResult(
                    preparedPrompt.PreparedUserMessageContent,
                    runBudget,
                    progress,
                    stopwatch.Elapsed,
                    stopReason,
                    timeBudgetExhausted: false);
                onEvent(CopilotAgentEvent.Completed());
                return exhaustedResult;
            }

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
            progress.ProviderCalls++;
            progress.Usage = progress.Usage.Add(finalUsage);

            var consumedTokens = progress.Usage.EffectiveTotalTokens;
            var tokenSnapshot = new CopilotAgentBudgetSnapshot
            {
                RequestTokenBudget = runBudget.RequestTokenBudget,
                ConsumedTokens = consumedTokens,
                ProviderCalls = progress.ProviderCalls,
                BudgetExhausted = consumedTokens >= runBudget.RequestTokenBudget,
            };
            var budgetSnapshot = runBudget.CreateSnapshot(tokenSnapshot, stopwatch.Elapsed, progress.StepRecords.Count, timeBudgetExhausted: false);
            if (budgetSnapshot.BudgetExhausted)
                stopReason = CopilotAgentStopReason.BudgetExhausted;
            onEvent(CopilotAgentEvent.Completed());
            return new CopilotAgentRunResult
            {
                PreparedUserMessageContent = preparedPrompt.PreparedUserMessageContent,
                StepRecords = progress.StepRecords.ToArray(),
                Usage = progress.Usage,
                Budget = budgetSnapshot,
                StopReason = stopReason,
            };
        }

        private CopilotAgentRunResult CreateInterruptedResult(
            CopilotAgentRequest request,
            CopilotAgentRunBudget runBudget,
            BuiltInRunProgress progress,
            TimeSpan elapsed,
            CopilotAgentStopReason stopReason,
            bool timeBudgetExhausted)
        {
            var finalRequest = CreateRoundRequest(request, progress.ReadableLocalFilePaths);
            var preparedPrompt = _contextBuilder.BuildAnswerMessages(finalRequest, progress.StepRecords);
            return CreateRunResult(
                preparedPrompt.PreparedUserMessageContent,
                runBudget,
                progress,
                elapsed,
                stopReason,
                timeBudgetExhausted);
        }

        private static CopilotAgentRunResult CreateRunResult(
            string preparedUserMessageContent,
            CopilotAgentRunBudget runBudget,
            BuiltInRunProgress progress,
            TimeSpan elapsed,
            CopilotAgentStopReason stopReason,
            bool timeBudgetExhausted)
        {
            var consumedTokens = progress.Usage.EffectiveTotalTokens;
            var tokenSnapshot = new CopilotAgentBudgetSnapshot
            {
                RequestTokenBudget = runBudget.RequestTokenBudget,
                ConsumedTokens = consumedTokens,
                ProviderCalls = progress.ProviderCalls,
                BudgetExhausted = consumedTokens >= runBudget.RequestTokenBudget,
            };
            return new CopilotAgentRunResult
            {
                PreparedUserMessageContent = preparedUserMessageContent ?? string.Empty,
                StepRecords = progress.StepRecords.ToArray(),
                Usage = progress.Usage,
                Budget = runBudget.CreateSnapshot(tokenSnapshot, elapsed, progress.StepRecords.Count, timeBudgetExhausted),
                StopReason = stopReason,
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
                RunBudgetOverride = request.RunBudgetOverride,
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

        private sealed class BuiltInRunProgress
        {
            public BuiltInRunProgress(IReadOnlyList<string> readableLocalFilePaths)
            {
                ReadableLocalFilePaths = new HashSet<string>(
                    (readableLocalFilePaths ?? Array.Empty<string>())
                        .Where(path => !string.IsNullOrWhiteSpace(path)),
                    StringComparer.OrdinalIgnoreCase);
            }

            public List<CopilotAgentStepRecord> StepRecords { get; } = new();

            public HashSet<string> ReadableLocalFilePaths { get; }

            public CopilotTokenUsage Usage { get; set; } = CopilotTokenUsage.Empty;

            public int ProviderCalls { get; set; }
        }
    }
}
