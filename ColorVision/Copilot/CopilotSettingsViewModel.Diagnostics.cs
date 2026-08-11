#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        private void RefreshMcpDiagnostics()
        {
            RefreshExternalMcpClientsStatus(_sourceConfig.ExternalMcpServers);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(20);
            var failureCount = entries.Count(CopilotMcpAuditLogger.IsRealFailureEntry);
            var approvalFlowCount = entries.Count(CopilotMcpAuditLogger.IsApprovalFlowEntry);
            var capabilityCatalog = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var subagentCatalog = CopilotSubagentRoleCatalog.Default;
            RefreshSubagentRoleDiagnostics(subagentCatalog);

            var server = CopilotMcpServer.Instance;
            var pendingCount = CopilotMcpConfirmationStore.Instance.PendingCount;
            var lastEntry = entries.Count > 0 ? entries[entries.Count - 1] : null;
            var lastActivity = lastEntry == null
                ? "No recent MCP activity."
                : $"{FormatAuditEntryForSummary(lastEntry)}.";

            McpDiagnosticsSummaryText =
                $"Capabilities: {capabilityCatalog.Capabilities.Count} (revision {capabilityCatalog.Revision}); subagent roles: {subagentCatalog.Roles.Count}; recent calls: {entries.Count}; failures: {failureCount}; approval events: {approvalFlowCount}; pending actions: {pendingCount}. {lastActivity}";

            var lastError = CopilotMcpAuditLogger.GetLastError();
            McpLastErrorText = lastError == null
                ? "Last error: none."
                : $"Last error: {FormatAuditEntryForSummary(lastError)} - {lastError.ErrorMessage}";

            McpServiceSummaryText = BuildMcpServiceSummary(server);
            McpActivitySummaryText = BuildMcpActivitySummary(entries.Count, failureCount, approvalFlowCount);
            McpPendingSummaryText = pendingCount == 0
                ? "None"
                : $"{pendingCount} pending";
            McpErrorSummaryText = lastError == null
                ? "None"
                : $"{lastError.ToolName} failed";
            McpDiagnosticsHeaderText = BuildMcpDiagnosticsHeader(failureCount, pendingCount);

            McpRecentAuditText = entries.Count == 0
                ? "No audit entries yet."
                : string.Join(Environment.NewLine, entries.Select(FormatAuditEntryForDetails));

            if (!McpEnabled && !server.IsRunning && entries.Count == 0)
            {
                McpDiagnosticsSummaryText = $"MCP is disabled. Capability catalog: {capabilityCatalog.Capabilities.Count} item(s), revision {capabilityCatalog.Revision}.";
                McpServiceSummaryText = "Disabled";
                McpActivitySummaryText = "No calls";
                McpPendingSummaryText = "None";
                McpErrorSummaryText = "None";
                McpDiagnosticsHeaderText = "Diagnostics";
            }
        }

        private void RefreshSubagentRoleDiagnostics(CopilotSubagentRoleCatalog catalog)
        {
            SubagentRolesSummaryText = $"{catalog.Roles.Count} built-in role(s); catalog revision {catalog.Revision}.";

            var lines = new List<string>();
            foreach (var role in catalog.Roles.OrderBy(role => role.Id, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"[built-in] {role.DisplayName} ({role.ToolName})");
                lines.Add($"  source={role.SourceName} [{role.SourceId}] version={role.SourceVersion}");
                lines.Add($"  domain={role.ContextScope}; tools={FormatSubagentCapabilities(role.ReadCapabilities)}; child={role.ChildMode}; parents={string.Join(",", role.ParentModes)}");
                lines.Add($"  fingerprint={role.CapabilityFingerprint}");
            }
            SubagentRolesDiagnosticsText = lines.Count == 0 ? "No subagent roles registered." : string.Join(Environment.NewLine, lines);
        }

        private void LoadAgentSkillSettings(IEnumerable<CopilotAgentSkillOverrideConfig> overrides)
        {
            AgentSkillSettings.Clear();
            foreach (var item in CopilotAgentSkillOverrideConfig.Normalize(overrides))
            {
                AgentSkillSettings.Add(new CopilotAgentSkillSetting(
                    item.Name,
                    item.State,
                    usage: null,
                    isHistoricalExplicitOnly: false,
                    OnAgentSkillSettingChanged,
                    item.SkillFilePath));
            }
        }

        private void SynchronizeAgentSkillSettings(CopilotAgentSkillUsageSnapshot snapshot)
        {
            var pendingSettings = AgentSkillSettings
                .GroupBy(setting => setting.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();
            var pendingNameStates = pendingSettings
                .Where(setting => !setting.HasExactPath)
                .ToDictionary(setting => setting.Name, setting => setting.State, StringComparer.OrdinalIgnoreCase);
            var usageByName = snapshot.Entries.ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
            var historicalNames = snapshot.HistoricalExplicitOnlySkills.Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = pendingNameStates.Keys.Concat(usageByName.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

            AgentSkillSettings.Clear();
            foreach (var name in names)
            {
                usageByName.TryGetValue(name, out var usage);
                AgentSkillSettings.Add(new CopilotAgentSkillSetting(
                    name,
                    pendingNameStates.GetValueOrDefault(name, CopilotAgentSkillOverrideState.Auto),
                    usage,
                    historicalNames.Contains(name),
                    OnAgentSkillSettingChanged));
            }
            foreach (var setting in pendingSettings
                .Where(setting => setting.HasExactPath)
                .OrderBy(setting => setting.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(setting => setting.SkillFilePath, StringComparer.OrdinalIgnoreCase))
            {
                AgentSkillSettings.Add(new CopilotAgentSkillSetting(
                    setting.Name,
                    setting.State,
                    usage: null,
                    isHistoricalExplicitOnly: false,
                    OnAgentSkillSettingChanged,
                    setting.SkillFilePath));
            }
            OnPropertyChanged(nameof(CanAddAgentSkillOverride));
        }

        private void AddAgentSkillOverride()
        {
            var name = CopilotAgentSkillOverrideConfig.NormalizeName(NewAgentSkillName);
            if (name.Length == 0 || AgentSkillSettings.Any(setting =>
                !setting.HasExactPath
                && string.Equals(setting.Name, name, StringComparison.OrdinalIgnoreCase)))
                return;

            AgentSkillSettings.Add(new CopilotAgentSkillSetting(
                name,
                CopilotAgentSkillOverrideState.UserInvocableOnly,
                usage: null,
                isHistoricalExplicitOnly: false,
                OnAgentSkillSettingChanged));
            NewAgentSkillName = string.Empty;
            OnAgentSkillSettingChanged();
        }

        private void RemoveAgentSkillOverride(CopilotAgentSkillSetting? setting)
        {
            if (setting == null)
                return;
            if (setting.IsTracked)
            {
                setting.State = CopilotAgentSkillOverrideState.Auto;
                return;
            }

            if (AgentSkillSettings.Remove(setting))
            {
                OnPropertyChanged(nameof(CanAddAgentSkillOverride));
                OnAgentSkillSettingChanged();
            }
        }

        private void OnAgentSkillSettingChanged()
        {
            RefreshAgentSkillSummaryText();
            MarkSettingsPending("Agent Skill 策略已更改；单击“应用”或“保存”后更新模型 Skill 目录。");
        }

        private void RefreshAgentSkillSummaryText()
        {
            var snapshot = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            var overrideCount = AgentSkillSettings.Count(setting => setting.State != CopilotAgentSkillOverrideState.Auto);
            AgentSkillsSummaryText = CopilotAgentSkillDiagnostics.FormatSummary(snapshot) + $" 手动覆盖 {overrideCount} 个。";
        }

        private void RefreshAgentSkillDiagnostics()
        {
            try
            {
                var snapshot = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
                SynchronizeAgentSkillSettings(snapshot);
                RefreshAgentSkillSummaryText();
                AgentSkillsDiagnosticsText = CopilotAgentSkillDiagnostics.FormatEntries(snapshot);
            }
            catch (Exception ex)
            {
                AgentSkillsSummaryText = "Skill 使用历史当前不可用。";
                AgentSkillsDiagnosticsText = SanitizeError(ex.Message);
            }
        }

        private static string FormatSubagentCapabilities(CopilotSubagentReadCapabilities capabilities)
        {
            var names = new List<string>();
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.SearchFiles))
                names.Add(nameof(CopilotSubagentReadCapabilities.SearchFiles));
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.GrepText))
                names.Add(nameof(CopilotSubagentReadCapabilities.GrepText));
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.ReadLocalFile))
                names.Add(nameof(CopilotSubagentReadCapabilities.ReadLocalFile));
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.ListDirectory))
                names.Add(nameof(CopilotSubagentReadCapabilities.ListDirectory));
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.WebSearch))
                names.Add(nameof(CopilotSubagentReadCapabilities.WebSearch));
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.FetchUrl))
                names.Add(nameof(CopilotSubagentReadCapabilities.FetchUrl));
            return names.Count == 0 ? "None" : string.Join(",", names);
        }

        private void CopyMcpDiagnostics()
        {
            try
            {
                Clipboard.SetText(BuildMcpDiagnosticsClipboardText());
                McpConnectionTestText = "MCP diagnostics copied.";
                SetSettingsNotice("MCP diagnostics copied. Sensitive token values are redacted.");
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
                SetSettingsNotice("Copy failed: " + SanitizeError(ex.Message));
            }
        }

        private string BuildMcpDiagnosticsClipboardText()
        {
            RefreshMcpDiagnostics();

            var server = CopilotMcpServer.Instance;
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP diagnostics");
            builder.AppendLine($"Enabled: {McpEnabled}");
            builder.AppendLine($"Running: {server.IsRunning}");
            builder.AppendLine($"Endpoint: {McpEndpoint}");
            builder.AppendLine($"Status: {McpStatusText}");
            builder.AppendLine($"Server message: {SanitizeError(server.LastStatusMessage)}");
            builder.AppendLine($"Service summary: {McpServiceSummaryText}");
            builder.AppendLine($"Activity summary: {McpActivitySummaryText}");
            builder.AppendLine($"Pending summary: {McpPendingSummaryText}");
            builder.AppendLine($"Error summary: {McpErrorSummaryText}");
            var capabilityCatalog = CopilotCapabilityCatalog.Shared.GetSnapshot();
            builder.AppendLine($"Capability catalog: {capabilityCatalog.Capabilities.Count} item(s) from {capabilityCatalog.SourceCount} source(s), revision {capabilityCatalog.Revision}");
            builder.AppendLine(McpDiagnosticsSummaryText);
            builder.AppendLine(McpLastErrorText);
            builder.AppendLine();
            builder.AppendLine("Subagent roles:");
            builder.AppendLine(SubagentRolesSummaryText);
            builder.AppendLine(SubagentRolesDiagnosticsText);
            builder.AppendLine();
            builder.AppendLine("Recent audit entries:");
            builder.AppendLine(McpRecentAuditText);
            return builder.ToString().TrimEnd();
        }

        private static string FormatAuditEntryForSummary(CopilotMcpAuditEntry entry)
        {
            var result = GetAuditEntryResultLabel(entry);
            return $"{entry.ToolName} {result} at {FormatLocalTime(entry.TimestampUtc)}";
        }

        private static string FormatAuditEntryForDetails(CopilotMcpAuditEntry entry)
        {
            var result = GetAuditEntryResultLabel(entry);
            var message = string.IsNullOrWhiteSpace(entry.ErrorMessage)
                ? string.Empty
                : " - " + entry.ErrorMessage;
            var caller = string.IsNullOrWhiteSpace(entry.CallerSource)
                ? string.Empty
                : $" caller={entry.CallerSource}";

            return $"{FormatLocalTime(entry.TimestampUtc)} {entry.ToolName} {result} {entry.DurationMs}ms{caller}{message}";
        }

        private static string GetAuditEntryResultLabel(CopilotMcpAuditEntry entry)
        {
            if (entry.Success)
                return "OK";

            return CopilotMcpAuditLogger.IsApprovalFlowEntry(entry) ? "approval" : "failed";
        }

        private static string FormatLocalTime(DateTimeOffset timestamp)
        {
            return timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
        }

        private string BuildMcpServiceSummary(CopilotMcpServer server)
        {
            if (McpStatusText.StartsWith("Unsaved changes.", StringComparison.OrdinalIgnoreCase))
                return "Pending save";

            if (!McpEnabled)
                return "Disabled";

            if (string.IsNullOrWhiteSpace(McpBearerToken))
                return "Token missing";

            if (server.IsRunning)
                return "Running";

            var message = server.LastStatusMessage ?? string.Empty;
            if (message.Contains("port", StringComparison.OrdinalIgnoreCase)
                || message.Contains("address", StringComparison.OrdinalIgnoreCase)
                || message.Contains("only one usage", StringComparison.OrdinalIgnoreCase))
            {
                return "Port unavailable";
            }

            return "Stopped";
        }

        private static string BuildMcpActivitySummary(int entryCount, int failureCount, int approvalFlowCount)
        {
            if (entryCount == 0)
                return "No calls";

            if (failureCount == 0 && approvalFlowCount == 0)
                return $"{entryCount} calls";

            if (failureCount == 0)
                return $"{entryCount} calls, {approvalFlowCount} reviews";

            if (approvalFlowCount == 0)
                return $"{entryCount} calls, {failureCount} failures";

            return $"{entryCount} calls, {failureCount} failures, {approvalFlowCount} reviews";
        }

        private static string BuildMcpDiagnosticsHeader(int failureCount, int pendingCount)
        {
            if (pendingCount > 0 && failureCount > 0)
                return $"Diagnostics ({pendingCount} pending, {failureCount} failures)";

            if (pendingCount > 0)
                return $"Diagnostics ({pendingCount} pending)";

            if (failureCount > 0)
                return $"Diagnostics ({failureCount} failures)";

            return "Diagnostics";
        }

    }
}
