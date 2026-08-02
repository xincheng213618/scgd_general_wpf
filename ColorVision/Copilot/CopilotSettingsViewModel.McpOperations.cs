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
        private void RegenerateMcpToken()
        {
            var result = MessageBox.Show(
                "Regenerating the MCP bearer token will invalidate any existing Codex configuration that uses the old token. Continue?",
                "Regenerate MCP token",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            McpBearerToken = CopilotConfig.GenerateMcpBearerToken();
            McpConnectionTestText = "Token regenerated. Save settings and update Codex before reconnecting.";
            MarkSettingsPending("MCP token regenerated. Click Apply or Save, then update any external MCP client.");
        }

        private void CopyMcpBearerToken()
        {
            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpConnectionTestText = "Token missing. Regenerate a token before copying.";
                SetSettingsNotice("MCP token is missing. Regenerate it before copying.");
                return;
            }

            try
            {
                Clipboard.SetText(McpBearerToken);
                McpConnectionTestText = "Token copied to clipboard.";
                SetSettingsNotice("MCP bearer token copied. Keep it private.");
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
                SetSettingsNotice("Copy failed: " + SanitizeError(ex.Message));
            }
        }

        private void CopyCodexMcpConfig()
        {
            try
            {
                Clipboard.SetText(CodexMcpConfigSnippet);
                McpConnectionTestText = "Codex MCP config snippet copied.";
                SetSettingsNotice("Codex MCP config copied. Paste it into Codex MCP settings.");
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
                SetSettingsNotice("Copy failed: " + SanitizeError(ex.Message));
            }
        }

        private void CopyMcpTokenEnvironmentCommandToClipboard()
        {
            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpConnectionTestText = "Token missing. Regenerate a token before copying the environment command.";
                SetSettingsNotice("MCP token is missing. Regenerate it before copying the environment command.");
                return;
            }

            try
            {
                Clipboard.SetText(McpTokenEnvironmentCommandText);
                McpConnectionTestText = "PowerShell token command copied.";
                SetSettingsNotice("PowerShell token command copied. Run it in the client environment.");
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
                SetSettingsNotice("Copy failed: " + SanitizeError(ex.Message));
            }
        }

        public async Task TestMcpConnectionAsync()
        {
            if (_disposed || IsTestingMcpConnection)
                return;

            if (!ApplyMcpPortText(updateNotice: true))
                return;

            if (string.IsNullOrWhiteSpace(McpEndpoint) || !Uri.TryCreate(McpEndpoint, UriKind.Absolute, out var endpoint))
            {
                McpConnectionTestText = "Connection failed: endpoint is invalid.";
                SetSettingsNotice("MCP connection test failed: endpoint is invalid.");
                RefreshMcpDiagnostics();
                return;
            }

            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpConnectionTestText = "Connection failed: token missing.";
                SetSettingsNotice("MCP connection test failed: token missing.");
                RefreshMcpDiagnostics();
                return;
            }

            IsTestingMcpConnection = true;
            McpConnectionTestText = "Testing connection...";
            SetSettingsNotice("Testing MCP connection...");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", McpBearerToken.Trim());
                var payload = JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "tools/call",
                    @params = new
                    {
                        name = "get_server_status",
                        arguments = new { },
                    },
                });
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await McpHttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    _lifetimeCancellation.Token);
                if (!response.IsSuccessStatusCode)
                {
                    McpConnectionTestText = $"Connection failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    SetSettingsNotice(SanitizeError(McpConnectionTestText));
                    RefreshMcpStatusText();
                    RefreshMcpDiagnostics();
                    return;
                }

                var body = await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                    response.Content,
                    MaximumMcpStatusResponseBytes,
                    "MCP status response",
                    _lifetimeCancellation.Token);
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var errorElement))
                {
                    McpConnectionTestText = "Connection failed: " + ReadJsonRpcErrorMessage(errorElement);
                    SetSettingsNotice(SanitizeError(McpConnectionTestText));
                    RefreshMcpStatusText();
                    RefreshMcpDiagnostics();
                    return;
                }

                var result = root.GetProperty("result");
                if (result.TryGetProperty("isError", out var isErrorElement) && isErrorElement.GetBoolean())
                {
                    McpConnectionTestText = "Connection failed: get_server_status returned an MCP error.";
                    SetSettingsNotice(SanitizeError(McpConnectionTestText));
                    RefreshMcpStatusText();
                    RefreshMcpDiagnostics();
                    return;
                }

                McpConnectionTestText = "Connected.";
                SetSettingsNotice("MCP connection test succeeded.");
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();
            }
            catch (OperationCanceledException) when (_disposed)
            {
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Connection failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(SanitizeError(McpConnectionTestText));
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();
            }
            finally
            {
                IsTestingMcpConnection = false;
            }
        }

        private string BuildMcpEndpoint()
        {
            return $"http://127.0.0.1:{McpPort}/mcp";
        }

        private bool ApplyMcpPortText(bool updateNotice)
        {
            var text = McpPortText.Trim();
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                || port <= 0
                || port > 65535)
            {
                IsMcpPortValid = false;
                McpPortValidationText = "Use a port from 1 to 65535.";
                if (updateNotice)
                {
                    McpStatusText = "Port is invalid. Use a value from 1 to 65535.";
                    McpConnectionTestText = string.Empty;
                    MarkSettingsPending("Fix the MCP port before applying settings.");
                }

                return false;
            }

            var wasInvalid = !IsMcpPortValid;
            IsMcpPortValid = true;
            McpPortValidationText = $"Endpoint will use http://127.0.0.1:{port}/mcp.";
            if (port == McpPort)
            {
                if (wasInvalid && updateNotice)
                {
                    RefreshMcpStatusText();
                    SetSettingsNotice("MCP port is valid.");
                }

                return true;
            }

            McpPort = port;
            return true;
        }

    }
}
