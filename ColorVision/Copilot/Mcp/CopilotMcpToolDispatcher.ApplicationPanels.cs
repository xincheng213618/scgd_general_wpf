using ColorVision.Solution.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private async Task<CopilotMcpToolCallResult> OpenPanelAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var panel = GetString(arguments, "panel");
            if (string.IsNullOrWhiteSpace(panel))
                panel = "copilot";

            var panelTarget = ResolvePanelTarget(panel);
            if (panelTarget == null)
            {
                return CopilotMcpToolCallResult.Fail(
                    "panel_alias_not_supported",
                    $"Unsupported panel alias: {panel}. Supported aliases: {string.Join(", ", SupportedPanelAliases)}.");
            }

            if (_environment.OpenPanelHandler != null)
                return await _environment.OpenPanelHandler(panelTarget.Value.Alias, cancellationToken);

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null)
                return CopilotMcpToolCallResult.Fail("layout_unavailable", "The ColorVision docking layout manager is not available.");

            if (!string.Equals(panelTarget.Value.TargetId, CopilotPanelService.PanelId, StringComparison.OrdinalIgnoreCase)
                && !layoutManager.GetRegisteredPanelIds().Contains(panelTarget.Value.TargetId, StringComparer.OrdinalIgnoreCase))
            {
                return CopilotMcpToolCallResult.Fail(
                    "panel_not_registered",
                    $"Panel alias '{panelTarget.Value.Alias}' resolved to '{panelTarget.Value.TargetId}', but that panel is not registered. Supported aliases: {string.Join(", ", SupportedPanelAliases)}.");
            }

            await CopilotUiDispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(panelTarget.Value.TargetId, CopilotPanelService.PanelId, StringComparison.OrdinalIgnoreCase))
                    CopilotPanelService.GetInstance().ShowPanel();
                else
                    layoutManager.ShowPanel(panelTarget.Value.TargetId);
            }, cancellationToken);

            return CopilotMcpToolCallResult.Ok($"Panel open request was scheduled: alias={panelTarget.Value.Alias}, target={panelTarget.Value.TargetId}, risk=low-risk-action.");
        }

        private static CopilotPanelTarget? ResolvePanelTarget(string panel)
        {
            var alias = (panel ?? string.Empty).Trim();
            var normalizedAlias = alias.ToLowerInvariant();
            var targetId = normalizedAlias switch
            {
                "" => CopilotPanelService.PanelId,
                "copilot" => CopilotPanelService.PanelId,
                "log" => "LogPanel",
                "solution" => "ProjectPanel",
                "config" => "ProjectPanel",
                "template" => "ProjectPanel",
                "device" => "AcquirePanel",
                _ => string.Empty,
            };

            if (string.IsNullOrWhiteSpace(targetId))
                return null;

            return new CopilotPanelTarget(string.IsNullOrWhiteSpace(normalizedAlias) ? "copilot" : normalizedAlias, targetId);
        }

        private async Task<CopilotMcpToolCallResult> ExecuteMenuAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The execute_menu tool requires a non-empty query argument.");

            var dryRun = GetBool(arguments, "dry_run") ?? true;
            var frameworkApproved = IsInAppAgentFrameworkApproved(executionScope);
            var inAppAgent = IsInAppAgent(executionScope);

            if (inAppAgent && !dryRun)
            {
                if (_environment.ExecuteMenuHandler != null)
                {
                    var handlerPreview = await _environment.ExecuteMenuHandler(query, true, cancellationToken);
                    if (!handlerPreview.Success)
                        return handlerPreview;

                    return CreateConfirmableActionResult(
                        "Confirm menu command",
                        $"Execute ColorVision menu command: {query}",
                        CopilotSharedCapabilityCatalog.ExecuteMenu.McpToolName,
                        arguments,
                        handlerPreview.Text,
                        token => _environment.ExecuteMenuHandler(query, false, token),
                        executeOnApproval: true,
                        executionScope: executionScope);
                }

                if (Application.Current == null)
                    return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

                var applicationPreview = await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: true, allowConfirmationRequired: false, cancellationToken);
                if (!applicationPreview.Success)
                    return ToMcpResult(applicationPreview, "menu_preview_failed");

                return CreateConfirmableActionResult(
                    "Confirm menu command",
                    $"Execute ColorVision menu command: {query}",
                    CopilotSharedCapabilityCatalog.ExecuteMenu.McpToolName,
                    arguments,
                    string.Join(Environment.NewLine, new[] { applicationPreview.Summary, applicationPreview.Content }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    async token => ToMcpResult(await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: false, allowConfirmationRequired: true, token), "menu_execution_failed"),
                    executeOnApproval: true,
                    executionScope: executionScope);
            }

            if (_environment.ExecuteMenuHandler != null)
            {
                var handlerResult = await _environment.ExecuteMenuHandler(query, dryRun, cancellationToken);
                if (!dryRun && IsConfirmationRequiredResult(handlerResult))
                {
                    if (frameworkApproved)
                        return await _environment.ExecuteMenuHandler(query, false, cancellationToken);

                    return CreateConfirmableActionResult(
                        "Confirm menu command",
                        $"Execute ColorVision menu command: {query}",
                        CopilotSharedCapabilityCatalog.ExecuteMenu.McpToolName,
                        arguments,
                        handlerResult.Text,
                        token => _environment.ExecuteMenuHandler(query, false, token),
                        executeOnApproval: IsInAppAgent(executionScope),
                        executionScope: executionScope);
                }

                return handlerResult;
            }

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            var result = await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun, allowConfirmationRequired: frameworkApproved, cancellationToken);
            if (!dryRun && IsConfirmationRequiredResult(result))
            {
                return CreateConfirmableActionResult(
                    "Confirm menu command",
                    $"Execute ColorVision menu command: {query}",
                    CopilotSharedCapabilityCatalog.ExecuteMenu.McpToolName,
                    arguments,
                    string.Join(Environment.NewLine, new[] { result.Summary, result.Content, result.ErrorMessage }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    async token => ToMcpResult(await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: false, allowConfirmationRequired: true, token), "menu_execution_failed"),
                    executeOnApproval: IsInAppAgent(executionScope),
                    executionScope: executionScope);
            }

            return ToMcpResult(result, "menu_execution_failed");
        }
    }
}
