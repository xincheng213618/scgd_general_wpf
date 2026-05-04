using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentService
    {
        private readonly CopilotChatService _chatService;
        private readonly CopilotAgentPlanner _planner;
        private readonly CopilotToolRegistry _toolRegistry;
        private readonly CopilotAgentContextBuilder _contextBuilder;

        public CopilotAgentService(
            CopilotChatService chatService,
            CopilotToolRegistry toolRegistry,
            CopilotAgentContextBuilder contextBuilder)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _planner = new CopilotAgentPlanner(_chatService);
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        }

        public async Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);

            onEvent(CopilotAgentEvent.Status("正在分析任务..."));

            var toolResults = new List<CopilotToolResult>();
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
                        ? "工具阶段已收敛，开始生成回答。"
                        : "当前任务无需额外工具，直接生成回答。"));
                    break;
                }

                onEvent(CopilotAgentEvent.Status($"第 {round} 轮：正在规划下一步。"));
                var planResult = await _planner.PlanNextAsync(
                    roundRequest,
                    tools,
                    toolResults,
                    readableLocalFilePaths,
                    cancellationToken);
                totalUsage = totalUsage.Add(planResult.Usage);
                var plan = planResult.Plan;

                if (plan.Action == CopilotAgentPlanAction.Finish)
                {
                    onEvent(CopilotAgentEvent.Status($"第 {round} 轮：结束工具阶段。{plan.Reason}"));
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
                    onEvent(CopilotAgentEvent.Status($"第 {round} 轮：规划重复调用同一工具和参数，结束工具阶段。"));
                    break;
                }

                executedAnyTool = true;
                onEvent(CopilotAgentEvent.Status(plan.IsFallback
                    ? $"第 {round} 轮：规划器回退执行 {selectedTool.Name}。{plan.Reason}{BuildPlanDetail(plan)}"
                    : $"第 {round} 轮：计划器选择 {selectedTool.Name}。{plan.Reason}{BuildPlanDetail(plan)}"));

                CopilotToolResult result;
                try
                {
                    result = await selectedTool.ExecuteAsync(executionRequest, executionInput, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = new CopilotToolResult
                    {
                        ToolName = selectedTool.Name,
                        Success = false,
                        Summary = $"{selectedTool.Name} 执行失败。",
                        ErrorMessage = ex.Message,
                    };
                }

                toolResults.Add(result);
                onEvent(CopilotAgentEvent.FromToolResult(result));

                var discoveredNewReadableFiles = TryMergeReadableLocalFilePaths(readableLocalFilePaths, result.SuggestedReadableLocalFilePaths);
                if (discoveredNewReadableFiles)
                    onEvent(CopilotAgentEvent.Status($"第 {round} 轮：已加入新的候选文件，准备继续规划。"));
            }

                    var finalRequest = CreateRoundRequest(request, readableLocalFilePaths);
            var preparedPrompt = _contextBuilder.BuildMessages(finalRequest, toolResults);
            onEvent(CopilotAgentEvent.Status("正在生成回答..."));

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
                Usage = totalUsage,
            };
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
                SearchRootPaths = request.SearchRootPaths,
                ActiveDocumentPath = request.ActiveDocumentPath,
                ReadableLocalFilePaths = readableLocalFilePaths.ToArray(),
                ReadableLocalDirectoryPaths = request.ReadableLocalDirectoryPaths,
                PreferBatchReadLocalFiles = request.PreferBatchReadLocalFiles,
                Mode = request.Mode,
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
                    ? "当前目录下存在多个候选文件，改为本轮批量读取。"
                    : plan.Reason + " 当前目录下存在多个候选文件，改为本轮批量读取。",
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
                builder.Append(" 目标文件：").Append(Path.GetFileName(plan.LocalFilePath));

                if (plan.StartLine.HasValue)
                {
                    builder.Append(" 行号：").Append(plan.StartLine.Value);
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

                return $" 目标目录：{directoryName}";
            }

            if ((string.Equals(plan.ToolName, "SearchFiles", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.ToolName, "GrepText", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.ToolName, "GetRecentLog", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(plan.ToolQuery))
            {
                return $" 查询词：{plan.ToolQuery}";
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
                || string.Equals(toolName, "GrepText", StringComparison.OrdinalIgnoreCase))
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

            return toolName;
        }
    }
}