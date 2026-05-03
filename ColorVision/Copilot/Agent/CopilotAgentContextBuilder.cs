using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentContextBuilder
    {
        private const int MaxHistoryMessages = 8;
        private const int MaxAttachmentContentChars = 12000;

        public CopilotAgentPreparedPrompt BuildMessages(CopilotAgentRequest request, IReadOnlyList<CopilotToolResult> toolResults)
        {
            ArgumentNullException.ThrowIfNull(request);

            var preparedUserMessageContent = BuildPreparedUserMessageContent(request, toolResults ?? Array.Empty<CopilotToolResult>());
            var messages = request.History
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(MaxHistoryMessages)
                .ToList();

            messages.Add(new CopilotRequestMessage("user", preparedUserMessageContent));
            return new CopilotAgentPreparedPrompt(messages, preparedUserMessageContent);
        }

        public string BuildPreparedUserMessageContent(CopilotAgentRequest request, IReadOnlyList<CopilotToolResult> toolResults)
        {
            ArgumentNullException.ThrowIfNull(request);

            var builder = new StringBuilder();
            builder.AppendLine("# 用户问题");
            builder.AppendLine((request.UserText ?? string.Empty).Trim());

            var extraAttachmentContext = BuildAdditionalAttachmentContext(request.Attachments);
            var hasTools = toolResults.Count > 0;
            if (hasTools || !string.IsNullOrWhiteSpace(extraAttachmentContext))
            {
                builder.AppendLine();
                builder.AppendLine("# 可用上下文");

                if (!string.IsNullOrWhiteSpace(extraAttachmentContext))
                    builder.AppendLine(extraAttachmentContext.TrimEnd());

                if (hasTools)
                {
                    foreach (var result in toolResults)
                    {
                        if (result == null)
                            continue;

                        builder.AppendLine($"## 工具：{result.ToolName}");
                        builder.AppendLine($"状态：{(result.Success ? "成功" : "失败")}");
                        builder.AppendLine($"摘要：{result.Summary}");

                        if (!string.IsNullOrWhiteSpace(result.Content))
                        {
                            builder.AppendLine("内容：");
                            builder.AppendLine(result.Content.TrimEnd());
                        }

                        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                            builder.AppendLine($"错误：{result.ErrorMessage}");

                        builder.AppendLine();
                    }
                }
            }

            builder.AppendLine("# 回答要求");
            builder.AppendLine("请只基于以上上下文回答。应用可能已经抓取网页、读取文件或收集日志，但你不能声称自己直接访问了网页、本地文件、日志或设备。");
            builder.AppendLine("如果上下文不足，明确说明缺少什么；如果工具失败，只能基于失败信息说明无法分析真实内容，不能编造未提供的信息。");
            builder.AppendLine(BuildModeInstruction(request.Mode));

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

        private static string TruncateContent(string value, int maxCharacters)
        {
            var content = value ?? string.Empty;
            if (content.Length <= maxCharacters)
                return content;

            return content[..maxCharacters] + Environment.NewLine + $"...<内容已截断，仅保留前 {maxCharacters} 字符。>";
        }
    }
}