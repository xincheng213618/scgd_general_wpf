using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotAgentPlanAction
    {
        Finish,
        Tool,
    }

    public sealed class CopilotAgentPlan
    {
        public CopilotAgentPlanAction Action { get; init; } = CopilotAgentPlanAction.Finish;

        public string ToolName { get; init; } = string.Empty;

        public string LocalFilePath { get; init; } = string.Empty;

        public int? StartLine { get; init; }

        public int? EndLine { get; init; }

        public string Reason { get; init; } = string.Empty;

        public bool IsFallback { get; init; }
    }

    public sealed class CopilotAgentPlanner
    {
        private const int MaxObservedTools = 6;
        private const int MaxObservationContentChars = 1200;
        private const int MaxReasonLength = 200;
        private const string PlannerSystemPrompt = "你是 ColorVision Copilot 的内部工具规划器。你的唯一任务是基于当前问题、已有工具观察和当前可用工具，决定下一步应该调用哪个工具，或者直接结束工具阶段。不要回答用户问题，不要输出 Markdown，不要解释额外内容。你必须只输出一个 JSON 对象。";

        private readonly CopilotChatService _chatService;

        public CopilotAgentPlanner(CopilotChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        public async Task<CopilotAgentPlan> PlanNextAsync(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            IReadOnlyList<CopilotToolResult> toolResults,
            IReadOnlyCollection<string> readableLocalFilePaths,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(availableTools);

            if (availableTools.Count == 0)
            {
                return new CopilotAgentPlan
                {
                    Action = CopilotAgentPlanAction.Finish,
                    Reason = "当前没有可用工具。",
                    IsFallback = true,
                };
            }

            var plannerProfile = request.Profile.Clone();
            plannerProfile.SystemPrompt = PlannerSystemPrompt;
            plannerProfile.Temperature = 0;
            plannerProfile.MaxTokens = Math.Min(256, Math.Max(128, request.Profile.MaxTokens));

            var prompt = BuildPlannerPrompt(request, availableTools, toolResults, readableLocalFilePaths);
            var response = await _chatService.CompleteReplyAsync(
                plannerProfile,
                new[] { new CopilotRequestMessage("user", prompt) },
                cancellationToken);

            var plannerText = !string.IsNullOrWhiteSpace(response.Content)
                ? response.Content
                : response.ReasoningContent;

            return ParsePlannerResponse(plannerText, availableTools);
        }

        private static string BuildPlannerPrompt(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            IReadOnlyList<CopilotToolResult> toolResults,
            IReadOnlyCollection<string> readableLocalFilePaths)
        {
            var builder = new StringBuilder();
            builder.AppendLine("你现在要为 Agent 选择下一步动作。只返回 JSON。不要回答用户问题。");
            builder.AppendLine();
            builder.AppendLine("JSON 格式：");
            builder.AppendLine("{\"action\":\"tool|finish\",\"toolName\":\"工具名或空字符串\",\"reason\":\"一句简短中文说明\",\"input\":{\"path\":\"仅在 ReadLocalFile 时填写\",\"startLine\":0,\"endLine\":0}}");
            builder.AppendLine();
            builder.AppendLine("决策规则：");
            builder.AppendLine("1. 如果当前仍缺少关键事实，并且某个可用工具最可能补足信息，就返回 action=tool。");
            builder.AppendLine("2. 如果已有上下文足够回答，或者剩余工具不会带来实质增益，就返回 action=finish。");
            builder.AppendLine("3. toolName 只能从当前可用工具中选择。");
            builder.AppendLine("4. 当 toolName=ReadLocalFile 时，尽量填写 input.path、input.startLine、input.endLine；path 必须来自可读文件列表。只有需要局部上下文时，优先小范围读取，例如 1-120 或 200-320。否则 input 置空或写 0。\n5. reason 保持一句话，20 到 60 字优先。");
            builder.AppendLine();
            builder.AppendLine("# 用户问题");
            builder.AppendLine((request.UserText ?? string.Empty).Trim());
            builder.AppendLine();
            builder.AppendLine("# 当前可用工具");
            foreach (var tool in availableTools)
            {
                builder.Append("- ")
                    .Append(tool.Name)
                    .Append(": ")
                    .AppendLine(tool.Description);
            }

            builder.AppendLine();
            builder.AppendLine("# 当前可直接读取的本地文件");
            if (readableLocalFilePaths == null || readableLocalFilePaths.Count == 0)
            {
                builder.AppendLine("- 无");
            }
            else
            {
                foreach (var path in readableLocalFilePaths.Take(5))
                    builder.Append("- ").AppendLine(path);
            }

            builder.AppendLine();
            builder.AppendLine("# 已执行工具观察");
            if (toolResults == null || toolResults.Count == 0)
            {
                builder.AppendLine("- 暂无");
            }
            else
            {
                foreach (var result in toolResults.TakeLast(MaxObservedTools))
                {
                    builder.Append("- ")
                        .Append(result.ToolName)
                        .Append(": ")
                        .Append(result.Summary);

                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                        builder.Append("；错误：").Append(result.ErrorMessage);

                    if (result.SuggestedReadableLocalFilePaths.Count > 0)
                    {
                        builder.Append("；候选文件：")
                            .Append(string.Join("，", result.SuggestedReadableLocalFilePaths.Take(3)));
                    }

                    builder.AppendLine();

                    if (!string.IsNullOrWhiteSpace(result.Content))
                    {
                        builder.AppendLine("  内容摘录：");
                        builder.AppendLine(IndentObservation(TruncateObservation(result.Content, MaxObservationContentChars)));
                    }
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static CopilotAgentPlan ParsePlannerResponse(string text, IReadOnlyList<ICopilotTool> availableTools)
        {
            var json = ExtractJsonObject(text);
            if (string.IsNullOrWhiteSpace(json))
                return BuildFallbackPlan(availableTools, "规划器没有返回可解析 JSON，回退到默认工具选择。", preferTool: true);

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var actionText = root.TryGetProperty("action", out var actionElement)
                    ? (actionElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                var toolName = root.TryGetProperty("toolName", out var toolElement)
                    ? (toolElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                var reason = root.TryGetProperty("reason", out var reasonElement)
                    ? (reasonElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                var localFilePath = TryReadString(root, "path");
                var startLine = TryReadPositiveInt(root, "startLine");
                var endLine = TryReadPositiveInt(root, "endLine");

                if (root.TryGetProperty("input", out var inputElement) && inputElement.ValueKind == JsonValueKind.Object)
                {
                    localFilePath = string.IsNullOrWhiteSpace(localFilePath) ? TryReadString(inputElement, "path") : localFilePath;
                    startLine ??= TryReadPositiveInt(inputElement, "startLine");
                    endLine ??= TryReadPositiveInt(inputElement, "endLine");
                }

                reason = NormalizeReason(reason);

                if (IsFinishAction(actionText))
                {
                    return new CopilotAgentPlan
                    {
                        Action = CopilotAgentPlanAction.Finish,
                        Reason = string.IsNullOrWhiteSpace(reason) ? "规划器判断当前上下文已足够。" : reason,
                    };
                }

                if (IsToolAction(actionText))
                {
                    var selectedTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, toolName, StringComparison.OrdinalIgnoreCase));
                    if (selectedTool != null)
                    {
                        return new CopilotAgentPlan
                        {
                            Action = CopilotAgentPlanAction.Tool,
                            ToolName = selectedTool.Name,
                            LocalFilePath = string.Equals(selectedTool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase) ? localFilePath : string.Empty,
                            StartLine = string.Equals(selectedTool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase) ? startLine : null,
                            EndLine = string.Equals(selectedTool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase) ? NormalizeEndLine(startLine, endLine) : null,
                            Reason = string.IsNullOrWhiteSpace(reason) ? $"优先执行 {selectedTool.Name} 获取缺失信息。" : reason,
                        };
                    }

                    return BuildFallbackPlan(availableTools, $"规划器选择了未知工具 {toolName}，回退到默认工具选择。", preferTool: true);
                }
            }
            catch (JsonException)
            {
            }

            return BuildFallbackPlan(availableTools, "规划器输出无法识别，回退到默认工具选择。", preferTool: true);
        }

        private static CopilotAgentPlan BuildFallbackPlan(
            IReadOnlyList<ICopilotTool> availableTools,
            string reason,
            bool preferTool)
        {
            if (preferTool && availableTools.Count > 0)
            {
                return new CopilotAgentPlan
                {
                    Action = CopilotAgentPlanAction.Tool,
                    ToolName = availableTools[0].Name,
                    Reason = NormalizeReason(reason),
                    IsFallback = true,
                };
            }

            return new CopilotAgentPlan
            {
                Action = CopilotAgentPlanAction.Finish,
                Reason = NormalizeReason(reason),
                IsFallback = true,
            };
        }

        private static string ExtractJsonObject(string text)
        {
            var value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLineBreak = value.IndexOf('\n');
                if (firstLineBreak >= 0)
                    value = value[(firstLineBreak + 1)..];

                if (value.EndsWith("```", StringComparison.Ordinal))
                    value = value[..^3];

                value = value.Trim();
            }

            var start = value.IndexOf('{');
            var end = value.LastIndexOf('}');
            if (start < 0 || end <= start)
                return string.Empty;

            return value[start..(end + 1)];
        }

        private static bool IsFinishAction(string action)
        {
            return action.Equals("finish", StringComparison.OrdinalIgnoreCase)
                || action.Equals("final", StringComparison.OrdinalIgnoreCase)
                || action.Equals("done", StringComparison.OrdinalIgnoreCase)
                || action.Contains("结束", StringComparison.OrdinalIgnoreCase)
                || action.Contains("完成", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsToolAction(string action)
        {
            return action.Equals("tool", StringComparison.OrdinalIgnoreCase)
                || action.Equals("call_tool", StringComparison.OrdinalIgnoreCase)
                || action.Contains("调用", StringComparison.OrdinalIgnoreCase)
                || action.Contains("工具", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeReason(string reason)
        {
            var value = (reason ?? string.Empty).Trim();
            if (value.Length <= MaxReasonLength)
                return value;

            return value[..MaxReasonLength];
        }

        private static string TryReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property)
                ? (property.GetString() ?? string.Empty).Trim()
                : string.Empty;
        }

        private static int? TryReadPositiveInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return null;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
                return number > 0 ? number : null;

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed))
                return parsed > 0 ? parsed : null;

            return null;
        }

        private static int? NormalizeEndLine(int? startLine, int? endLine)
        {
            if (!endLine.HasValue)
                return null;

            if (!startLine.HasValue)
                return endLine.Value;

            return Math.Max(startLine.Value, endLine.Value);
        }

        private static string TruncateObservation(string text, int maxCharacters)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length <= maxCharacters)
                return value;

            return value[..maxCharacters] + Environment.NewLine + "...<观察内容已截断>";
        }

        private static string IndentObservation(string text)
        {
            return string.Join(Environment.NewLine, (text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => "  " + line));
        }
    }
}