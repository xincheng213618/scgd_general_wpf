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

    public sealed partial class CopilotSettingsViewModel : ViewModelBase, IDisposable
    {
        private const int MaximumMcpStatusResponseBytes = 512 * 1024;
        private static readonly HttpClient McpHttpClient = CopilotMcpHttpTransport.CreateClient(TimeSpan.FromSeconds(5));

        private static readonly Regex SensitiveErrorRegex = new(
            "(Bearer\\s+)[^,;\\s]+|(?<name>token|api[_-]?key|authorization)\\s*[:=]\\s*[^,;\\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly IReadOnlyList<CopilotConnectProviderOption> ConnectProviderOptionCatalog =
            new ReadOnlyCollection<CopilotConnectProviderOption>(new[]
            {
                new CopilotConnectProviderOption
                {
                    GroupName = "热门",
                    IconText = "D",
                    Label = "DeepSeek",
                    Description = "低成本推理与代码模型",
                    BadgeText = "推荐",
                    SearchKeywords = "deep deepseek",
                    VendorType = CopilotVendorType.DeepSeek,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "热门",
                    IconText = "AI",
                    Label = "OpenAI",
                    Description = "使用 OpenAI API 密钥连接",
                    SearchKeywords = "openai chatgpt gpt",
                    VendorType = CopilotVendorType.OpenAI,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "热门",
                    IconText = "A",
                    Label = "Anthropic",
                    Description = "使用 Claude API 密钥连接",
                    SearchKeywords = "anthropic claude",
                    VendorType = CopilotVendorType.Claude,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "热门",
                    IconText = "G",
                    Label = "Google",
                    Description = "使用 Gemini API 密钥连接",
                    SearchKeywords = "google gemini",
                    VendorType = CopilotVendorType.Gemini,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "其他",
                    IconText = "M",
                    Label = "MiniMax",
                    Description = "使用 MiniMax API 密钥连接",
                    SearchKeywords = "minimax",
                    VendorType = CopilotVendorType.MiniMax,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "其他",
                    IconText = "S",
                    Label = "SenseNova",
                    Description = "使用商汤 SenseNova API 密钥连接",
                    SearchKeywords = "sensenova sensetime",
                    VendorType = CopilotVendorType.SenseNova,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "其他",
                    IconText = "Z",
                    Label = "Zhipu GLM",
                    Description = "使用智谱 GLM API 密钥连接",
                    SearchKeywords = "zhipu glm bigmodel",
                    VendorType = CopilotVendorType.GLM,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "其他",
                    IconText = "X",
                    Label = "Grok / xAI",
                    Description = "使用 xAI API 密钥连接",
                    SearchKeywords = "grok xai x.ai",
                    VendorType = CopilotVendorType.Grok,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "其他",
                    IconText = "MI",
                    Label = "Xiaomi Mimo",
                    Description = "使用小米 Mimo API 密钥连接",
                    SearchKeywords = "xiaomi mimo",
                    VendorType = CopilotVendorType.Xiaomi,
                },
                new CopilotConnectProviderOption
                {
                    GroupName = "其他",
                    IconText = "+",
                    Label = "自定义",
                    Description = "手动配置兼容接口",
                    SearchKeywords = "custom 自定义",
                    VendorType = CopilotVendorType.Custom,
                },
            });

        private readonly CopilotModelConnectionDiagnostic _modelConnectionDiagnostic = new();
        private readonly CopilotBackendSyncClient _backendSyncClient = new();
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private CancellationTokenSource? _modelConnectionTestCancellation;
        private bool _isApplyingPreset;
        private bool _isReadyForUserChanges;
        private bool _isSavingSettings;
        private bool _disposed;
        private string _activeProfileId = string.Empty;

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
            ShellOptions = new ReadOnlyCollection<CopilotShellOption>(new[]
            {
                new CopilotShellOption { Label = "自动（PowerShell）", Value = CopilotShellKind.Auto },
                new CopilotShellOption { Label = "PowerShell", Value = CopilotShellKind.PowerShell },
                new CopilotShellOption { Label = "CMD", Value = CopilotShellKind.CommandPrompt },
            });
            AgentSkillOverrideOptions = new ReadOnlyCollection<CopilotAgentSkillOverrideOption>(new[]
            {
                new CopilotAgentSkillOverrideOption(CopilotAgentSkillOverrideState.Auto, "Auto"),
                new CopilotAgentSkillOverrideOption(CopilotAgentSkillOverrideState.NameOnly, "Name only"),
                new CopilotAgentSkillOverrideOption(CopilotAgentSkillOverrideState.UserInvocableOnly, "Explicit only"),
                new CopilotAgentSkillOverrideOption(CopilotAgentSkillOverrideState.Off, "Off"),
                new CopilotAgentSkillOverrideOption(CopilotAgentSkillOverrideState.On, "On"),
            });
            VendorOptions = CopilotVendorCatalog.VendorOptions;
            QuickAddVendorOptions = VendorOptions
                .Where(option => option.Value != CopilotVendorType.Custom)
                .ToArray();

            foreach (var profile in config.Profiles.Select(profile => profile.Clone()))
            {
                profile.EnsureValid();
                Profiles.Add(profile);
            }

            if (Profiles.Count == 0)
                Profiles.Add(CopilotProfileConfig.CreateDefault());

            var state = CopilotChatStateStore.Instance.Load();
            _activeProfileId = Profiles.Any(profile => string.Equals(profile.Id, state.ActiveProfileId, StringComparison.Ordinal))
                ? state.ActiveProfileId
                : string.Empty;
            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == state.ActiveProfileId)
                ?? Profiles.FirstOrDefault(profile => profile.IsConfigured)
                ?? Profiles.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(_activeProfileId))
                _activeProfileId = SelectedProfile?.Id ?? string.Empty;

            AddProfileCommand = new RelayCommand(_ => AddProfile(), _ => CanAddProfile);
            AddAndUseProfileCommand = new RelayCommand(_ => AddAndUseProfile(), _ => CanAddAndUseProfile);
            DuplicateProfileCommand = new RelayCommand(_ => DuplicateSelectedProfile());
            DeleteProfileCommand = new RelayCommand(_ => DeleteSelectedProfile());
            RegenerateMcpTokenCommand = new RelayCommand(_ => RegenerateMcpToken());
            CopyMcpTokenCommand = new RelayCommand(_ => CopyMcpBearerToken());
            ToggleMcpBearerTokenVisibilityCommand = new RelayCommand(_ => IsMcpBearerTokenVisible = !IsMcpBearerTokenVisible);
            CopyCodexMcpConfigCommand = new RelayCommand(_ => CopyCodexMcpConfig());
            CopyMcpTokenEnvironmentCommand = new RelayCommand(_ => CopyMcpTokenEnvironmentCommandToClipboard());
            TestMcpConnectionCommand = new RelayCommand(_ => RunUiOperation(TestMcpConnectionAsync, "测试 MCP 连接"));
            RefreshMcpDiagnosticsCommand = new RelayCommand(_ => RefreshMcpDiagnostics());
            RefreshAgentSkillDiagnosticsCommand = new RelayCommand(_ => RefreshAgentSkillDiagnostics());
            RefreshExternalMcpClientsCommand = new RelayCommand(_ => RunUiOperation(RefreshExternalMcpClientsAsync, "刷新外部 MCP"), _ => !IsRefreshingExternalMcpClients);
            CopyExternalMcpClientsStatusCommand = new RelayCommand(_ => CopyExternalMcpClientsStatus());
            CopyMcpDiagnosticsCommand = new RelayCommand(_ => CopyMcpDiagnostics());
            TestSelectedProfileCommand = new RelayCommand(_ => ToggleSelectedProfileConnectionTest(), _ => CanTestSelectedProfile);
            UseSelectedProfileInChatCommand = new RelayCommand(_ => UseSelectedProfileInChat(), _ => CanUseSelectedProfileInChat);
            ToggleNewProfileApiKeyVisibilityCommand = new RelayCommand(_ => IsNewProfileApiKeyVisible = !IsNewProfileApiKeyVisible);
            ToggleSelectedProfileApiKeyVisibilityCommand = new RelayCommand(_ => IsSelectedProfileApiKeyVisible = !IsSelectedProfileApiKeyVisible);
            SyncBackendConfigCommand = new RelayCommand(
                _ => RunUiOperation(SyncBackendConfigAsync, "同步后台 Copilot 配置"),
                _ => CanSyncBackendConfig);
            SelectConnectProviderCommand = new RelayCommand(parameter => SelectConnectProvider(parameter as CopilotConnectProviderOption));
            BackToConnectProviderPickerCommand = new RelayCommand(_ => IsConnectProviderPickerVisible = true);
            ClearConnectProviderSearchCommand = new RelayCommand(_ => ConnectProviderSearchText = string.Empty);
            AddAgentSkillOverrideCommand = new RelayCommand(_ => AddAgentSkillOverride(), _ => CanAddAgentSkillOverride);
            RemoveAgentSkillOverrideCommand = new RelayCommand<CopilotAgentSkillSetting>(RemoveAgentSkillOverride, setting => setting != null);

            McpEnabled = config.McpEnabled;
            AgentContextWindowTokens = config.AgentDefaults.ContextWindowTokens;
            AutoCompactConversationHistory = config.AgentDefaults.AutoCompactConversationHistory;
            AutoCompactThresholdPercent = config.AgentDefaults.AutoCompactThresholdPercent;
            AutoCompactInstructions = config.AgentDefaults.AutoCompactInstructions;
            AgentRequestTokenBudget = config.AgentDefaults.RequestTokenBudget;
            MaxAgentToolCalls = config.AgentDefaults.MaxToolCalls;
            MaxAgentPasses = config.AgentDefaults.MaxAgentPasses;
            AgentTimeoutSeconds = config.AgentDefaults.TimeoutSeconds;
            PreferredShell = config.AgentDefaults.PreferredShell;
            LoadAgentSkillSettings(config.AgentDefaults.SkillOverrides);
            McpPort = config.McpPort;
            McpPortText = config.McpPort.ToString(CultureInfo.InvariantCulture);
            McpEndpoint = BuildMcpEndpoint();
            McpBearerToken = config.McpBearerToken;
            ExternalMcpServersText = CopilotMcpClientConfigurationText.Format(config.ExternalMcpServers);
            BackendSyncUrl = config.BackendSyncUrl;
            AllowInsecureBackendSync = config.AllowInsecureBackendSync;
            RefreshMcpStatusText();
            RefreshMcpDiagnostics();
            RefreshAgentSkillDiagnostics();
            _isReadyForUserChanges = true;
        }

        public ObservableCollection<CopilotProfileConfig> Profiles { get; } = new();

        public ObservableCollection<CopilotAgentSkillSetting> AgentSkillSettings { get; } = new();

        public ObservableCollection<CopilotExternalMcpClientStatusItem> ExternalMcpClientStatuses { get; } = new();

        public IReadOnlyList<CopilotProviderOption> ProviderOptions { get; }

        public IReadOnlyList<CopilotShellOption> ShellOptions { get; }

        public IReadOnlyList<CopilotAgentSkillOverrideOption> AgentSkillOverrideOptions { get; }

        public IReadOnlyList<CopilotVendorOption> VendorOptions { get; }

        public IReadOnlyList<CopilotVendorOption> QuickAddVendorOptions { get; }

        public IReadOnlyList<CopilotConnectProviderOption> ConnectProviderOptions => ConnectProviderOptionCatalog;

        public IReadOnlyList<CopilotConnectProviderOption> VisibleConnectProviderOptions =>
            ConnectProviderOptions.Where(option => option.Matches(ConnectProviderSearchText)).ToArray();

        public string BackendSyncUrl
        {
            get => _backendSyncUrl;
            set
            {
                if (SetProperty(ref _backendSyncUrl, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(CanSyncBackendConfig));
                    CommandManager.InvalidateRequerySuggested();
                    MarkSettingsPending("Backend sync settings changed. Click Apply or Save to keep them.");
                }
            }
        }
        private string _backendSyncUrl = CopilotConfig.DefaultBackendSyncUrl;

        public bool AllowInsecureBackendSync
        {
            get => _allowInsecureBackendSync;
            set
            {
                if (SetProperty(ref _allowInsecureBackendSync, value))
                    MarkSettingsPending("Backend sync transport policy changed. Click Apply or Save to keep it.");
            }
        }
        private bool _allowInsecureBackendSync;

        public bool IsSyncingBackendConfig
        {
            get => _isSyncingBackendConfig;
            private set
            {
                if (SetProperty(ref _isSyncingBackendConfig, value))
                {
                    OnPropertyChanged(nameof(CanSyncBackendConfig));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private bool _isSyncingBackendConfig;

        public bool CanSyncBackendConfig => !_disposed
            && !IsSyncingBackendConfig
            && !string.IsNullOrWhiteSpace(BackendSyncUrl);

        public string BackendSyncStatusText
        {
            get => _backendSyncStatusText;
            private set => SetProperty(ref _backendSyncStatusText, value ?? string.Empty);
        }
        private string _backendSyncStatusText = "Click Download and sync to verify this device and update managed model profiles.";

        public RelayCommand AddProfileCommand { get; }

        public RelayCommand AddAndUseProfileCommand { get; }

        public RelayCommand DuplicateProfileCommand { get; }

        public RelayCommand DeleteProfileCommand { get; }

        public RelayCommand RegenerateMcpTokenCommand { get; }

        public RelayCommand CopyMcpTokenCommand { get; }

        public RelayCommand ToggleMcpBearerTokenVisibilityCommand { get; }

        public RelayCommand CopyCodexMcpConfigCommand { get; }

        public RelayCommand CopyMcpTokenEnvironmentCommand { get; }

        public RelayCommand TestMcpConnectionCommand { get; }

        public RelayCommand RefreshMcpDiagnosticsCommand { get; }

        public RelayCommand RefreshAgentSkillDiagnosticsCommand { get; }

        public RelayCommand RefreshExternalMcpClientsCommand { get; }

        public RelayCommand CopyExternalMcpClientsStatusCommand { get; }

        public RelayCommand CopyMcpDiagnosticsCommand { get; }

        public RelayCommand TestSelectedProfileCommand { get; }

        public RelayCommand UseSelectedProfileInChatCommand { get; }

        public RelayCommand ToggleNewProfileApiKeyVisibilityCommand { get; }

        public RelayCommand ToggleSelectedProfileApiKeyVisibilityCommand { get; }

        public RelayCommand SyncBackendConfigCommand { get; }

        public RelayCommand SelectConnectProviderCommand { get; }

        public RelayCommand BackToConnectProviderPickerCommand { get; }

        public RelayCommand ClearConnectProviderSearchCommand { get; }

        public RelayCommand AddAgentSkillOverrideCommand { get; }

        public ICommand RemoveAgentSkillOverrideCommand { get; }


        public bool CanTestMcpConnection => !_disposed && !IsTestingMcpConnection && IsMcpPortValid;

        public string SelectedProfileConnectionTestText
        {
            get => _selectedProfileConnectionTestText;
            private set => SetProperty(ref _selectedProfileConnectionTestText, value ?? string.Empty);
        }
        private string _selectedProfileConnectionTestText = "Test sends one short request using the selected profile.";

        public bool IsTestingSelectedProfileConnection
        {
            get => _isTestingSelectedProfileConnection;
            private set
            {
                if (SetProperty(ref _isTestingSelectedProfileConnection, value))
                {
                    OnPropertyChanged(nameof(CanTestSelectedProfile));
                    OnPropertyChanged(nameof(SelectedProfileConnectionTestActionText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private bool _isTestingSelectedProfileConnection;

        public bool CanTestSelectedProfile => !_disposed
            && (IsTestingSelectedProfileConnection || SelectedProfile?.IsConfigured == true);

        public string SelectedProfileConnectionTestActionText =>
            IsTestingSelectedProfileConnection ? "Cancel Test" : "Test Model";

        public bool IsSelectedProfileActiveInChat => SelectedProfile != null
            && string.Equals(SelectedProfile.Id, _activeProfileId, StringComparison.Ordinal);

        public bool CanUseSelectedProfileInChat => SelectedProfile?.IsConfigured == true
            && CanSaveSettings
            && (!IsSelectedProfileActiveInChat || HasUnsavedSettings);

        public string SelectedProfileUsageActionText
        {
            get
            {
                if (SelectedProfile == null)
                    return "Select Profile";

                if (!SelectedProfile.IsConfigured)
                    return "Complete Profile";

                return IsSelectedProfileActiveInChat
                    ? HasUnsavedSettings ? "Apply to Chat" : "In Chat"
                    : "Use in Chat";
            }
        }

        public string SelectedProfileUsageText
        {
            get
            {
                var profile = SelectedProfile;
                if (profile == null)
                    return "Select a profile to review its chat usage.";

                if (!profile.IsConfigured)
                    return "Complete API key, endpoint, and model before using this profile in chat.";

                if (IsSelectedProfileActiveInChat)
                {
                    return HasUnsavedSettings
                        ? "This is the current chat profile. Unsaved edits will apply after Apply, Save, or Apply to Chat."
                        : "This is the current chat profile.";
                }

                return "This profile is not used by chat yet. Use it now, or Apply/Save to make the selected profile active.";
            }
        }

        public bool HasUnsavedSettings
        {
            get => _hasUnsavedSettings;
            private set
            {
                if (SetProperty(ref _hasUnsavedSettings, value))
                {
                    OnPropertyChanged(nameof(CanApplySettings));
                    OnPropertyChanged(nameof(SettingsCancelButtonText));
                    OnSelectedProfileUsageChanged();
                }
            }
        }
        private bool _hasUnsavedSettings;

        public bool CanApplySettings => HasUnsavedSettings && IsMcpPortValid && IsExternalMcpServersValid;

        public bool CanSaveSettings => IsMcpPortValid && IsExternalMcpServersValid;

        public string SettingsCancelButtonText => HasUnsavedSettings ? "Cancel" : "Close";




        private void RunUiOperation(Func<Task> operation, string operationName)
        {
            if (_disposed)
                return;

            CopilotUiTaskObserver.Run(
                operation,
                operationName,
                message => SetSettingsNotice($"{operationName}失败：{message}"));
        }

       public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                _lifetimeCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            _lifetimeCancellation.Dispose();
            OnPropertyChanged(nameof(CanTestMcpConnection));
            OnPropertyChanged(nameof(CanTestSelectedProfile));
            OnPropertyChanged(nameof(CanSyncBackendConfig));
            CommandManager.InvalidateRequerySuggested();
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


        private static string FormatProviderLabel(CopilotProviderType providerType)
        {
            return providerType == CopilotProviderType.AnthropicCompatible
                ? "Anthropic Compatible"
                : "OpenAI Compatible";
        }

        private void ClearQuickAddFeedback()
        {
            if (!string.IsNullOrWhiteSpace(NewProfileAddFeedbackText))
                NewProfileAddFeedbackText = string.Empty;
        }

        private void ClearQuickAddCredentialDraft()
        {
            NewProfileApiKey = string.Empty;
            IsNewProfileApiKeyVisible = false;
        }

        private void MarkSettingsPending(string message)
        {
            if (!_isReadyForUserChanges || _isSavingSettings)
                return;

            HasUnsavedSettings = true;
            SettingsStatusText = string.IsNullOrWhiteSpace(message)
                ? "Unsaved changes. Click Apply or Save to use them."
                : message;
        }

        private void MarkSettingsSaved()
        {
            HasUnsavedSettings = false;
            HasAppliedChanges = true;
            SettingsStatusText = $"Settings saved at {DateTime.Now:HH:mm:ss}. The chat panel will use the selected profile list.";
        }

        private void SetSettingsNotice(string message)
        {
            if (!_isReadyForUserChanges)
                return;

            SettingsStatusText = string.IsNullOrWhiteSpace(message)
                ? "Ready."
                : message;
        }

        private void OnSelectedProfileUsageChanged()
        {
            OnPropertyChanged(nameof(IsSelectedProfileActiveInChat));
            OnPropertyChanged(nameof(CanUseSelectedProfileInChat));
            OnPropertyChanged(nameof(SelectedProfileUsageActionText));
            OnPropertyChanged(nameof(SelectedProfileUsageText));
            CommandManager.InvalidateRequerySuggested();
        }

        private CopilotProfileConfig CreateProfileForVendor(CopilotVendorType vendorType)
        {
            var profile = new CopilotProfileConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorType = vendorType,
                Name = $"{CopilotVendorCatalog.GetLabel(vendorType)} {Profiles.Count + 1}",
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
            RefreshMcpDiagnostics();
            MarkSettingsPending("MCP settings changed. Click Apply or Save to update the local server.");
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
