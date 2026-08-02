#pragma warning disable CA1822
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        public IReadOnlyList<CopilotMcpResourceDescriptor> ListResources()
        {
            return new[]
            {
                Resource(LiveContextResourceUri, "Current live context", "Current ColorVision Copilot live context snapshot."),
                Resource(WorkspaceResourceUri, "Current workspace", "Current solution directory, active document, and allowed search roots."),
                Resource(LogsResourceUri, "Recent logs", "Recent ColorVision application log lines."),
                Resource(TemplateResourceUri, "Current template", "Current active template JSON editor context, when available."),
                Resource(FlowResourceUri, "Current flow", "Current active flow snapshot and selected node summary, when available."),
                Resource(AuditSummaryResourceUri, "MCP audit summary", "Compact ColorVision MCP audit and pending approval summary."),
                Resource(AuditLogResourceUri, "MCP audit log", "Recent ColorVision MCP tool-call audit entries."),
                Resource(CapabilityCatalogResourceUri, "Copilot capability catalog", "Versioned read-only catalog of built-in and discovered Copilot capabilities.", "application/json"),
                Resource(TaskEventJournalResourceUri, "Copilot Agent task events", "Latest saved bounded and redacted Agent task event journal.", "application/json"),
            };
        }

        public string GetResourceMimeType(string uri)
        {
            var normalizedUri = NormalizeResourceUri(uri);
            return ListResources().FirstOrDefault(resource => string.Equals(resource.Uri, normalizedUri, StringComparison.OrdinalIgnoreCase))?.MimeType
                ?? "text/plain";
        }

        internal async Task<CopilotMcpToolCallResult> ReadResourceAsync(
            string uri,
            string callerSource,
            CancellationToken cancellationToken)
        {
            return await ReadResourceAsync(
                uri,
                CopilotExecutionScope.ForInProcess(callerSource),
                cancellationToken);
        }

        internal async Task<CopilotMcpToolCallResult> ReadResourceAsync(
            string uri,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            if (executionScope == null || executionScope.CallerIdentity.Length == 0)
                return CopilotMcpToolCallResult.Fail("mcp_session_required", "A validated MCP session is required to read resources.");

            executionScope = EnsureWorkspaceScope(executionScope);
            var normalizedUri = NormalizeResourceUri(uri);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            CopilotMcpAuditLogger.ToolCallStarted("resources/read", $"uri={normalizedUri}", executionScope);
            try
            {
                var result = normalizedUri switch
                {
                    LiveContextResourceUri => GetLiveContext(),
                    WorkspaceResourceUri => GetWorkspaceContext(),
                    LogsResourceUri => await GetRecentLogAsync(null, cancellationToken),
                    TemplateResourceUri => GetActiveTemplateContext(),
                    FlowResourceUri => await GetFlowSummaryAsync(cancellationToken),
                    AuditSummaryResourceUri => GetAuditSummary(null, executionScope),
                    AuditLogResourceUri => GetAuditLog(null, executionScope),
                    CapabilityCatalogResourceUri => GetCapabilityCatalog(),
                    TaskEventJournalResourceUri => GetAgentTaskEvents(
                        null,
                        executionScope,
                        CopilotAgentTaskEventJournal.MaxQueryLimit),
                    _ => CopilotMcpToolCallResult.Fail("resource_not_found", $"Unknown ColorVision MCP resource: {uri}"),
                };
                CopilotMcpAuditLogger.ToolCallCompleted("resources/read", result.Success, stopwatch.Elapsed, result.Success ? "OK" : result.Text);
                return result;
            }
            catch (OperationCanceledException)
            {
                CopilotMcpAuditLogger.ToolCallCompleted("resources/read", false, stopwatch.Elapsed, "The MCP resource read was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                var result = CopilotMcpToolCallResult.Fail("resource_read_failed", $"The MCP resource read failed: {CopilotMcpAuditLogger.RedactText(ex.Message)}");
                CopilotMcpAuditLogger.ToolCallCompleted("resources/read", false, stopwatch.Elapsed, result.Text);
                return result;
            }
        }

        private static CopilotMcpResourceDescriptor Resource(
            string uri,
            string name,
            string description,
            string mimeType = "text/plain") => new()
        {
            Uri = uri,
            Name = name,
            Description = description,
            MimeType = mimeType,
        };
    }
}
