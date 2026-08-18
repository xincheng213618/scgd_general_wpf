using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal static class CopilotToolOutputArchivePolicy
    {
        public const string RetrievalToolName = "ReadToolOutput";

        private static readonly HashSet<string> ExcludedToolNames = new(
            StringComparer.OrdinalIgnoreCase)
        {
            RetrievalToolName,
            "RunShellCommand",
            "ReadShellCommandOutput",
            "ReadBackgroundShellCommandOutput",
        };

        public static string Format(
            CopilotToolExecutionOutcome outcome,
            int? toolOutputTokenLimit,
            CopilotToolOutputArchiveRegistry? registry = null)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            lock (outcome)
            {
                if (outcome.FormattedModelResult != null)
                    return outcome.FormattedModelResult;

                var initial = CopilotFrameworkToolResultFormatter.FormatDetailed(
                    outcome,
                    toolOutputTokenLimit);
                if (!ShouldArchive(outcome, initial))
                    return outcome.FormattedModelResult = initial.Content;

                registry ??= CopilotToolOutputArchiveRegistry.Shared;
                var snapshot = registry.Retain(
                    outcome.Invocation.AgentRequest.ConversationId,
                    outcome.Execution.ToolName,
                    outcome.Execution.CallId,
                    outcome.EffectiveModelResult.Content);
                if (snapshot == null)
                    return outcome.FormattedModelResult = initial.Content;

                outcome.ToolOutputArchive = snapshot;
                var archived = CopilotFrameworkToolResultFormatter.FormatDetailed(
                    outcome,
                    toolOutputTokenLimit);
                if (archived.ArchiveReferenceIncluded)
                    return outcome.FormattedModelResult = archived.Content;

                outcome.ToolOutputArchive = null;
                registry.Remove(snapshot.Id);
                return outcome.FormattedModelResult = initial.Content;
            }
        }

        private static bool ShouldArchive(
            CopilotToolExecutionOutcome outcome,
            CopilotFrameworkToolResultFormatResult formatted)
        {
            var result = outcome.EffectiveModelResult;
            return formatted.ContentTruncated
                && result.Success
                && result.Approval == null
                && !result.SuppressModelOutput
                && !string.IsNullOrEmpty(result.Content)
                && outcome.Invocation?.AgentRequest != null
                && outcome.Invocation.Tool != null
                && !ExcludedToolNames.Contains(outcome.Invocation.Tool.Name)
                && !outcome.Invocation.Tool.Name.Contains(
                    "BackgroundShellCommand",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
