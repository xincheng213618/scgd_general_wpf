using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotSettingsViewModel : ViewModelBase
    {
        private static readonly HttpClient McpHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5),
        };

        private static readonly Regex SensitiveErrorRegex = new(
            "(Bearer\\s+)[^,;\\s]+|(?<name>token|api[_-]?key|authorization)\\s*[:=]\\s*[^,;\\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private bool _isApplyingPreset;

        public CopilotSettingsViewModel()
        {
            var config = CopilotConfig.Instance;
            if (config.EnsureInitialized())
                ConfigHandler.GetInstance().Save<CopilotConfig>();

            ProviderOptions = new ReadOnlyCollection<CopilotProviderOption>(new[]
            {
                new CopilotProviderOption { Label = "OpenAI Compatible", Value = CopilotProviderType.OpenAICompatible },
                new CopilotProviderOption { Label = "Anthropic Compatible", Value = CopilotProviderType.AnthropicCompatible },
            });
            VendorOptions = CopilotVendorCatalog.VendorOptions;

            foreach (var profile in config.Profiles.Select(profile => profile.Clone()))
            {
                profile.EnsureValid();
                Profiles.Add(profile);
            }

            if (Profiles.Count == 0)
                Profiles.Add(CopilotProfileConfig.CreateDefault());

            var state = CopilotChatStateStore.Instance.Load();
            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == state.ActiveProfileId)
                ?? Profiles.FirstOrDefault(profile => profile.IsConfigured)
                ?? Profiles.FirstOrDefault();

            AddProfileCommand = new RelayCommand(_ => AddProfile());
            DuplicateProfileCommand = new RelayCommand(_ => DuplicateSelectedProfile());
            DeleteProfileCommand = new RelayCommand(_ => DeleteSelectedProfile());
            RegenerateMcpTokenCommand = new RelayCommand(_ => RegenerateMcpToken());
            CopyMcpTokenCommand = new RelayCommand(_ => CopyMcpBearerToken());
            CopyCodexMcpConfigCommand = new RelayCommand(_ => CopyCodexMcpConfig());
            CopyMcpTokenEnvironmentCommand = new RelayCommand(_ => CopyMcpTokenEnvironmentCommandToClipboard());
            TestMcpConnectionCommand = new RelayCommand(_ => _ = TestMcpConnectionAsync());

            McpEnabled = config.McpEnabled;
            McpPort = config.McpPort;
            McpEndpoint = BuildMcpEndpoint();
            McpBearerToken = config.McpBearerToken;
            RefreshMcpStatusText();
        }

        public ObservableCollection<CopilotProfileConfig> Profiles { get; } = new();

        public IReadOnlyList<CopilotProviderOption> ProviderOptions { get; }

        public IReadOnlyList<CopilotVendorOption> VendorOptions { get; }

        public RelayCommand AddProfileCommand { get; }

        public RelayCommand DuplicateProfileCommand { get; }

        public RelayCommand DeleteProfileCommand { get; }

        public RelayCommand RegenerateMcpTokenCommand { get; }

        public RelayCommand CopyMcpTokenCommand { get; }

        public RelayCommand CopyCodexMcpConfigCommand { get; }

        public RelayCommand CopyMcpTokenEnvironmentCommand { get; }

        public RelayCommand TestMcpConnectionCommand { get; }

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
                    McpEndpoint = BuildMcpEndpoint();
                    MarkMcpSettingsPending();
                }
            }
        }
        private int _mcpPort = CopilotConfig.DefaultMcpPort;

        public string McpBearerToken
        {
            get => _mcpBearerToken;
            set
            {
                if (SetProperty(ref _mcpBearerToken, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(McpTokenEnvironmentCommandText));
                    MarkMcpSettingsPending();
                }
            }
        }
        private string _mcpBearerToken = string.Empty;

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

        public bool CanTestMcpConnection => !IsTestingMcpConnection;

        public CopilotVendorType NewProfileVendorType
        {
            get => _newProfileVendorType;
            set => SetProperty(ref _newProfileVendorType, value);
        }
        private CopilotVendorType _newProfileVendorType = CopilotVendorType.DeepSeek;

        public CopilotProfileConfig? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (ReferenceEquals(_selectedProfile, value))
                    return;

                if (_selectedProfile != null)
                    _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;

                if (SetProperty(ref _selectedProfile, value))
                {
                    if (_selectedProfile != null)
                        _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;

                    OnPropertyChanged(nameof(CanEditSelectedProfile));
                    OnPropertyChanged(nameof(AvailableModelPresets));
                }
            }
        }
        private CopilotProfileConfig? _selectedProfile;

        public bool CanEditSelectedProfile => SelectedProfile != null;

        public IReadOnlyList<string> AvailableModelPresets => SelectedProfile == null
            ? Array.Empty<string>()
            : CopilotVendorCatalog.GetModelPresets(SelectedProfile.VendorType);

        public bool Save()
        {
            var config = CopilotConfig.Instance;
            config.Profiles.Clear();
            foreach (var profile in Profiles.Select(profile => profile.Clone()))
            {
                profile.EnsureValid();
                config.Profiles.Add(profile);
            }

            config.McpEnabled = McpEnabled;
            config.McpPort = McpPort;
            config.McpBearerToken = string.IsNullOrWhiteSpace(McpBearerToken)
                ? CopilotConfig.GenerateMcpBearerToken()
                : McpBearerToken.Trim();

            config.EnsureInitialized();
            McpPort = config.McpPort;
            McpEndpoint = BuildMcpEndpoint();
            McpBearerToken = config.McpBearerToken;
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            CopilotMcpServer.Instance.ApplyConfig();
            RefreshMcpStatusText();

            var stateStore = CopilotChatStateStore.Instance;
            var state = stateStore.Load();
            state.ActiveProfileId = SelectedProfile?.Id ?? state.ActiveProfileId;
            state.EnsureInitialized(config);
            stateStore.Save(state);
            return true;
        }

        private void AddProfile()
        {
            var profile = CreateProfileForVendor(NewProfileVendorType);
            Profiles.Add(profile);
            SelectedProfile = profile;
        }

        private void DuplicateSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            var profile = SelectedProfile.Clone();
            profile.Id = Guid.NewGuid().ToString("N");
            profile.Name = $"{SelectedProfile.DisplayLabel} Copy";
            Profiles.Add(profile);
            SelectedProfile = profile;
        }

        private void DeleteSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            var index = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);

            if (Profiles.Count == 0)
                Profiles.Add(CopilotProfileConfig.CreateDefault());

            SelectedProfile = Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
        }

        private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isApplyingPreset || sender is not CopilotProfileConfig profile)
                return;

            if (e.PropertyName == nameof(CopilotProfileConfig.VendorType))
            {
                ApplyVendorPreset(profile, resetName: false);
                OnPropertyChanged(nameof(AvailableModelPresets));
            }
            else if (e.PropertyName == nameof(CopilotProfileConfig.ProviderType))
            {
                ApplyProviderPreset(profile);
            }
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
        }

        private void CopyMcpBearerToken()
        {
            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpConnectionTestText = "Token missing. Regenerate a token before copying.";
                return;
            }

            try
            {
                Clipboard.SetText(McpBearerToken);
                McpConnectionTestText = "Token copied to clipboard.";
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
            }
        }

        private void CopyCodexMcpConfig()
        {
            try
            {
                Clipboard.SetText(CodexMcpConfigSnippet);
                McpConnectionTestText = "Codex MCP config snippet copied.";
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
            }
        }

        private void CopyMcpTokenEnvironmentCommandToClipboard()
        {
            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpConnectionTestText = "Token missing. Regenerate a token before copying the environment command.";
                return;
            }

            try
            {
                Clipboard.SetText(McpTokenEnvironmentCommandText);
                McpConnectionTestText = "PowerShell token command copied.";
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Copy failed: " + SanitizeError(ex.Message);
            }
        }

        public async Task TestMcpConnectionAsync()
        {
            if (IsTestingMcpConnection)
                return;

            if (string.IsNullOrWhiteSpace(McpEndpoint) || !Uri.TryCreate(McpEndpoint, UriKind.Absolute, out var endpoint))
            {
                McpConnectionTestText = "Connection failed: endpoint is invalid.";
                return;
            }

            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpConnectionTestText = "Connection failed: token missing.";
                return;
            }

            IsTestingMcpConnection = true;
            McpConnectionTestText = "Testing connection...";
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

                using var response = await McpHttpClient.SendAsync(request, CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    McpConnectionTestText = $"Connection failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    RefreshMcpStatusText();
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var errorElement))
                {
                    McpConnectionTestText = "Connection failed: " + ReadJsonRpcErrorMessage(errorElement);
                    RefreshMcpStatusText();
                    return;
                }

                var result = root.GetProperty("result");
                if (result.TryGetProperty("isError", out var isErrorElement) && isErrorElement.GetBoolean())
                {
                    McpConnectionTestText = "Connection failed: get_server_status returned an MCP error.";
                    RefreshMcpStatusText();
                    return;
                }

                McpConnectionTestText = "Connected.";
                RefreshMcpStatusText();
            }
            catch (Exception ex)
            {
                McpConnectionTestText = "Connection failed: " + SanitizeError(ex.Message);
                RefreshMcpStatusText();
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

        private string BuildCodexMcpConfigSnippet()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "[mcp_servers.colorvision]",
                $"url = \"{EscapeTomlString(McpEndpoint)}\"",
                "bearer_token_env_var = \"COLORVISION_MCP_TOKEN\"",
            });
        }

        private string BuildMcpTokenEnvironmentCommand()
        {
            return $"[Environment]::SetEnvironmentVariable(\"COLORVISION_MCP_TOKEN\", \"{EscapePowerShellDoubleQuotedString(McpBearerToken)}\", \"User\")";
        }

        private CopilotProfileConfig CreateProfileForVendor(CopilotVendorType vendorType)
        {
            var profile = new CopilotProfileConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorType = vendorType,
                Name = $"{CopilotVendorCatalog.GetLabel(vendorType)} {Profiles.Count + 1}",
                SystemPrompt = CopilotProfileConfig.DefaultSystemPrompt,
                MaxTokens = CopilotProfileConfig.DefaultMaxTokens,
                Temperature = CopilotProfileConfig.DefaultTemperature,
            };

            ApplyVendorPreset(profile, resetName: false);
            return profile;
        }

        private void ApplyVendorPreset(CopilotProfileConfig profile, bool resetName)
        {
            _isApplyingPreset = true;
            try
            {
                var preset = CopilotVendorCatalog.GetPreset(profile.VendorType);

                if (resetName || string.IsNullOrWhiteSpace(profile.Name))
                    profile.Name = $"{preset.Label} {Profiles.Count + 1}";

                if (profile.ProviderType != preset.DefaultProviderType)
                    profile.ProviderType = preset.DefaultProviderType;

                ApplyProviderPreset(profile);

                var modelPresets = CopilotVendorCatalog.GetModelPresets(profile.VendorType);
                if (modelPresets.Count > 0 && (string.IsNullOrWhiteSpace(profile.Model) || !modelPresets.Contains(profile.Model, StringComparer.OrdinalIgnoreCase)))
                    profile.Model = modelPresets[0];
            }
            finally
            {
                _isApplyingPreset = false;
            }
        }

        private void ApplyProviderPreset(CopilotProfileConfig profile)
        {
            _isApplyingPreset = true;
            try
            {
                var defaultBaseUrl = CopilotVendorCatalog.GetDefaultBaseUrl(profile.VendorType, profile.ProviderType);
                if (!string.IsNullOrWhiteSpace(defaultBaseUrl))
                    profile.BaseUrl = defaultBaseUrl;
            }
            finally
            {
                _isApplyingPreset = false;
            }
        }

        private void MarkMcpSettingsPending()
        {
            if (string.IsNullOrEmpty(McpStatusText))
                return;

            McpStatusText = McpEnabled
                ? "Unsaved changes. Save settings to apply the local MCP server configuration."
                : "Unsaved changes. Save settings to disable the local MCP server.";
            McpConnectionTestText = string.Empty;
        }

        private void RefreshMcpStatusText()
        {
            if (!McpEnabled)
            {
                McpStatusText = "Disabled.";
                return;
            }

            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpStatusText = "Token missing. Regenerate a token and save settings.";
                return;
            }

            var server = CopilotMcpServer.Instance;
            if (server.IsRunning)
            {
                McpStatusText = "Running at " + McpEndpoint + ".";
                return;
            }

            var message = server.LastStatusMessage ?? string.Empty;
            if (message.Contains("port", StringComparison.OrdinalIgnoreCase)
                || message.Contains("address", StringComparison.OrdinalIgnoreCase)
                || message.Contains("only one usage", StringComparison.OrdinalIgnoreCase))
            {
                McpStatusText = "Port unavailable. " + SanitizeError(message);
                return;
            }

            McpStatusText = string.IsNullOrWhiteSpace(message)
                ? "Stopped. Save settings to start the local MCP server."
                : SanitizeError(message);
        }

        private static string ReadJsonRpcErrorMessage(JsonElement errorElement)
        {
            if (errorElement.ValueKind == JsonValueKind.Object
                && errorElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return SanitizeError(messageElement.GetString());
            }

            return "JSON-RPC error.";
        }

        private static string SanitizeError(string? message)
        {
            var text = (message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            text = SensitiveErrorRegex.Replace(text, match => match.Value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase)
                ? "Bearer <redacted>"
                : match.Groups["name"].Value + "=<redacted>");
            return text.Length <= 220 ? text : text[..220] + "...";
        }

        private static string EscapeTomlString(string? value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string EscapePowerShellDoubleQuotedString(string? value)
        {
            return (value ?? string.Empty)
                .Replace("`", "``")
                .Replace("$", "`$")
                .Replace("\"", "`\"");
        }
    }
}
