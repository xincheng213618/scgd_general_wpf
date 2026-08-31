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
        private const string McpConnectionTestingNotice = "Testing MCP connection...";
        private readonly HttpClient _mcpConnectionHttpClient;
        private CancellationTokenSource? _mcpConnectionTestCancellation;
        private long _mcpConnectionSettingsRevision;

        private void InvalidateMcpConnectionTest()
        {
            _mcpConnectionSettingsRevision++;
            if (_mcpConnectionTestCancellation == null)
                return;

            McpConnectionTestText = string.Empty;
            if (string.Equals(SettingsStatusText, McpConnectionTestingNotice, StringComparison.Ordinal))
                SetSettingsNotice("MCP settings changed. Run a new connection test for the current values.");
            _mcpConnectionTestCancellation.Cancel();
        }

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

            var revision = _mcpConnectionSettingsRevision;
            var bearerToken = McpBearerToken;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            _mcpConnectionTestCancellation = cancellation;
            bool CanPublishResult() => !_disposed
                && !cancellation.IsCancellationRequested
                && revision == _mcpConnectionSettingsRevision;

            IsTestingMcpConnection = true;
            McpConnectionTestText = "Testing connection...";
            SetSettingsNotice(McpConnectionTestingNotice);
            try
            {
                await CopilotMcpConnectionDiagnostic.TestAsync(_mcpConnectionHttpClient, endpoint, bearerToken, cancellation.Token);

                if (!CanPublishResult())
                    return;
                McpConnectionTestText = "Connected.";
                if (string.Equals(SettingsStatusText, McpConnectionTestingNotice, StringComparison.Ordinal))
                    SetSettingsNotice("MCP connection test succeeded.");
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (!CanPublishResult())
                    return;
                McpConnectionTestText = "Connection failed: " + SanitizeError(ex.Message);
                if (string.Equals(SettingsStatusText, McpConnectionTestingNotice, StringComparison.Ordinal))
                    SetSettingsNotice(SanitizeError(McpConnectionTestText));
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();
            }
            finally
            {
                _mcpConnectionTestCancellation = null;
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
