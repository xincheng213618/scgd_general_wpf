using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentContextBuilder
    {
        private const int MaxHistoryMessages = 8;
        private const int MaxAttachmentContentChars = 12000;
        private const int MaxPlannerObservationSteps = 6;
        private const int MaxPlannerObservationContentChars = 1200;

        public IReadOnlyList<CopilotRequestMessage> BuildPlannerMessages(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            IReadOnlyList<CopilotAgentStepRecord> stepRecords,
            IReadOnlyCollection<string> readableLocalFilePaths)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new[]
            {
                new CopilotRequestMessage(
                    "user",
                    BuildPlannerUserMessageContent(
                        request,
                        availableTools ?? Array.Empty<ICopilotTool>(),
                        stepRecords ?? Array.Empty<CopilotAgentStepRecord>(),
                        readableLocalFilePaths ?? Array.Empty<string>()))
            };
        }

        public CopilotAgentPreparedPrompt BuildAnswerMessages(CopilotAgentRequest request, IReadOnlyList<CopilotAgentStepRecord> stepRecords)
        {
            ArgumentNullException.ThrowIfNull(request);

            var preparedUserMessageContent = BuildAnswerUserMessageContent(request, stepRecords ?? Array.Empty<CopilotAgentStepRecord>());
            var messages = request.History
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(MaxHistoryMessages)
                .ToList();

            messages.Add(new CopilotRequestMessage("user", preparedUserMessageContent));
            return new CopilotAgentPreparedPrompt(messages, preparedUserMessageContent);
        }

        public CopilotAgentPreparedPrompt BuildMessages(CopilotAgentRequest request, IReadOnlyList<CopilotToolResult> toolResults)
        {
            return BuildAnswerMessages(request, ConvertToolResultsToStepRecords(toolResults));
        }

        public string BuildPreparedUserMessageContent(CopilotAgentRequest request, IReadOnlyList<CopilotToolResult> toolResults)
        {
            return BuildAnswerUserMessageContent(request, ConvertToolResultsToStepRecords(toolResults));
        }

        public string BuildObservationSummary(
            IReadOnlyList<CopilotAgentStepRecord> stepRecords,
            int maxSteps,
            int maxContentChars,
            bool includeContent)
        {
            if (stepRecords == null || stepRecords.Count == 0)
                return "- 暂无";

            var builder = new StringBuilder();
            foreach (var stepRecord in stepRecords.TakeLast(Math.Max(1, maxSteps)))
            {
                if (stepRecord == null)
                    continue;

                var toolCall = stepRecord.ToolCall ?? new CopilotToolCall();
                var observation = stepRecord.Observation ?? new CopilotToolObservation();
                var toolName = string.IsNullOrWhiteSpace(toolCall.ToolName) ? "未知工具" : toolCall.ToolName;

                builder.Append("- 第 ")
                    .Append(stepRecord.Round <= 0 ? "?" : stepRecord.Round)
                    .Append(" 轮 ")
                    .Append(toolName);

                if (toolCall.IsFallback)
                    builder.Append("（回退）");

                builder.Append(BuildToolInputDetail(toolCall))
                    .AppendLine();

                if (!string.IsNullOrWhiteSpace(toolCall.Reason))
                    builder.Append("  规划理由：").AppendLine(toolCall.Reason);

                builder.Append("  状态：")
                    .Append(observation.Success ? "成功" : "失败")
                    .Append("；摘要：")
                    .AppendLine(observation.Summary);

                if (!string.IsNullOrWhiteSpace(observation.ErrorMessage))
                    builder.Append("  错误：").AppendLine(observation.ErrorMessage);

                if (observation.SuggestedReadableLocalFilePaths.Count > 0)
                {
                    builder.Append("  候选文件：")
                        .AppendLine(string.Join("，", observation.SuggestedReadableLocalFilePaths.Take(3)));
                }

                if (includeContent && !string.IsNullOrWhiteSpace(observation.Content))
                {
                    builder.AppendLine("  内容摘录：");
                    builder.AppendLine(IndentText(TruncateContent(observation.Content.TrimEnd(), Math.Max(256, maxContentChars)), "  "));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildPlannerUserMessageContent(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools,
            IReadOnlyList<CopilotAgentStepRecord> stepRecords,
            IReadOnlyCollection<string> readableLocalFilePaths)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Choose the next Agent action. Return JSON only. Do not answer the user.");
            builder.AppendLine();
            builder.AppendLine("JSON format:");
            builder.AppendLine("{\"action\":\"tool|finish\",\"toolName\":\"tool name or empty string\",\"reason\":\"one short English reason\",\"input\":{\"query\":\"use for search or app-control tools\",\"path\":\"use for ReadLocalFile/ListDirectory\",\"startLine\":0,\"endLine\":0}}");
            builder.AppendLine();
            builder.AppendLine("Decision rules:");
            builder.AppendLine("1. If key facts are still missing and one available tool is likely to provide them, return action=tool.");
            builder.AppendLine("2. If the context is sufficient to answer, or remaining tools will not add meaningful value, return action=finish.");
            builder.AppendLine("3. toolName must be selected from the currently available tools.");
            builder.AppendLine("4. For SearchFiles, GrepText, GetRecentLog, SearchDocs, FetchUrl, SetTheme, SetLanguage, or ExecuteMenu, fill input.query when possible; use short focused search terms, direct product questions for SearchDocs, and the target theme, language, or menu for app-control tools.\n5. For FetchUrl, prefer a complete URL and avoid repeating the whole user question.\n6. For ListDirectory, fill input.path when possible; the path must come from the allowed local directory list.\n7. For ReadLocalFile, leave input.path empty when analyzing a directory or candidate set; fill input.path/startLine/endLine only for close reading of one file or line range.\n8. Keep reason to one short English sentence.");
            builder.AppendLine();
            builder.AppendLine("# User question");
            builder.AppendLine((request.UserText ?? string.Empty).Trim());

            builder.AppendLine();
            builder.AppendLine("# Available tools");
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
            builder.AppendLine("# 当前可直接列出的本地文件夹");
            if (request.ReadableLocalDirectoryPaths == null || request.ReadableLocalDirectoryPaths.Count == 0)
            {
                builder.AppendLine("- 无");
            }
            else
            {
                foreach (var path in request.ReadableLocalDirectoryPaths.Take(5))
                    builder.Append("- ").AppendLine(path);
            }

            builder.AppendLine();
            builder.AppendLine("# 已执行工具观察");
            builder.AppendLine(BuildObservationSummary(stepRecords, MaxPlannerObservationSteps, MaxPlannerObservationContentChars, includeContent: true));

            return builder.ToString().TrimEnd();
        }

        private string BuildAnswerUserMessageContent(CopilotAgentRequest request, IReadOnlyList<CopilotAgentStepRecord> stepRecords)
        {
            var observations = stepRecords ?? Array.Empty<CopilotAgentStepRecord>();
            var builder = new StringBuilder();
            builder.AppendLine("# 用户问题");
            builder.AppendLine((request.UserText ?? string.Empty).Trim());

            var applicationContext = BuildApplicationContext(request.ContextItems);
            var extraAttachmentContext = BuildAdditionalAttachmentContext(request.Attachments);
            var hasObservations = observations.Count > 0;
            if (!string.IsNullOrWhiteSpace(applicationContext) || hasObservations || !string.IsNullOrWhiteSpace(extraAttachmentContext))
            {
                builder.AppendLine();
                builder.AppendLine("# 可用上下文");

                if (!string.IsNullOrWhiteSpace(applicationContext))
                    builder.AppendLine(applicationContext.TrimEnd());

                if (!string.IsNullOrWhiteSpace(extraAttachmentContext))
                    builder.AppendLine(extraAttachmentContext.TrimEnd());

                if (hasObservations)
                {
                    builder.AppendLine("## 工具观察");
                    builder.AppendLine(BuildObservationSummary(observations, observations.Count, MaxAttachmentContentChars, includeContent: true));
                    builder.AppendLine();
                }
            }

            builder.AppendLine("# 回答要求");
            builder.AppendLine("请只基于以上上下文回答。应用可能已经抓取网页、读取文件或收集日志，但你不能声称自己直接访问了网页、本地文件、日志或设备。");
            builder.AppendLine("如果上下文不足，明确说明缺少什么；如果工具失败，只能基于失败信息说明无法分析真实内容，不能编造未提供的信息。");
            builder.AppendLine(BuildModeInstruction(request.Mode));

            return builder.ToString().TrimEnd();
        }

        private static string BuildApplicationContext(IReadOnlyList<CopilotContextItem> contextItems)
        {
            if (contextItems == null || contextItems.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var item in contextItems)
            {
                if (item == null)
                    continue;

                if (string.IsNullOrWhiteSpace(item.Title)
                    && string.IsNullOrWhiteSpace(item.Summary)
                    && string.IsNullOrWhiteSpace(item.Content))
                {
                    continue;
                }

                builder.Append("## 应用上下文");
                if (!string.IsNullOrWhiteSpace(item.Title))
                    builder.Append("：").Append(item.Title.Trim());

                builder.AppendLine();

                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("摘要：").AppendLine(item.Summary.Trim());

                if (!string.IsNullOrWhiteSpace(item.Content))
                    builder.AppendLine(TruncateContent(item.Content, MaxAttachmentContentChars));

                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildAdditionalAttachmentContext(IReadOnlyList<CopilotAttachmentItem> attachments)
        {
            if (attachments == null || attachments.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();

            foreach (var attachment in attachments.Where(item => item.Type != CopilotAttachmentType.File))
            {
                var block = BuildAttachmentBlock(attachment);
                if (string.IsNullOrWhiteSpace(block))
                    continue;

                builder.AppendLine(block.TrimEnd());
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildAttachmentBlock(CopilotAttachmentItem attachment)
        {
            return attachment.Type switch
            {
                CopilotAttachmentType.Context => string.Join(Environment.NewLine, new[]
                {
                    $"## 附加上下文：{attachment.DisplayLabel}",
                    TruncateContent(attachment.Value, MaxAttachmentContentChars),
                }),
                CopilotAttachmentType.WebPage => string.Join(Environment.NewLine, new[]
                {
                    $"## 附加网页：{attachment.DisplayLabel}",
                    $"来源：{attachment.Source}",
                    TruncateContent(attachment.Value, MaxAttachmentContentChars),
                }),
                CopilotAttachmentType.Image => string.Join(Environment.NewLine, new[]
                {
                    $"## 附加图片：{attachment.DisplayLabel}",
                    $"本地图片路径：{attachment.Value}",
                    "当前版本不会自动把图片像素上传给模型，只能引用图片附件路径和标题。",
                }),
                _ => string.Empty,
            };
        }

        private static string BuildModeInstruction(CopilotAgentMode mode)
        {
            return mode switch
            {
                CopilotAgentMode.Web => "优先围绕网页内容回答；如果网页抓取失败，明确指出无法基于真实网页内容判断。",
                CopilotAgentMode.Code => "优先结合已附加文件和工程上下文回答，必要时明确指出还需要哪些代码或文件。",
                CopilotAgentMode.Diagnose => "优先结合最近日志、失败信息和上下文分析原因，并区分已知事实与推测。",
                CopilotAgentMode.Explain => "请把结论讲清楚，并在上下文不足时说明限制。",
                _ => "优先利用应用提供的上下文完成分析，不要忽略工具结果。",
            };
        }

        private static IReadOnlyList<CopilotAgentStepRecord> ConvertToolResultsToStepRecords(IReadOnlyList<CopilotToolResult> toolResults)
        {
            if (toolResults == null || toolResults.Count == 0)
                return Array.Empty<CopilotAgentStepRecord>();

            return toolResults
                .Select((result, index) => new CopilotAgentStepRecord
                {
                    Round = index + 1,
                    ToolCall = new CopilotToolCall
                    {
                        ToolName = result?.ToolName ?? string.Empty,
                    },
                    Observation = CopilotToolObservation.FromResult(result),
                })
                .ToArray();
        }

        private static string BuildToolInputDetail(CopilotToolCall toolCall)
        {
            if (toolCall == null)
                return string.Empty;

            var toolName = toolCall.ToolName ?? string.Empty;
            var toolInput = toolCall.ToolInput ?? CopilotAgentToolInput.Empty;
            if (string.Equals(toolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Path))
            {
                var builder = new StringBuilder();
                builder.Append("（目标文件：").Append(System.IO.Path.GetFileName(toolInput.Path));
                if (toolInput.StartLine.HasValue)
                {
                    builder.Append("，行号：").Append(toolInput.StartLine.Value);
                    if (toolInput.EndLine.HasValue)
                        builder.Append('-').Append(toolInput.EndLine.Value);
                }

                builder.Append(')');
                return builder.ToString();
            }

            if (string.Equals(toolName, "ListDirectory", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Path))
            {
                var directoryName = System.IO.Path.GetFileName(toolInput.Path);
                if (string.IsNullOrWhiteSpace(directoryName))
                    directoryName = toolInput.Path;

                return $"（目标目录：{directoryName}）";
            }

            if (string.Equals(toolName, "FetchUrl", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Query))
            {
                var url = CopilotWebPageToolSupport.ExtractHttpUrls(toolInput.Query).FirstOrDefault() ?? toolInput.Query;
                return $"（目标网页：{url}）";
            }

            if (string.Equals(toolName, "SearchDocs", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Query))
            {
                return $"（文档查询：{toolInput.Query}）";
            }

            if (string.Equals(toolName, "ExecuteMenu", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Query))
            {
                return $"（目标菜单：{toolInput.Query}）";
            }

            if (!string.IsNullOrWhiteSpace(toolInput.Query))
                return $"（查询词：{toolInput.Query}）";

            return string.Empty;
        }

        private static string IndentText(string text, string prefix)
        {
            return string.Join(Environment.NewLine, (text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => prefix + line));
        }

        private static string TruncateContent(string value, int maxCharacters)
        {
            var content = value ?? string.Empty;
            if (content.Length <= maxCharacters)
                return content;

            return content[..maxCharacters] + Environment.NewLine + $"...<内容已截断，仅保留前 {maxCharacters} 字符。>";
        }
    }
}