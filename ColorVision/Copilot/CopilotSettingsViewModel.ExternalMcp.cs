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
    public sealed partial class CopilotSettingsViewModel : ViewModelBase, IDisposable
    {
        private bool ValidateExternalMcpServers(bool updateNotice)
        {
            if (CopilotMcpClientConfigurationText.TryParse(ExternalMcpServersText, out var servers, out var error))
            {
                IsExternalMcpServersValid = true;
                ExternalMcpServersValidationText = servers.Count == 0
                    ? "Optional. Add an exact tool list in the fifth field to limit what Copilot can discover."
                    : $"{servers.Count} external MCP server(s) configured. Exact tool lists are recommended; tokens are read only from environment variables.";
                RefreshExternalMcpClientsStatus(servers);
                return true;
            }

            IsExternalMcpServersValid = false;
            ExternalMcpServersValidationText = error;
            ExternalMcpClientsStatusText = "Fix the external MCP configuration before refreshing discovery.";
            ExternalMcpClientStatuses.Clear();
            if (updateNotice)
                SetSettingsNotice(error);
            return false;
        }

        private void RefreshExternalMcpClientsStatus(IEnumerable<CopilotMcpClientServerConfig>? servers)
        {
            var configuredServers = servers?.Where(server => server?.Enabled == true).Take(8).ToArray()
                ?? Array.Empty<CopilotMcpClientServerConfig>();
            if (configuredServers.Length == 0)
            {
                ExternalMcpClientsStatusText = "No external MCP servers configured.";
                ExternalMcpClientStatuses.Clear();
                return;
            }

            ExternalMcpClientStatuses.Clear();
            var connectedCount = 0;
            var unavailableCount = 0;
            var changedCount = 0;
            foreach (var server in configuredServers)
            {
                if (!CopilotMcpClientHealthRegistry.TryGetSnapshot(server, out var health))
                {
                    ExternalMcpClientStatuses.Add(new CopilotExternalMcpClientStatusItem
                    {
                        ServerName = server.Name,
                        Endpoint = server.Endpoint,
                        StateText = "Not checked",
                        DetailText = "Run Refresh Discovery to validate the connection and inspect the exposed tools.",
                        CheckedText = "Not checked",
                    });
                    continue;
                }

                if (health.CacheInvalidated)
                    changedCount++;
                else if (health.State == CopilotMcpClientHealthState.Connected)
                    connectedCount++;
                else
                    unavailableCount++;

                ExternalMcpClientStatuses.Add(CreateExternalMcpClientStatusItem(server, health));
            }

            var notCheckedCount = configuredServers.Length - connectedCount - unavailableCount - changedCount;
            ExternalMcpClientsStatusText = $"{connectedCount}/{configuredServers.Length} connected"
                + (unavailableCount > 0 ? $" · {unavailableCount} unavailable" : string.Empty)
                + (changedCount > 0 ? $" · {changedCount} tool list changed" : string.Empty)
                + (notCheckedCount > 0 ? $" · {notCheckedCount} not checked" : string.Empty);
        }

        private static CopilotExternalMcpClientStatusItem CreateExternalMcpClientStatusItem(
            CopilotMcpClientServerConfig server,
            CopilotMcpClientHealthSnapshot health)
        {
            var stateText = health.CacheInvalidated
                ? "Tool list changed"
                : health.State == CopilotMcpClientHealthState.Connected
                    ? $"Connected · {health.ExposedToolCount}/{health.DiscoveredToolCount} tools"
                    : "Unavailable";
            var detailText = string.IsNullOrWhiteSpace(health.Message)
                ? health.State == CopilotMcpClientHealthState.Connected ? "Connection succeeded." : "Connection unavailable."
                : health.Message;
            return new CopilotExternalMcpClientStatusItem
            {
                ServerName = server.Name,
                Endpoint = server.Endpoint,
                StateText = stateText,
                DetailText = detailText,
                CheckedText = "Checked " + health.CheckedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            };
        }

        private void CopyExternalMcpClientsStatus()
        {
            if (ExternalMcpClientStatuses.Count == 0)
            {
                SetSettingsNotice("No external MCP client status is available to copy.");
                return;
            }

            var builder = new StringBuilder();
            foreach (var status in ExternalMcpClientStatuses)
            {
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.Append(status.ServerName).Append(" · ").AppendLine(status.StateText);
                builder.Append("Endpoint: ").AppendLine(status.Endpoint);
                builder.Append("Status: ").AppendLine(status.DetailText);
                builder.AppendLine(status.CheckedText);
            }

            try
            {
                Clipboard.SetText(builder.ToString().TrimEnd());
                SetSettingsNotice("External MCP client status copied.");
            }
            catch (Exception ex)
            {
                SetSettingsNotice("Copy failed: " + SanitizeError(ex.Message));
            }
        }

        private async Task RefreshExternalMcpClientsAsync()
        {
            if (_disposed || IsRefreshingExternalMcpClients)
                return;
            if (!CopilotMcpClientConfigurationText.TryParse(ExternalMcpServersText, out var servers, out var error))
            {
                IsExternalMcpServersValid = false;
                ExternalMcpServersValidationText = error;
                SetSettingsNotice(error);
                return;
            }
            if (servers.Count == 0)
            {
                ExternalMcpClientsStatusText = "No external MCP servers configured.";
                SetSettingsNotice("Add an external MCP server before refreshing discovery.");
                return;
            }

            IsRefreshingExternalMcpClients = true;
            ExternalMcpClientsStatusText = "Refreshing external MCP discovery...";
            try
            {
                var provider = new CopilotMcpToolProvider();
                await using var lease = await provider.DiscoverAsync(new CopilotAgentRequest
                {
                    ExternalMcpServers = servers.Select(server => server.Clone()).ToArray(),
                    ForceExternalMcpToolRefresh = true,
                }, _lifetimeCancellation.Token);

                RefreshExternalMcpClientsStatus(servers);
                var connectedCount = servers.Count(server =>
                    CopilotMcpClientHealthRegistry.TryGetSnapshot(server, out var health)
                    && health.State == CopilotMcpClientHealthState.Connected);
                SetSettingsNotice($"External MCP discovery refreshed: {connectedCount}/{servers.Count} server(s) connected.");
            }
            catch (OperationCanceledException) when (_disposed)
            {
            }
            catch (Exception ex)
            {
                var message = CopilotMcpAuditLogger.RedactText(ex.Message);
                ExternalMcpClientsStatusText = "External MCP discovery refresh failed.";
                SetSettingsNotice(message);
            }
            finally
            {
                IsRefreshingExternalMcpClients = false;
            }
        }

    }
}
