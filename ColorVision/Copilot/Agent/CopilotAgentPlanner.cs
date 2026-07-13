using System;
using System.Collections.Generic;
using System.Linq;
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

        public CopilotAgentToolInput ToolInput { get; init; } = CopilotAgentToolInput.Empty;

        public string ToolQuery => ToolInput?.Query ?? string.Empty;

        public string LocalFilePath => ToolInput?.Path ?? string.Empty;

        public int? StartLine => ToolInput?.StartLine;

        public int? EndLine => ToolInput?.EndLine;

        public string Reason { get; init; } = string.Empty;

        public bool IsFallback { get; init; }
    }

    public sealed class CopilotAgentPlanResult
    {
        public CopilotAgentPlan Plan { get; init; } = new();

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;
    }

    public sealed class CopilotAgentPlanner
    {
        private const int MaxReasonLength = 200;
        private const string PlannerSystemPrompt = "You are the internal tool planner for ColorVision Copilot. Your only task is to decide, from the current question, existing tool observations, and available tools, whether to call one next tool or finish the tool phase. Do not answer the user. Do not output Markdown. Do not add extra explanation. Output only one JSON object.";

        private readonly CopilotChatService _chatService;
        private readonly CopilotAgentContextBuilder _contextBuilder;

        public CopilotAgentPlanner(CopilotChatService chatService, CopilotAgentContextBuilder contextBuilder)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        }

        public async Task<CopilotAgentPlanResult> PlanNextAsync(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            IReadOnlyList<CopilotAgentStepRecord> stepRecords,
            IReadOnlyCollection<string> readableLocalFilePaths,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(availableTools);

            if (availableTools.Count == 0)
            {
                return new CopilotAgentPlanResult
                {
                    Plan = new CopilotAgentPlan
                    {
                        Action = CopilotAgentPlanAction.Finish,
                        Reason = "No tools are currently available.",
                        IsFallback = true,
                    },
                };
            }

            var plannerProfile = request.Profile.Clone();
            plannerProfile.UseSystemPromptOverride(PlannerSystemPrompt);
            plannerProfile.Temperature = 0;
            plannerProfile.MaxTokens = Math.Min(256, Math.Max(128, request.Profile.MaxTokens));

            var plannerMessages = _contextBuilder.BuildPlannerMessages(
                request,
                availableTools,
                stepRecords ?? Array.Empty<CopilotAgentStepRecord>(),
                readableLocalFilePaths);
            var response = await _chatService.CompleteReplyAsync(
                plannerProfile,
                plannerMessages,
                cancellationToken);

            var plannerText = !string.IsNullOrWhiteSpace(response.Content)
                ? response.Content
                : response.ReasoningContent;

            return new CopilotAgentPlanResult
            {
                Plan = ParsePlannerResponse(request, plannerText, availableTools),
                Usage = response.Usage,
            };
        }

        private static CopilotAgentPlan ParsePlannerResponse(CopilotAgentRequest request, string text, IReadOnlyList<ICopilotTool> availableTools)
        {
            var json = ExtractJsonObject(text);
            if (string.IsNullOrWhiteSpace(json))
                return BuildFallbackPlan(request, availableTools, "Planner returned no parseable JSON; falling back to the default tool selection.", preferTool: true);

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
                var toolQuery = TryReadString(root, "query");
                var localFilePath = TryReadString(root, "path");
                var startLine = TryReadPositiveInt(root, "startLine");
                var endLine = TryReadPositiveInt(root, "endLine");

                if (root.TryGetProperty("input", out var inputElement) && inputElement.ValueKind == JsonValueKind.Object)
                {
                    toolQuery = string.IsNullOrWhiteSpace(toolQuery) ? TryReadString(inputElement, "query") : toolQuery;
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
                        Reason = string.IsNullOrWhiteSpace(reason) ? "The planner judged the current context sufficient." : reason,
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
                            ToolInput = BuildToolInput(selectedTool.Name, toolQuery, localFilePath, startLine, endLine),
                            Reason = string.IsNullOrWhiteSpace(reason) ? $"Run {selectedTool.Name} first to collect missing information." : reason,
                        };
                    }

                    return BuildFallbackPlan(request, availableTools, $"Planner selected unknown tool {toolName}; falling back to the default tool selection.", preferTool: true);
                }
            }
            catch (JsonException)
            {
            }

            return BuildFallbackPlan(request, availableTools, "Planner output was not recognized; falling back to the default tool selection.", preferTool: true);
        }

        private static CopilotAgentPlan BuildFallbackPlan(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            string reason,
            bool preferTool)
        {
            var normalizedReason = NormalizeReason(reason);
            if (preferTool && availableTools.Count > 0)
            {
                var selectedTool = SelectFallbackTool(request, availableTools);
                if (selectedTool != null)
                {
                    return new CopilotAgentPlan
                    {
                        Action = CopilotAgentPlanAction.Tool,
                        ToolName = selectedTool.Name,
                        ToolInput = BuildFallbackToolInput(request, selectedTool.Name),
                        Reason = normalizedReason,
                        IsFallback = true,
                    };
                }
            }

            return new CopilotAgentPlan
            {
                Action = CopilotAgentPlanAction.Finish,
                Reason = normalizedReason,
                IsFallback = true,
            };
        }

        private static ICopilotTool? SelectFallbackTool(CopilotAgentRequest request, IReadOnlyList<ICopilotTool> availableTools)
        {
            if (availableTools.Count == 0)
                return null;

            if (CopilotFlowCreationSupport.HasCreateIntent(request.UserText))
            {
                var createFlowTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "CreateFlow", StringComparison.OrdinalIgnoreCase));
                if (createFlowTool != null)
                    return createFlowTool;
            }

            if (CopilotApplicationCapability.HasMenuIntent(request.UserText))
            {
                var menuTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "ExecuteMenu", StringComparison.OrdinalIgnoreCase));
                if (menuTool != null)
                    return menuTool;
            }

            if (CopilotApplicationCapability.HasThemeIntent(request.UserText))
            {
                var themeTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "SetTheme", StringComparison.OrdinalIgnoreCase));
                if (themeTool != null)
                    return themeTool;
            }

            if (CopilotApplicationCapability.HasLanguageIntent(request.UserText))
            {
                var languageTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "SetLanguage", StringComparison.OrdinalIgnoreCase));
                if (languageTool != null)
                    return languageTool;
            }

            if (CopilotDocsCapability.HasDocumentationIntent(request.UserText))
            {
                var docsTool = availableTools.FirstOrDefault(tool => string.Equals(tool.Name, "SearchDocs", StringComparison.OrdinalIgnoreCase));
                if (docsTool != null)
                    return docsTool;
            }

            return null;
        }

        private static CopilotAgentToolInput BuildFallbackToolInput(CopilotAgentRequest request, string toolName)
        {
            if (string.Equals(toolName, "CreateFlow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "ExecuteMenu", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "SetTheme", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "SetLanguage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "TemplatePatch", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentToolInput
                {
                    Query = request.UserText ?? string.Empty,
                };
            }

            if (string.Equals(toolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentToolInput
                {
                    Path = request.ReadableLocalFilePaths.Count == 1
                        ? request.ReadableLocalFilePaths[0]
                        : string.Empty,
                };
            }

            if (string.Equals(toolName, "ListDirectory", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentToolInput
                {
                    Path = request.ReadableLocalDirectoryPaths.Count == 1
                        ? request.ReadableLocalDirectoryPaths[0]
                        : string.Empty,
                };
            }

            if (string.Equals(toolName, "FetchUrl", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentToolInput
                {
                    Query = CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).FirstOrDefault() ?? string.Empty,
                };
            }

            if (string.Equals(toolName, "SearchDocs", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentToolInput
                {
                    Query = request.UserText ?? string.Empty,
                };
            }

            if (string.Equals(toolName, "WebSearch", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentToolInput
                {
                    Query = request.UserText ?? string.Empty,
                };
            }

            return CopilotAgentToolInput.Empty;
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
                || action.Contains("\u7ed3\u675f", StringComparison.OrdinalIgnoreCase)
                || action.Contains("\u5b8c\u6210", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsToolAction(string action)
        {
            return action.Equals("tool", StringComparison.OrdinalIgnoreCase)
                || action.Equals("call_tool", StringComparison.OrdinalIgnoreCase)
                || action.Contains("\u8c03\u7528", StringComparison.OrdinalIgnoreCase)
                || action.Contains("\u5de5\u5177", StringComparison.OrdinalIgnoreCase);
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

        private static CopilotAgentToolInput BuildToolInput(string toolName, string toolQuery, string localFilePath, int? startLine, int? endLine)
        {
            return new CopilotAgentToolInput
            {
                Query = RequiresQuery(toolName) ? toolQuery : string.Empty,
                Path = RequiresPath(toolName) ? localFilePath : string.Empty,
                StartLine = string.Equals(toolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase) ? startLine : null,
                EndLine = string.Equals(toolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase) ? NormalizeEndLine(startLine, endLine) : null,
            };
        }

        private static bool RequiresPath(string toolName)
        {
            return string.Equals(toolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "ListDirectory", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresQuery(string toolName)
        {
            return string.Equals(toolName, "SearchFiles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "GrepText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "GetRecentLog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "SearchDocs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "WebSearch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "FetchUrl", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "ExecuteMenu", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "SetTheme", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "SetLanguage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "TemplatePatch", StringComparison.OrdinalIgnoreCase);
        }

    }
}
