#pragma warning disable CA1822
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private CopilotMcpToolCallResult GetServerStatus(CopilotExecutionScope executionScope)
        {
            var settings = _environment.RuntimeSettingsProvider();
            var isRunning = SafeInvoke(_environment.ServerRunningProvider);
            var statusMessage = SafeInvoke(_environment.ServerStatusMessageProvider) ?? string.Empty;
            var runtimeActivity = GetRuntimeActivity();

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP server status");
            builder.AppendLine("ColorVision process: running");
            builder.AppendLine("Authentication: passed for this request");
            builder.AppendLine($"MCP enabled: {settings.Enabled}");
            builder.AppendLine($"Listener running: {isRunning}");
            builder.AppendLine($"Endpoint: {settings.Endpoint}");
            builder.AppendLine($"Host: {settings.Host}");
            builder.AppendLine($"Port: {settings.Port}");
            builder.AppendLine($"Caller/source: {EmptyLabel(executionScope.CallerIdentity)}");
            builder.AppendLine($"Execution scope: {executionScope.ScopeId}");
            builder.AppendLine($"Status message: {EmptyLabel(statusMessage)}");
            builder.AppendLine($"Active Copilot runs: {runtimeActivity.ActiveRuns}");
            builder.AppendLine($"Queued Copilot runs: {runtimeActivity.QueuedRuns}");
            builder.AppendLine($"Pending actions: {GetPendingActionsForScope(executionScope).Count}");
            builder.AppendLine("Safety boundary: no shell, no device control, no flow execution, no config mutation, no file deletion, and no arbitrary file read.");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetEnabledTools()
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP enabled tools");
            var categoryOrder = new[] { "status", "context", "search", "file", "app-control", "audit" };
            var tools = ListTools()
                .OrderBy(tool => Array.IndexOf(categoryOrder, tool.Category) < 0 ? int.MaxValue : Array.IndexOf(categoryOrder, tool.Category))
                .ThenBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .GroupBy(tool => string.IsNullOrWhiteSpace(tool.Category) ? "other" : tool.Category, StringComparer.OrdinalIgnoreCase);

            foreach (var group in tools)
            {
                builder.AppendLine();
                builder.AppendLine($"## {group.Key}");
                foreach (var tool in group)
                {
                    builder.AppendLine($"- {tool.Name} [{tool.RiskLevel}]: {tool.Description}");
                    builder.AppendLine($"  Example: {tool.UsageExample}");
                }
            }

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetAuditLog(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope)
        {
            var maxEntries = Math.Clamp(GetInt(arguments, "max_entries") ?? MaxAuditEntries, 1, 200);
            var toolFilter = NormalizeToolName(GetString(arguments, "tool"));
            var failedOnly = GetBool(arguments, "failed_only") ?? false;
            var entries = CopilotMcpAuditLogger.GetRecentEntries(200)
                .Where(entry => AuditEntryMatchesScope(entry, executionScope))
                .Where(entry => string.IsNullOrWhiteSpace(toolFilter) || string.Equals(entry.ToolName, toolFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry => !failedOnly || !entry.Success)
                .TakeLast(maxEntries)
                .ToArray();
            return CopilotMcpToolCallResult.Ok(FormatAuditEntries(entries, "ColorVision MCP audit log"));
        }

        private CopilotMcpToolCallResult GetAuditSummary(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope)
        {
            var maxEntries = Math.Clamp(GetInt(arguments, "max_entries") ?? 50, 1, 200);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(200)
                .Where(entry => AuditEntryMatchesScope(entry, executionScope))
                .TakeLast(maxEntries)
                .ToArray();
            var pendingActions = GetPendingActionsForScope(executionScope);
            var unsuccessfulEntries = entries.Where(entry => !entry.Success).ToArray();
            var approvalFlowEntries = unsuccessfulEntries.Where(IsApprovalFlowAuditEntry).ToArray();
            var failedEntries = unsuccessfulEntries.Where(IsRealFailureAuditEntry).ToArray();
            var lastEntry = entries.LastOrDefault();
            var lastFailure = failedEntries.LastOrDefault();
            var lastApprovalFlowEntry = approvalFlowEntries.LastOrDefault();
            var topFailures = failedEntries
                .GroupBy(entry => string.IsNullOrWhiteSpace(entry.ToolName) ? "(unknown)" : entry.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    ToolName = group.Key,
                    Count = group.Count(),
                    LastFailure = group.Last(),
                })
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.LastFailure.TimestampUtc)
                .Take(5)
                .ToArray();
            var callers = entries
                .Select(entry => EmptyLabel(entry.CallerSource))
                .Where(caller => !string.Equals(caller, "(none)", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP audit summary");
            builder.AppendLine($"Entries summarized: {entries.Length}");
            builder.AppendLine($"Successful entries: {entries.Count(entry => entry.Success)}");
            builder.AppendLine($"Raw unsuccessful entries: {unsuccessfulEntries.Length}");
            builder.AppendLine($"Real failure entries: {failedEntries.Length}");
            builder.AppendLine($"Approval-flow entries: {approvalFlowEntries.Length}");
            builder.AppendLine($"Pending approvals: {pendingActions.Count}");
            builder.AppendLine($"Last entry: {FormatAuditEntryOneLine(lastEntry)}");
            builder.AppendLine($"Last real failure: {FormatAuditEntryOneLine(lastFailure)}");
            builder.AppendLine($"Last approval-flow event: {FormatAuditEntryOneLine(lastApprovalFlowEntry)}");
            builder.AppendLine($"Recent callers: {(callers.Length == 0 ? "(none)" : string.Join(", ", callers))}");

            builder.AppendLine();
            builder.AppendLine("Top failures");
            if (topFailures.Length == 0)
                builder.AppendLine("- None");
            else
                foreach (var failure in topFailures)
                    builder.AppendLine($"- {failure.ToolName}: {failure.Count} failure(s); latest {failure.LastFailure.TimestampUtc:O}; error={EmptyLabel(failure.LastFailure.ErrorMessage)}");

            builder.AppendLine();
            builder.AppendLine("Pending approvals");
            if (pendingActions.Count == 0)
                builder.AppendLine("- None");
            else
            {
                foreach (var action in pendingActions.Take(8))
                    builder.AppendLine($"- tool={action.ToolName}; risk={action.RiskLevel}; status={action.StatusLabel}; expires_at={action.ExpiresAt:O}; title={action.Title}");
                if (pendingActions.Count > 8)
                    builder.AppendLine($"- ... {pendingActions.Count - 8} more pending approval(s)");
            }

            builder.AppendLine();
            builder.AppendLine("Next step hints");
            if (pendingActions.Count > 0)
                builder.AppendLine("- Ask the ColorVision user to approve or reject pending actions before calling confirm_action.");
            if (approvalFlowEntries.Length > 0 && pendingActions.Count == 0)
                builder.AppendLine("- Approval-flow entries are not counted as real failures; inspect pending approvals or get_audit_log when reviewing user decisions.");
            if (lastFailure != null)
                builder.AppendLine("- Call get_last_tool_error or get_audit_log with failed_only=true for failure details.");
            if (entries.Length == 0)
                builder.AppendLine("- No MCP activity has been recorded yet.");

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetLastToolError(CopilotExecutionScope executionScope)
        {
            var entry = CopilotMcpAuditLogger.GetRecentEntries(200)
                .Where(item => AuditEntryMatchesScope(item, executionScope))
                .LastOrDefault(IsRealFailureAuditEntry);
            if (entry == null)
                return CopilotMcpToolCallResult.Ok("No failed MCP tool call is recorded.");

            return CopilotMcpToolCallResult.Ok(FormatAuditEntries(new[] { entry }, "Last ColorVision MCP tool error"));
        }

        private async Task<CopilotMcpToolCallResult> GetRuntimeEnvironmentSummaryAsync(CancellationToken cancellationToken)
        {
            var settings = _environment.RuntimeSettingsProvider();
            var workspace = GetWorkspaceSnapshot();
            var liveContext = _environment.LiveContextProvider();
            var flowSnapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            var logResult = await _environment.RecentLogProvider(null, CopilotRecentLogMode.RecentLines, 20, 4000, cancellationToken);
            using var process = Process.GetCurrentProcess();
            var appDataDirectory = SafeInvoke(() => Environments.DirAppData) ?? string.Empty;
            var configDirectory = string.IsNullOrWhiteSpace(appDataDirectory) ? string.Empty : Path.Combine(appDataDirectory, "Config");
            var logFilePath = SafeInvoke(() => Environments.DirLog) ?? string.Empty;
            var theme = SafeInvoke(() => ThemeConfig.Instance.Theme.ToString()) ?? "(unknown)";
            var runtimeActivity = GetRuntimeActivity();
            var residentMemoryBytes = SafeInvoke(_environment.ResidentMemoryBytesProvider);
            var residentMemoryLabel = residentMemoryBytes is long bytes && bytes >= 0
                ? bytes.ToString(CultureInfo.InvariantCulture)
                : "(unavailable)";
            var logDirectories = SafeInvoke(() => CopilotRecentLogSupport.GetCandidateLogDirectories()
                .Where(directory => !string.IsNullOrWhiteSpace(directory))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()) ?? Array.Empty<string>();

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP runtime environment summary");
            builder.AppendLine($"ColorVision version: {EmptyLabel(typeof(CopilotMcpToolDispatcher).Assembly.GetName().Version?.ToString())}");
            builder.AppendLine($"Process: {process.ProcessName} ({process.Id})");
            builder.AppendLine($"Process start time: {EmptyLabel(SafeInvoke(() => process.StartTime.ToString("O", CultureInfo.InvariantCulture)))}");
            builder.AppendLine($"Resident memory bytes: {residentMemoryLabel}");
            builder.AppendLine($"Base directory: {EmptyLabel(AppDomain.CurrentDomain.BaseDirectory)}");
            builder.AppendLine($"Config directory: {EmptyLabel(configDirectory)}");
            builder.AppendLine($"AppData directory: {EmptyLabel(appDataDirectory)}");
            builder.AppendLine($"Log file path: {EmptyLabel(logFilePath)}");
            builder.AppendLine($"Candidate log directories: {logDirectories.Length}");
            foreach (var directory in logDirectories)
                builder.AppendLine($"- {directory}");
            builder.AppendLine($"Current UI culture: {EmptyLabel(Thread.CurrentThread.CurrentUICulture.Name)}");
            builder.AppendLine($"Current culture: {EmptyLabel(Thread.CurrentThread.CurrentCulture.Name)}");
            builder.AppendLine($"Theme: {theme}");
            builder.AppendLine($"MCP enabled: {settings.Enabled}");
            builder.AppendLine($"MCP listener running: {SafeInvoke(_environment.ServerRunningProvider)}");
            builder.AppendLine($"Endpoint: {settings.Endpoint}");
            builder.AppendLine($"MCP status message: {EmptyLabel(SafeInvoke(_environment.ServerStatusMessageProvider))}");
            builder.AppendLine($"Active Copilot runs: {runtimeActivity.ActiveRuns}");
            builder.AppendLine($"Queued Copilot runs: {runtimeActivity.QueuedRuns}");
            builder.AppendLine($"Workspace solution directory: {EmptyLabel(workspace.SolutionDirectoryPath)}");
            builder.AppendLine($"Active document: {EmptyLabel(workspace.ActiveDocumentPath)}");
            builder.AppendLine($"Allowed search roots: {workspace.SearchRootPaths.Count}");
            foreach (var root in workspace.SearchRootPaths)
                builder.AppendLine($"- {root}");
            builder.AppendLine($"Live context source: {EmptyLabel(liveContext?.SourceId)}");
            builder.AppendLine($"Live context title: {EmptyLabel(liveContext?.Title)}");
            builder.AppendLine($"Flow snapshot available: {flowSnapshot != null}");
            builder.AppendLine($"Flow running: {flowSnapshot?.IsRunning.ToString() ?? "(unknown)"}");
            builder.AppendLine($"Selected flow nodes: {flowSnapshot?.Nodes.Count(node => node.IsSelected) ?? 0}");
            builder.AppendLine($"Recent log available: {logResult.Success}");
            builder.AppendLine($"Recent audit entries: {CopilotMcpAuditLogger.GetRecentEntries(MaxAuditEntries).Count}");
            builder.AppendLine($"Pending actions: {CopilotMcpConfirmationStore.Instance.PendingCount}");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private (int ActiveRuns, int QueuedRuns) GetRuntimeActivity() => (
            Math.Max(0, SafeInvoke(_environment.ActiveCopilotRunCountProvider)),
            Math.Max(0, SafeInvoke(_environment.QueuedCopilotRunCountProvider)));

        private async Task<CopilotMcpToolCallResult> GetDiagnosticBundleAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            var maxChars = Math.Clamp(GetInt(arguments, "max_chars") ?? DefaultDiagnosticBundleChars, 1000, MaxDiagnosticBundleChars);
            var recentLog = await GetRecentLogAsync(new Dictionary<string, JsonElement>
            {
                ["max_lines"] = JsonSerializer.SerializeToElement(120),
            }, cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP diagnostic bundle");
            builder.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
            builder.AppendLine($"Max chars: {maxChars}");
            AppendDiagnosticSection(builder, "server_status", GetServerStatus(executionScope).Text);
            AppendDiagnosticSection(builder, "runtime_environment_summary", (await GetRuntimeEnvironmentSummaryAsync(cancellationToken)).Text);
            AppendDiagnosticSection(builder, "last_tool_error", GetLastToolError(executionScope).Text);
            AppendDiagnosticSection(builder, "recent_log", recentLog.Text);
            AppendDiagnosticSection(builder, "live_context", GetLiveContext().Text);
            AppendDiagnosticSection(builder, "flow_summary", (await GetFlowSummaryAsync(cancellationToken)).Text);

            var redacted = RedactForDiagnostics(builder.ToString().TrimEnd());
            return CopilotMcpToolCallResult.Ok(TruncateWithLimit(redacted, maxChars));
        }

        private CopilotMcpToolCallResult GetLiveContext()
        {
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null)
                return CopilotMcpToolCallResult.Ok("No live context is currently published.");

            return CopilotMcpToolCallResult.Ok(FormatLiveContext(liveContext));
        }

        private CopilotMcpToolCallResult GetWorkspaceContext()
        {
            var snapshot = GetWorkspaceSnapshot();
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision workspace context");
            builder.AppendLine($"Solution directory: {EmptyLabel(snapshot.SolutionDirectoryPath)}");
            builder.AppendLine($"Active document: {EmptyLabel(snapshot.ActiveDocumentPath)}");
            builder.AppendLine($"Allowed search roots: {snapshot.SearchRootPaths.Count}");
            foreach (var root in snapshot.SearchRootPaths)
                builder.AppendLine($"- {root}");

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }
    }
}
