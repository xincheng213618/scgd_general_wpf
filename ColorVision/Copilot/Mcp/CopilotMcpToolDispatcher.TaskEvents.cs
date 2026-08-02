#pragma warning disable CA1822,CA1826,CA1859,CA1861
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private CopilotMcpToolCallResult GetAgentTaskEvents(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            int defaultMaxEvents = 50)
        {
            if (executionScope.SourceKind == CopilotExecutionSourceKind.ExternalMcp
                && string.IsNullOrWhiteSpace(executionScope.ConversationId))
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_scope_required",
                    "This MCP session is not bound to a Copilot conversation and cannot read the process-wide Agent task journal.");
            }

            var context = SafeInvoke(_environment.TaskEventJournalProvider);
            if (context?.IsStructurallyValid() != true)
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_unavailable",
                    "No saved Agent task event journal is available for the selected conversation.");
            }
            if (executionScope.SourceKind == CopilotExecutionSourceKind.InAppAgent
                && !string.IsNullOrWhiteSpace(executionScope.ConversationId)
                && !string.Equals(
                    executionScope.ConversationId,
                    context.ConversationId,
                    StringComparison.Ordinal))
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_scope_mismatch",
                    "The saved Agent task event journal belongs to a different conversation.");
            }

            if (!TryGetTaskEventTypes(arguments, out var eventTypes, out var eventTypesError))
                return CopilotMcpToolCallResult.Fail("invalid_arguments", eventTypesError);

            var beforeSequence = GetLong(arguments, "before_sequence");
            if (arguments?.ContainsKey("before_sequence") == true && beforeSequence is null or <= 0)
                return CopilotMcpToolCallResult.Fail("invalid_arguments", "before_sequence must be a positive integer cursor.");
            var maxEvents = GetInt(arguments, "max_events");
            if (arguments?.ContainsKey("max_events") == true
                && (maxEvents is null or <= 0 || maxEvents > CopilotAgentTaskEventJournal.MaxQueryLimit))
            {
                return CopilotMcpToolCallResult.Fail(
                    "invalid_arguments",
                    $"max_events must be between 1 and {CopilotAgentTaskEventJournal.MaxQueryLimit}.");
            }

            var query = new CopilotAgentTaskEventQuery
            {
                Types = eventTypes,
                RunId = GetString(arguments, "run_id"),
                ToolName = GetString(arguments, "tool"),
                SubjectOrRelatedId = GetString(arguments, "related_id"),
                BeforeSequence = beforeSequence ?? long.MaxValue,
                Limit = maxEvents ?? defaultMaxEvents,
            };
            var result = CopilotAgentTaskEventJournal.Query(context.Journal, query);
            var payload = new
            {
                context.ConversationId,
                context.PublishedAtUtc,
                context.Journal.SchemaVersion,
                Events = result.Events,
                result.HasMore,
                result.NextBeforeSequence,
            };
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(payload, StructuredJsonOptions));
        }

        private static bool TryGetTaskEventTypes(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out IReadOnlyCollection<CopilotAgentTaskEventType> eventTypes,
            out string error)
        {
            eventTypes = Array.Empty<CopilotAgentTaskEventType>();
            error = string.Empty;
            if (arguments == null || !arguments.TryGetValue("event_types", out var value))
                return true;
            if (value.ValueKind != JsonValueKind.Array)
            {
                error = "event_types must be an array of Agent task event type names.";
                return false;
            }
            if (value.GetArrayLength() > Enum.GetValues<CopilotAgentTaskEventType>().Length)
            {
                error = "event_types contains more entries than the supported Agent task event type set.";
                return false;
            }

            var parsed = new HashSet<CopilotAgentTaskEventType>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String
                    || !Enum.TryParse<CopilotAgentTaskEventType>(item.GetString(), ignoreCase: true, out var eventType)
                    || !Enum.IsDefined(eventType))
                {
                    error = $"Unknown Agent task event type: {item}.";
                    return false;
                }
                parsed.Add(eventType);
            }
            eventTypes = parsed;
            return true;
        }
    }
}
