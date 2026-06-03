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
                return "- None";

            var builder = new StringBuilder();
            foreach (var stepRecord in stepRecords.TakeLast(Math.Max(1, maxSteps)))
            {
                if (stepRecord == null)
                    continue;

                var toolCall = stepRecord.ToolCall ?? new CopilotToolCall();
                var observation = stepRecord.Observation ?? new CopilotToolObservation();
                var toolName = string.IsNullOrWhiteSpace(toolCall.ToolName) ? "Unknown tool" : toolCall.ToolName;

                builder.Append("- Round ")
                    .Append(stepRecord.Round <= 0 ? "?" : stepRecord.Round)
                    .Append(": ")
                    .Append(toolName);

                if (toolCall.IsFallback)
                    builder.Append(" (fallback)");

                builder.Append(BuildToolInputDetail(toolCall))
                    .AppendLine();

                if (!string.IsNullOrWhiteSpace(toolCall.Reason))
                    builder.Append("  Planning reason: ").AppendLine(toolCall.Reason);

                builder.Append("  Status: ")
                    .Append(observation.Success ? "success" : "failure")
                    .Append("; summary: ")
                    .AppendLine(observation.Summary);

                if (!string.IsNullOrWhiteSpace(observation.ErrorMessage))
                    builder.Append("  Error: ").AppendLine(observation.ErrorMessage);

                if (observation.SuggestedReadableLocalFilePaths.Count > 0)
                {
                    builder.Append("  Candidate files: ")
                        .AppendLine(string.Join(", ", observation.SuggestedReadableLocalFilePaths.Take(3)));
                }

                if (includeContent && !string.IsNullOrWhiteSpace(observation.Content))
                {
                    builder.AppendLine("  Content excerpt:");
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
            builder.AppendLine("# Directly readable local files");
            if (readableLocalFilePaths == null || readableLocalFilePaths.Count == 0)
            {
                builder.AppendLine("- None");
            }
            else
            {
                foreach (var path in readableLocalFilePaths.Take(5))
                    builder.Append("- ").AppendLine(path);
            }

            builder.AppendLine();
            builder.AppendLine("# Directly listable local directories");
            if (request.ReadableLocalDirectoryPaths == null || request.ReadableLocalDirectoryPaths.Count == 0)
            {
                builder.AppendLine("- None");
            }
            else
            {
                foreach (var path in request.ReadableLocalDirectoryPaths.Take(5))
                    builder.Append("- ").AppendLine(path);
            }

            builder.AppendLine();
            builder.AppendLine("# Completed tool observations");
            builder.AppendLine(BuildObservationSummary(stepRecords, MaxPlannerObservationSteps, MaxPlannerObservationContentChars, includeContent: true));

            return builder.ToString().TrimEnd();
        }

        private string BuildAnswerUserMessageContent(CopilotAgentRequest request, IReadOnlyList<CopilotAgentStepRecord> stepRecords)
        {
            var observations = stepRecords ?? Array.Empty<CopilotAgentStepRecord>();
            var builder = new StringBuilder();
            builder.AppendLine("# User question");
            builder.AppendLine((request.UserText ?? string.Empty).Trim());

            var applicationContext = BuildApplicationContext(request.ContextItems);
            var extraAttachmentContext = BuildAdditionalAttachmentContext(request.Attachments);
            var hasObservations = observations.Count > 0;
            if (!string.IsNullOrWhiteSpace(applicationContext) || hasObservations || !string.IsNullOrWhiteSpace(extraAttachmentContext))
            {
                builder.AppendLine();
                builder.AppendLine("# Available context");

                if (!string.IsNullOrWhiteSpace(applicationContext))
                    builder.AppendLine(applicationContext.TrimEnd());

                if (!string.IsNullOrWhiteSpace(extraAttachmentContext))
                    builder.AppendLine(extraAttachmentContext.TrimEnd());

                if (hasObservations)
                {
                    builder.AppendLine("## Tool observations");
                    builder.AppendLine(BuildObservationSummary(observations, observations.Count, MaxAttachmentContentChars, includeContent: true));
                    builder.AppendLine();
                }
            }

            builder.AppendLine("# Answer requirements");
            builder.AppendLine("Answer only from the context above. The application may have fetched web pages, read files, or collected logs, but do not claim direct access to web pages, local files, logs, or devices beyond the provided context.");
            builder.AppendLine("If context is insufficient, state exactly what is missing. If a tool failed, explain only what the failure information supports and do not invent unavailable content.");
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

                builder.Append("## Application context");
                if (!string.IsNullOrWhiteSpace(item.Title))
                    builder.Append(": ").Append(item.Title.Trim());

                builder.AppendLine();

                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("Summary: ").AppendLine(item.Summary.Trim());

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
                    $"## Attached context: {attachment.DisplayLabel}",
                    TruncateContent(attachment.Value, MaxAttachmentContentChars),
                }),
                CopilotAttachmentType.WebPage => string.Join(Environment.NewLine, new[]
                {
                    $"## Attached web page: {attachment.DisplayLabel}",
                    $"Source: {attachment.Source}",
                    TruncateContent(attachment.Value, MaxAttachmentContentChars),
                }),
                CopilotAttachmentType.Image => string.Join(Environment.NewLine, new[]
                {
                    $"## Attached image: {attachment.DisplayLabel}",
                    $"Local image path: {attachment.Value}",
                    "The current version does not upload image pixels to the model; only the image attachment path and title are available.",
                }),
                _ => string.Empty,
            };
        }

        private static string BuildModeInstruction(CopilotAgentMode mode)
        {
            return mode switch
            {
                CopilotAgentMode.Web => "Prioritize the provided web page content. If fetching failed, state that you cannot judge from real page content.",
                CopilotAgentMode.Code => "Prioritize attached files and project context. When needed, name the specific code or files still required.",
                CopilotAgentMode.Diagnose => "Prioritize recent logs, failure details, and context. Separate known facts from hypotheses.",
                CopilotAgentMode.Explain => "Make the conclusion clear and state any limits caused by missing context.",
                _ => "Prioritize the context supplied by the application and do not ignore tool results.",
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
                builder.Append(" (target file: ").Append(System.IO.Path.GetFileName(toolInput.Path));
                if (toolInput.StartLine.HasValue)
                {
                    builder.Append(", lines: ").Append(toolInput.StartLine.Value);
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

                return $" (target directory: {directoryName})";
            }

            if (string.Equals(toolName, "FetchUrl", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Query))
            {
                var url = CopilotWebPageToolSupport.ExtractHttpUrls(toolInput.Query).FirstOrDefault() ?? toolInput.Query;
                return $" (target page: {url})";
            }

            if (string.Equals(toolName, "SearchDocs", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Query))
            {
                return $" (docs query: {toolInput.Query})";
            }

            if (string.Equals(toolName, "ExecuteMenu", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(toolInput.Query))
            {
                return $" (target menu: {toolInput.Query})";
            }

            if (!string.IsNullOrWhiteSpace(toolInput.Query))
                return $" (query: {toolInput.Query})";

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

            return content[..maxCharacters] + Environment.NewLine + $"...<content truncated; kept the first {maxCharacters} characters.>";
        }
    }
}
