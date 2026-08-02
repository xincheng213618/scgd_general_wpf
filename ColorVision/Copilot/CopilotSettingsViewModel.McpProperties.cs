#pragma warning disable CA1822
using System;
using System.Globalization;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public bool McpEnabled
        {
            get => _mcpEnabled;
            set
            {
                if (SetProperty(ref _mcpEnabled, value))
                    MarkMcpSettingsPending();
            }
        }
        private bool _mcpEnabled;

        public string McpEndpoint
        {
            get => _mcpEndpoint;
            private set
            {
                if (SetProperty(ref _mcpEndpoint, value ?? string.Empty))
                    OnPropertyChanged(nameof(CodexMcpConfigSnippet));
            }
        }
        private string _mcpEndpoint = string.Empty;

        public int McpPort
        {
            get => _mcpPort;
            set
            {
                if (SetProperty(ref _mcpPort, value))
                {
                    SyncMcpPortTextFromValue(value);
                    McpEndpoint = BuildMcpEndpoint();
                    MarkMcpSettingsPending();
                }
            }
        }
        private int _mcpPort = CopilotConfig.DefaultMcpPort;

        public string McpPortText
        {
            get => _mcpPortText;
            set
            {
                if (SetProperty(ref _mcpPortText, value ?? string.Empty))
                    ApplyMcpPortText(updateNotice: true);
            }
        }
        private string _mcpPortText = CopilotConfig.DefaultMcpPort.ToString(CultureInfo.InvariantCulture);

        private void SyncMcpPortTextFromValue(int port)
        {
            var portText = port.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(_mcpPortText, portText, StringComparison.Ordinal))
                return;

            _mcpPortText = portText;
            OnPropertyChanged(nameof(McpPortText));
        }

        public bool IsMcpPortValid
        {
            get => _isMcpPortValid;
            private set
            {
                if (SetProperty(ref _isMcpPortValid, value))
                {
                    OnPropertyChanged(nameof(CanApplySettings));
                    OnPropertyChanged(nameof(CanSaveSettings));
                    OnPropertyChanged(nameof(CanAddAndUseProfile));
                    OnPropertyChanged(nameof(NewProfileUseNowButtonToolTip));
                    OnPropertyChanged(nameof(CanTestMcpConnection));
                    OnSelectedProfileUsageChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private bool _isMcpPortValid = true;

        public string McpPortValidationText
        {
            get => _mcpPortValidationText;
            private set => SetProperty(ref _mcpPortValidationText, value ?? string.Empty);
        }
        private string _mcpPortValidationText = "Port must be between 1 and 65535.";

        public string McpBearerToken
        {
            get => _mcpBearerToken;
            set
            {
                if (SetProperty(ref _mcpBearerToken, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(McpTokenEnvironmentCommandText));
                    OnPropertyChanged(nameof(McpBearerTokenDisplayText));
                    MarkMcpSettingsPending();
                }
            }
        }
        private string _mcpBearerToken = string.Empty;

        public bool IsMcpBearerTokenVisible
        {
            get => _isMcpBearerTokenVisible;
            set
            {
                if (SetProperty(ref _isMcpBearerTokenVisible, value))
                {
                    OnPropertyChanged(nameof(IsMcpBearerTokenHidden));
                    OnPropertyChanged(nameof(McpBearerTokenVisibilityText));
                    OnPropertyChanged(nameof(McpBearerTokenDisplayText));
                }
            }
        }
        private bool _isMcpBearerTokenVisible;

        public bool IsMcpBearerTokenHidden => !IsMcpBearerTokenVisible;

        public string McpBearerTokenVisibilityText => IsMcpBearerTokenVisible ? "Hide" : "Show";

        public string McpBearerTokenDisplayText
        {
            get
            {
                var token = McpBearerToken.Trim();
                if (string.IsNullOrEmpty(token))
                    return "No token generated.";

                return IsMcpBearerTokenVisible
                    ? token
                    : $"Token hidden ({token.Length} characters).";
            }
        }

        public string ExternalMcpServersText
        {
            get => _externalMcpServersText;
            set
            {
                if (SetProperty(ref _externalMcpServersText, value ?? string.Empty))
                {
                    ValidateExternalMcpServers(updateNotice: _isReadyForUserChanges);
                    if (_isReadyForUserChanges)
                        MarkSettingsPending("External MCP server configuration changed. Click Apply or Save to use it in Copilot.");
                }
            }
        }
        private string _externalMcpServersText = string.Empty;

        public bool IsExternalMcpServersValid
        {
            get => _isExternalMcpServersValid;
            private set
            {
                if (SetProperty(ref _isExternalMcpServersValid, value))
                {
                    OnPropertyChanged(nameof(CanApplySettings));
                    OnPropertyChanged(nameof(CanSaveSettings));
                    OnPropertyChanged(nameof(CanAddAndUseProfile));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private bool _isExternalMcpServersValid = true;

        public string ExternalMcpServersValidationText
        {
            get => _externalMcpServersValidationText;
            private set => SetProperty(ref _externalMcpServersValidationText, value ?? string.Empty);
        }
        private string _externalMcpServersValidationText = "One server per line: name | endpoint | token environment variable | approval/read-only | optional tool=policy,...";

        public string ExternalMcpClientsStatusText
        {
            get => _externalMcpClientsStatusText;
            private set => SetProperty(ref _externalMcpClientsStatusText, value ?? string.Empty);
        }
        private string _externalMcpClientsStatusText = "No external MCP servers configured.";

        public bool IsRefreshingExternalMcpClients
        {
            get => _isRefreshingExternalMcpClients;
            private set
            {
                if (SetProperty(ref _isRefreshingExternalMcpClients, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isRefreshingExternalMcpClients;

        public string CodexMcpConfigSnippet => BuildCodexMcpConfigSnippet();

        public string McpTokenEnvironmentCommandText => BuildMcpTokenEnvironmentCommand();

        public string McpStatusText
        {
            get => _mcpStatusText;
            private set => SetProperty(ref _mcpStatusText, value ?? string.Empty);
        }
        private string _mcpStatusText = string.Empty;

        public string McpConnectionTestText
        {
            get => _mcpConnectionTestText;
            private set => SetProperty(ref _mcpConnectionTestText, value ?? string.Empty);
        }
        private string _mcpConnectionTestText = string.Empty;

        public bool IsTestingMcpConnection
        {
            get => _isTestingMcpConnection;
            private set
            {
                if (SetProperty(ref _isTestingMcpConnection, value))
                    OnPropertyChanged(nameof(CanTestMcpConnection));
            }
        }
        private bool _isTestingMcpConnection;
    }
}
