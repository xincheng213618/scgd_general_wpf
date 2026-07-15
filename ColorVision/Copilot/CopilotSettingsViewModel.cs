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
    public sealed class CopilotConnectProviderOption
    {
        public string GroupName { get; init; } = string.Empty;

        public string IconText { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string BadgeText { get; init; } = string.Empty;

        public string SearchKeywords { get; init; } = string.Empty;

        public CopilotVendorType VendorType { get; init; }

        public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);

        public bool Matches(string? searchText)
        {
            var query = (searchText ?? string.Empty).Trim();
            if (query.Length == 0)
                return true;

            return Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || BadgeText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || SearchKeywords.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class CopilotPluginSubagentRoleSetting : ViewModelBase
    {
        private readonly Action _changed;

        public CopilotPluginSubagentRoleSetting(
            CopilotPluginSubagentRoleInfo role,
            bool isEnabled,
            string permissionSummary,
            string budgetSummary,
            Action changed)
        {
            Key = role.Key;
            DisplayName = role.DisplayName;
            SourceText = $"{role.SourceName} · {role.ToolName}";
            PermissionSummary = permissionSummary;
            BudgetSummary = budgetSummary;
            _isEnabled = isEnabled;
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        }

        public string Key { get; }

        public string DisplayName { get; }

        public string SourceText { get; }

        public string PermissionSummary { get; }

        public string BudgetSummary { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                    _changed();
            }
        }
        private bool _isEnabled;
    }

    public sealed class CopilotSettingsViewModel : ViewModelBase
    {
        private static readonly HttpClient McpHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5),
        };

        private static readonly Regex SensitiveErrorRegex = new(
            "(Bearer\\s+)[^,;\\s]+|(?<name>token|api[_-]?key|authorization)\\s*[:=]\\s*[^,;\\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly CopilotRequestMessage[] ModelConnectionTestMessages =
        {
            new("user", "Reply with OK."),
        };
        private const string ModelConnectionTestSystemPrompt = "You are validating a model connection. Reply with OK.";
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

        private readonly CopilotChatService _chatService = new();
        private bool _isApplyingPreset;
        private bool _isReadyForUserChanges;
        private bool _isSavingSettings;
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
            TestMcpConnectionCommand = new RelayCommand(_ => _ = TestMcpConnectionAsync());
            RefreshMcpDiagnosticsCommand = new RelayCommand(_ => RefreshMcpDiagnostics());
            RefreshExternalMcpClientsCommand = new RelayCommand(_ => _ = RefreshExternalMcpClientsAsync(), _ => !IsRefreshingExternalMcpClients);
            CopyMcpDiagnosticsCommand = new RelayCommand(_ => CopyMcpDiagnostics());
            TestSelectedProfileCommand = new RelayCommand(_ => _ = TestSelectedProfileConnectionAsync(), _ => CanTestSelectedProfile);
            UseSelectedProfileInChatCommand = new RelayCommand(_ => UseSelectedProfileInChat(), _ => CanUseSelectedProfileInChat);
            ToggleNewProfileApiKeyVisibilityCommand = new RelayCommand(_ => IsNewProfileApiKeyVisible = !IsNewProfileApiKeyVisible);
            ToggleSelectedProfileApiKeyVisibilityCommand = new RelayCommand(_ => IsSelectedProfileApiKeyVisible = !IsSelectedProfileApiKeyVisible);
            SelectConnectProviderCommand = new RelayCommand(parameter => SelectConnectProvider(parameter as CopilotConnectProviderOption));
            BackToConnectProviderPickerCommand = new RelayCommand(_ => IsConnectProviderPickerVisible = true);
            ClearConnectProviderSearchCommand = new RelayCommand(_ => ConnectProviderSearchText = string.Empty);

            McpEnabled = config.McpEnabled;
            PreferredShell = config.PreferredShell;
            McpPort = config.McpPort;
            McpPortText = config.McpPort.ToString(CultureInfo.InvariantCulture);
            McpEndpoint = BuildMcpEndpoint();
            McpBearerToken = config.McpBearerToken;
            ExternalMcpServersText = CopilotMcpClientConfigurationText.Format(config.ExternalMcpServers);
            RefreshMcpStatusText();
            RefreshMcpDiagnostics();
            _isReadyForUserChanges = true;
        }

        public ObservableCollection<CopilotProfileConfig> Profiles { get; } = new();

        public ObservableCollection<CopilotPluginSubagentRoleSetting> PluginSubagentRoles { get; } = new();

        public IReadOnlyList<CopilotProviderOption> ProviderOptions { get; }

        public IReadOnlyList<CopilotShellOption> ShellOptions { get; }

        public IReadOnlyList<CopilotVendorOption> VendorOptions { get; }

        public IReadOnlyList<CopilotVendorOption> QuickAddVendorOptions { get; }

        public IReadOnlyList<CopilotConnectProviderOption> ConnectProviderOptions => ConnectProviderOptionCatalog;

        public IReadOnlyList<CopilotConnectProviderOption> VisibleConnectProviderOptions =>
            ConnectProviderOptions.Where(option => option.Matches(ConnectProviderSearchText)).ToArray();

        public CopilotShellKind PreferredShell
        {
            get => _preferredShell;
            set
            {
                if (SetProperty(ref _preferredShell, value) && _isReadyForUserChanges)
                    MarkSettingsPending("Default Agent shell changed. Click Apply or Save to use it.");
            }
        }
        private CopilotShellKind _preferredShell = CopilotShellKind.Auto;

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

        public RelayCommand RefreshExternalMcpClientsCommand { get; }

        public RelayCommand CopyMcpDiagnosticsCommand { get; }

        public RelayCommand TestSelectedProfileCommand { get; }

        public RelayCommand UseSelectedProfileInChatCommand { get; }

        public RelayCommand ToggleNewProfileApiKeyVisibilityCommand { get; }

        public RelayCommand ToggleSelectedProfileApiKeyVisibilityCommand { get; }

        public RelayCommand SelectConnectProviderCommand { get; }

        public RelayCommand BackToConnectProviderPickerCommand { get; }

        public RelayCommand ClearConnectProviderSearchCommand { get; }

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

        public bool CanTestMcpConnection => !IsTestingMcpConnection && IsMcpPortValid;

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
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private bool _isTestingSelectedProfileConnection;

        public bool CanTestSelectedProfile => SelectedProfile?.IsConfigured == true && !IsTestingSelectedProfileConnection;

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

        public string SettingsStatusText
        {
            get => _settingsStatusText;
            private set => SetProperty(ref _settingsStatusText, value ?? string.Empty);
        }
        private string _settingsStatusText = "Ready. Add a model or edit a profile, then Apply or Save.";

        public string McpDiagnosticsSummaryText
        {
            get => _mcpDiagnosticsSummaryText;
            private set => SetProperty(ref _mcpDiagnosticsSummaryText, value ?? string.Empty);
        }
        private string _mcpDiagnosticsSummaryText = string.Empty;

        public string McpServiceSummaryText
        {
            get => _mcpServiceSummaryText;
            private set => SetProperty(ref _mcpServiceSummaryText, value ?? string.Empty);
        }
        private string _mcpServiceSummaryText = string.Empty;

        public string McpActivitySummaryText
        {
            get => _mcpActivitySummaryText;
            private set => SetProperty(ref _mcpActivitySummaryText, value ?? string.Empty);
        }
        private string _mcpActivitySummaryText = string.Empty;

        public string McpPendingSummaryText
        {
            get => _mcpPendingSummaryText;
            private set => SetProperty(ref _mcpPendingSummaryText, value ?? string.Empty);
        }
        private string _mcpPendingSummaryText = string.Empty;

        public string McpErrorSummaryText
        {
            get => _mcpErrorSummaryText;
            private set => SetProperty(ref _mcpErrorSummaryText, value ?? string.Empty);
        }
        private string _mcpErrorSummaryText = string.Empty;

        public string McpDiagnosticsHeaderText
        {
            get => _mcpDiagnosticsHeaderText;
            private set => SetProperty(ref _mcpDiagnosticsHeaderText, value ?? string.Empty);
        }
        private string _mcpDiagnosticsHeaderText = "Diagnostics";

        public string McpLastErrorText
        {
            get => _mcpLastErrorText;
            private set => SetProperty(ref _mcpLastErrorText, value ?? string.Empty);
        }
        private string _mcpLastErrorText = string.Empty;

        public string McpRecentAuditText
        {
            get => _mcpRecentAuditText;
            private set => SetProperty(ref _mcpRecentAuditText, value ?? string.Empty);
        }
        private string _mcpRecentAuditText = string.Empty;

        public string SubagentRolesSummaryText
        {
            get => _subagentRolesSummaryText;
            private set => SetProperty(ref _subagentRolesSummaryText, value ?? string.Empty);
        }
        private string _subagentRolesSummaryText = string.Empty;

        public string SubagentRolesDiagnosticsText
        {
            get => _subagentRolesDiagnosticsText;
            private set => SetProperty(ref _subagentRolesDiagnosticsText, value ?? string.Empty);
        }
        private string _subagentRolesDiagnosticsText = string.Empty;

        public string AgentSkillsSummaryText
        {
            get => _agentSkillsSummaryText;
            private set => SetProperty(ref _agentSkillsSummaryText, value ?? string.Empty);
        }
        private string _agentSkillsSummaryText = string.Empty;

        public string AgentSkillsDiagnosticsText
        {
            get => _agentSkillsDiagnosticsText;
            private set => SetProperty(ref _agentSkillsDiagnosticsText, value ?? string.Empty);
        }
        private string _agentSkillsDiagnosticsText = string.Empty;

        public CopilotVendorType NewProfileVendorType
        {
            get => _newProfileVendorType;
            set
            {
                if (SetProperty(ref _newProfileVendorType, value))
                {
                    OnPropertyChanged(nameof(SelectedConnectProvider));
                    OnPropertyChanged(nameof(ConnectProviderTitle));
                    OnPropertyChanged(nameof(ConnectProviderDescription));
                    OnPropertyChanged(nameof(ConnectProviderApiKeyLabel));
                    OnPropertyChanged(nameof(ConnectProviderIconText));
                    OnPropertyChanged(nameof(NewProfilePresetSummary));
                    OnPropertyChanged(nameof(NewProfilePresetProtocol));
                    OnPropertyChanged(nameof(NewProfilePresetModel));
                    OnPropertyChanged(nameof(NewProfilePresetEndpoint));
                    OnPropertyChanged(nameof(NewProfileAddButtonToolTip));
                    OnPropertyChanged(nameof(NewProfileUseNowButtonToolTip));
                    ClearQuickAddFeedback();
                }
            }
        }
        private CopilotVendorType _newProfileVendorType = CopilotVendorType.DeepSeek;

        public string ConnectProviderSearchText
        {
            get => _connectProviderSearchText;
            set
            {
                if (SetProperty(ref _connectProviderSearchText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(VisibleConnectProviderOptions));
                    OnPropertyChanged(nameof(HasConnectProviderSearchText));
                    OnPropertyChanged(nameof(HasVisibleConnectProviders));
                }
            }
        }
        private string _connectProviderSearchText = string.Empty;

        public bool HasConnectProviderSearchText => !string.IsNullOrWhiteSpace(ConnectProviderSearchText);

        public bool HasVisibleConnectProviders => VisibleConnectProviderOptions.Count > 0;

        public bool IsConnectProviderPickerVisible
        {
            get => _isConnectProviderPickerVisible;
            set
            {
                if (SetProperty(ref _isConnectProviderPickerVisible, value))
                    OnPropertyChanged(nameof(IsConnectProviderFormVisible));
            }
        }
        private bool _isConnectProviderPickerVisible = true;

        public bool IsConnectProviderFormVisible => !IsConnectProviderPickerVisible;

        public CopilotConnectProviderOption SelectedConnectProvider =>
            ConnectProviderOptions.FirstOrDefault(option => option.VendorType == NewProfileVendorType)
            ?? ConnectProviderOptions[0];

        public string ConnectProviderTitle => $"连接 {SelectedConnectProvider.Label}";

        public string ConnectProviderDescription =>
            $"输入你的 {SelectedConnectProvider.Label} API 密钥以连接账户，并在 ColorVision Copilot 中使用 {SelectedConnectProvider.Label} 模型。";

        public string ConnectProviderApiKeyLabel => $"{SelectedConnectProvider.Label} API 密钥";

        public string ConnectProviderIconText => SelectedConnectProvider.IconText;

        public string NewProfileApiKey
        {
            get => _newProfileApiKey;
            set
            {
                if (SetProperty(ref _newProfileApiKey, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(CanAddProfile));
                    OnPropertyChanged(nameof(CanAddAndUseProfile));
                    OnPropertyChanged(nameof(NewProfileCredentialStatusText));
                    if (!string.IsNullOrWhiteSpace(NewProfileApiKey))
                        ClearQuickAddFeedback();

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private string _newProfileApiKey = string.Empty;

        public bool IsNewProfileApiKeyVisible
        {
            get => _isNewProfileApiKeyVisible;
            set
            {
                if (SetProperty(ref _isNewProfileApiKeyVisible, value))
                {
                    OnPropertyChanged(nameof(IsNewProfileApiKeyHidden));
                    OnPropertyChanged(nameof(NewProfileApiKeyVisibilityText));
                    OnPropertyChanged(nameof(NewProfileCredentialStatusText));
                }
            }
        }
        private bool _isNewProfileApiKeyVisible;

        public bool IsNewProfileApiKeyHidden => !IsNewProfileApiKeyVisible;

        public string NewProfileApiKeyVisibilityText => IsNewProfileApiKeyVisible ? "Hide" : "Show";

        public bool CanAddProfile => !string.IsNullOrWhiteSpace(NewProfileApiKey);

        public bool CanAddAndUseProfile => CanAddProfile && CanSaveSettings;

        public string NewProfileAddButtonToolTip =>
            $"Create a {CopilotVendorCatalog.GetLabel(NewProfileVendorType)} profile without changing the active chat model.";

        public string NewProfileUseNowButtonToolTip => CanSaveSettings
            ? $"Create a {CopilotVendorCatalog.GetLabel(NewProfileVendorType)} profile and make it active in chat."
            : "Fix the MCP port before adding and using a model, because this action saves settings.";

        public bool HasAppliedChanges
        {
            get => _hasAppliedChanges;
            private set => SetProperty(ref _hasAppliedChanges, value);
        }
        private bool _hasAppliedChanges;

        public string NewProfileCredentialStatusText
        {
            get
            {
                var key = NewProfileApiKey.Trim();
                return string.IsNullOrWhiteSpace(key)
                    ? "Paste the vendor API key. It stays hidden by default."
                    : IsNewProfileApiKeyVisible
                        ? $"Ready to add. Key visible ({key.Length} characters)."
                        : $"Ready to add. Key hidden ({key.Length} characters).";
            }
        }

        public string NewProfileAddFeedbackText
        {
            get => _newProfileAddFeedbackText;
            private set
            {
                if (SetProperty(ref _newProfileAddFeedbackText, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasNewProfileAddFeedback));
            }
        }
        private string _newProfileAddFeedbackText = string.Empty;

        public bool HasNewProfileAddFeedback => !string.IsNullOrWhiteSpace(NewProfileAddFeedbackText);

        public string NewProfilePresetSummary
        {
            get
            {
                var preset = CopilotVendorCatalog.GetPreset(NewProfileVendorType);
                return $"{preset.Label} profile will be created from the preset below.";
            }
        }

        public string NewProfilePresetProtocol => FormatProviderLabel(CopilotVendorCatalog.GetPreset(NewProfileVendorType).DefaultProviderType);

        public string NewProfilePresetModel
        {
            get
            {
                var models = CopilotVendorCatalog.GetModelPresets(NewProfileVendorType);
                return models.Count > 0 ? models[0] : "Set after adding";
            }
        }

        public string NewProfilePresetEndpoint
        {
            get
            {
                var preset = CopilotVendorCatalog.GetPreset(NewProfileVendorType);
                var baseUrl = CopilotVendorCatalog.GetDefaultBaseUrl(preset.VendorType, preset.DefaultProviderType);
                return string.IsNullOrWhiteSpace(baseUrl) ? "Set after adding" : baseUrl;
            }
        }

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
                    OnPropertyChanged(nameof(CanTestSelectedProfile));
                    OnSelectedProfileUsageChanged();
                    SelectedProfileConnectionTestText = _selectedProfile?.IsConfigured == true
                        ? "Test sends one short request using the selected profile."
                        : "Complete API key, endpoint, and model before testing.";
                    CommandManager.InvalidateRequerySuggested();

                    if (_isReadyForUserChanges && _selectedProfile != null)
                        MarkSettingsPending("Selected profile will become active after Apply or Save.");
                }
            }
        }
        private CopilotProfileConfig? _selectedProfile;

        public bool CanEditSelectedProfile => SelectedProfile != null;

        public bool IsSelectedProfileApiKeyVisible
        {
            get => _isSelectedProfileApiKeyVisible;
            set
            {
                if (SetProperty(ref _isSelectedProfileApiKeyVisible, value))
                {
                    OnPropertyChanged(nameof(IsSelectedProfileApiKeyHidden));
                    OnPropertyChanged(nameof(SelectedProfileApiKeyVisibilityText));
                }
            }
        }
        private bool _isSelectedProfileApiKeyVisible;

        public bool IsSelectedProfileApiKeyHidden => !IsSelectedProfileApiKeyVisible;

        public string SelectedProfileApiKeyVisibilityText => IsSelectedProfileApiKeyVisible ? "Hide" : "Show";

        public IReadOnlyList<string> AvailableModelPresets => SelectedProfile == null
            ? Array.Empty<string>()
            : CopilotVendorCatalog.GetModelPresets(SelectedProfile.VendorType);

        public bool Save()
        {
            if (!ApplyMcpPortText(updateNotice: true))
                return false;
            if (!CopilotMcpClientConfigurationText.TryParse(ExternalMcpServersText, out var externalMcpServers, out var externalMcpError))
            {
                IsExternalMcpServersValid = false;
                ExternalMcpServersValidationText = externalMcpError;
                SetSettingsNotice(externalMcpError);
                return false;
            }

            _isSavingSettings = true;
            try
            {
                var config = CopilotConfig.Instance;
                config.Profiles.Clear();
                foreach (var profile in Profiles.Select(profile => profile.Clone()))
                {
                    profile.EnsureValid();
                    config.Profiles.Add(profile);
                }

                config.McpEnabled = McpEnabled;
                config.PreferredShell = PreferredShell;
                config.McpPort = McpPort;
                config.McpBearerToken = string.IsNullOrWhiteSpace(McpBearerToken)
                    ? CopilotConfig.GenerateMcpBearerToken()
                    : McpBearerToken.Trim();
                config.ExternalMcpServers.Clear();
                foreach (var server in externalMcpServers)
                    config.ExternalMcpServers.Add(server.Clone());

                var visibleRoleKeys = PluginSubagentRoles.Select(role => role.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var disabledRoleKeys = config.DisabledPluginSubagentRoles
                    .Where(roleKey => !visibleRoleKeys.Contains(roleKey))
                    .Concat(PluginSubagentRoles.Where(role => !role.IsEnabled).Select(role => role.Key))
                    .ToArray();
                config.DisabledPluginSubagentRoles.Clear();
                foreach (var roleKey in CopilotPluginSubagentRolePreference.NormalizeKeys(disabledRoleKeys))
                    config.DisabledPluginSubagentRoles.Add(roleKey);

                config.EnsureInitialized();
                McpPort = config.McpPort;
                McpPortText = config.McpPort.ToString(CultureInfo.InvariantCulture);
                McpEndpoint = BuildMcpEndpoint();
                McpBearerToken = config.McpBearerToken;
                ConfigHandler.GetInstance().Save<CopilotConfig>();
                CopilotPluginSubagentRoleLoader.Shared.SetDisabledRoleKeys(config.DisabledPluginSubagentRoles);
                CopilotMcpServer.Instance.ApplyConfig();
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();

                var stateStore = CopilotChatStateStore.Instance;
                var state = stateStore.Load();
                state.ActiveProfileId = SelectedProfile?.Id ?? state.ActiveProfileId;
                state.EnsureInitialized(config);
                _activeProfileId = state.ActiveProfileId;
                stateStore.Save(state);
            }
            finally
            {
                _isSavingSettings = false;
            }

            MarkSettingsSaved();
            return true;
        }

        private void UseSelectedProfileInChat()
        {
            var profile = SelectedProfile;
            if (profile == null)
            {
                SetSettingsNotice("Select a model profile before using it in chat.");
                return;
            }

            if (!profile.IsConfigured)
            {
                SetSettingsNotice("Complete API key, endpoint, and model before using this profile in chat.");
                OnSelectedProfileUsageChanged();
                return;
            }

            var displayLabel = profile.DisplayLabel;
            if (Save())
                SetSettingsNotice($"{displayLabel} is active in chat.");
        }

        public void PrepareAddModelDialog()
        {
            ClearQuickAddFeedback();
            ClearQuickAddCredentialDraft();
            ConnectProviderSearchText = string.Empty;
            IsConnectProviderPickerVisible = true;
        }

        public void ClearQuickAddModelDraft()
        {
            ClearQuickAddFeedback();
            ClearQuickAddCredentialDraft();
        }

        private void SelectConnectProvider(CopilotConnectProviderOption? option)
        {
            if (option == null)
                return;

            if (NewProfileVendorType != option.VendorType)
                ClearQuickAddCredentialDraft();

            NewProfileVendorType = option.VendorType;
            IsConnectProviderPickerVisible = false;
            ClearQuickAddFeedback();
        }

        public bool AddQuickProfile(bool useNow)
        {
            if (useNow && !CanSaveSettings)
            {
                SetSettingsNotice("Fix the MCP port before adding and using a model.");
                return false;
            }

            var profile = AddProfileCore();
            if (profile == null)
                return false;

            if (!useNow)
            {
                NewProfileAddFeedbackText = $"Added {profile.DisplayLabel}. It is saved after Apply or Save.";
                MarkSettingsPending($"Added {profile.DisplayLabel}. Click Apply to use it in chat, or Save to close.");
                return true;
            }

            var displayLabel = profile.DisplayLabel;
            if (!Save())
                return false;

            NewProfileAddFeedbackText = $"Ready: {displayLabel} is active in chat. You can close settings now.";
            SetSettingsNotice($"{displayLabel} is active in chat. You can close settings now.");
            return true;
        }

        private void AddProfile()
        {
            AddQuickProfile(useNow: false);
        }

        private void AddAndUseProfile()
        {
            AddQuickProfile(useNow: true);
        }

        private CopilotProfileConfig? AddProfileCore()
        {
            if (!CanAddProfile)
                return null;

            var profile = CreateProfileForVendor(NewProfileVendorType);
            profile.ApiKey = NewProfileApiKey.Trim();
            Profiles.Add(profile);
            SelectedProfile = profile;
            NewProfileApiKey = string.Empty;
            IsNewProfileApiKeyVisible = false;
            return profile;
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
            MarkSettingsPending($"Duplicated {SelectedProfile.DisplayLabel}. Click Apply or Save to keep it.");
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
            MarkSettingsPending("Profile list changed. Click Apply or Save to keep it.");
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

            RefreshSelectedProfileTestState("Profile details changed. Test uses the current unsaved values.");
            OnSelectedProfileUsageChanged();
            MarkSettingsPending("Profile details changed. Click Apply or Save to use them.");
        }

        private void RefreshSelectedProfileTestState(string? configuredMessage = null)
        {
            OnPropertyChanged(nameof(CanTestSelectedProfile));
            CommandManager.InvalidateRequerySuggested();

            if (IsTestingSelectedProfileConnection)
                return;

            SelectedProfileConnectionTestText = SelectedProfile?.IsConfigured == true
                ? string.IsNullOrWhiteSpace(configuredMessage)
                    ? "Test sends one short request using the selected profile."
                    : configuredMessage
                : "Complete API key, endpoint, and model before testing.";
        }

        private async Task TestSelectedProfileConnectionAsync()
        {
            if (IsTestingSelectedProfileConnection)
                return;

            var sourceProfile = SelectedProfile;
            if (sourceProfile == null)
            {
                SelectedProfileConnectionTestText = "Select a profile before testing.";
                SetSettingsNotice("Select a model profile before testing.");
                return;
            }

            var profile = sourceProfile.Clone();
            profile.EnsureValid();
            if (!profile.IsConfigured)
            {
                SelectedProfileConnectionTestText = "Complete API key, endpoint, and model before testing.";
                SetSettingsNotice("Model test skipped: profile is incomplete.");
                RefreshSelectedProfileTestState();
                return;
            }

            profile.UseSystemPromptOverride(ModelConnectionTestSystemPrompt);
            profile.MaxTokens = 128;
            profile.Temperature = 0;

            IsTestingSelectedProfileConnection = true;
            SelectedProfileConnectionTestText = "Testing model connection...";
            SetSettingsNotice($"Testing {profile.DisplayLabel}...");
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _chatService.StreamReplyAsync(
                    profile,
                    ModelConnectionTestMessages,
                    _ => { },
                    timeout.Token);

                SelectedProfileConnectionTestText = "Connected. The model returned a response.";
                SetSettingsNotice($"Model test succeeded for {profile.DisplayLabel}.");
            }
            catch (OperationCanceledException)
            {
                SelectedProfileConnectionTestText = "Connection failed: request timed out after 30 seconds.";
                SetSettingsNotice(SelectedProfileConnectionTestText);
            }
            catch (Exception ex)
            {
                SelectedProfileConnectionTestText = "Connection failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(SanitizeError(SelectedProfileConnectionTestText));
            }
            finally
            {
                IsTestingSelectedProfileConnection = false;
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
            if (IsTestingMcpConnection)
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

                using var response = await McpHttpClient.SendAsync(request, CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    McpConnectionTestText = $"Connection failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    SetSettingsNotice(SanitizeError(McpConnectionTestText));
                    RefreshMcpStatusText();
                    RefreshMcpDiagnostics();
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
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

        private bool ValidateExternalMcpServers(bool updateNotice)
        {
            if (CopilotMcpClientConfigurationText.TryParse(ExternalMcpServersText, out var servers, out var error))
            {
                IsExternalMcpServersValid = true;
                ExternalMcpServersValidationText = servers.Count == 0
                    ? "Optional. Add an exact tool list in the fifth field to limit what Copilot can discover."
                    : $"{servers.Count} external MCP server(s) configured. Exact tool lists are recommended; tokens are read only from environment variables.";
                return true;
            }

            IsExternalMcpServersValid = false;
            ExternalMcpServersValidationText = error;
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
                return;
            }

            ExternalMcpClientsStatusText = string.Join(" · ", configuredServers.Select(server =>
            {
                if (!CopilotMcpClientHealthRegistry.TryGetSnapshot(server, out var health))
                    return $"{server.Name}: not checked";
                if (health.CacheInvalidated)
                    return $"{server.Name}: tools changed (live refresh required)";

                return health.State == CopilotMcpClientHealthState.Connected
                    ? $"{server.Name}: connected ({health.ExposedToolCount}/{health.DiscoveredToolCount} tools, {(health.UsedCachedDiscovery ? "cached" : "live")}{(health.CapabilitiesChanged ? ", updated" : string.Empty)})"
                    : $"{server.Name}: unavailable";
            }));
        }

        private async Task RefreshExternalMcpClientsAsync()
        {
            if (IsRefreshingExternalMcpClients)
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
                }, CancellationToken.None);

                RefreshExternalMcpClientsStatus(servers);
                var connectedCount = servers.Count(server =>
                    CopilotMcpClientHealthRegistry.TryGetSnapshot(server, out var health)
                    && health.State == CopilotMcpClientHealthState.Connected);
                SetSettingsNotice($"External MCP discovery refreshed: {connectedCount}/{servers.Count} server(s) connected.");
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

        private void RefreshMcpDiagnostics()
        {
            RefreshExternalMcpClientsStatus(CopilotConfig.Instance.ExternalMcpServers);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(20);
            var failureCount = entries.Count(CopilotMcpAuditLogger.IsRealFailureEntry);
            var approvalFlowCount = entries.Count(CopilotMcpAuditLogger.IsApprovalFlowEntry);
            var capabilityCatalog = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var subagentCatalog = CopilotSubagentRoleCatalog.Default;
            var pluginSubagents = CopilotPluginSubagentRoleLoader.Shared.GetSnapshot();
            RefreshSubagentRoleDiagnostics(subagentCatalog, pluginSubagents);
            RefreshAgentSkillDiagnostics();

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

        private void RefreshSubagentRoleDiagnostics(
            CopilotSubagentRoleCatalog catalog,
            CopilotPluginSubagentRoleLoaderSnapshot pluginSnapshot)
        {
            var builtInCount = catalog.Roles.Count(role => string.Equals(role.SourceId, "builtin", StringComparison.OrdinalIgnoreCase));
            var disabledPluginCount = pluginSnapshot.DeclaredRoles.Count(role => !role.IsEnabled);
            var advertisedCharacters = pluginSnapshot.DeclaredRoles.Sum(role => role.AdvertisedCharacters);
            SubagentRolesSummaryText =
                $"{builtInCount} built-in; {pluginSnapshot.LoadedRoleCount}/{pluginSnapshot.DeclaredRoles.Count} plugin roles enabled; {disabledPluginCount} disabled; {advertisedCharacters:N0} advertised characters; registry revision {catalog.Revision}; manifest issues: {pluginSnapshot.Issues.Count}.";

            var lines = new List<string>();
            foreach (var role in catalog.Roles.Where(role => string.Equals(role.SourceId, "builtin", StringComparison.OrdinalIgnoreCase)).OrderBy(role => role.Id, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"[built-in] {role.DisplayName} ({role.ToolName})");
                lines.Add($"  source={role.SourceName} [{role.SourceId}] version={role.SourceVersion}");
                lines.Add($"  domain={role.ContextScope}; tools={FormatSubagentCapabilities(role.ReadCapabilities)}; child={role.ChildMode}; parents={string.Join(",", role.ParentModes)}");
                lines.Add($"  fingerprint={role.CapabilityFingerprint}");
            }
            foreach (var role in pluginSnapshot.DeclaredRoles)
            {
                lines.Add($"[{(role.IsEnabled ? "enabled" : "disabled")}] {role.DisplayName} ({role.ToolName})");
                lines.Add($"  source={role.SourceName} [{role.SourceId}]; role={role.RoleId}; domain={role.ContextScope}; tools={FormatSubagentCapabilities(role.ReadCapabilities)}");
                lines.Add($"  budget={role.MaximumToolCalls} tools/{role.MaximumAgentPasses} passes/{role.MaximumDuration.TotalSeconds:0}s/{role.MaximumAnswerCharacters:N0} answer chars; advertised={role.AdvertisedCharacters:N0} chars");
            }
            foreach (var issue in pluginSnapshot.Issues)
            {
                var roleLabel = string.IsNullOrWhiteSpace(issue.RoleId) ? string.Empty : "/" + issue.RoleId;
                lines.Add($"! {issue.SourceId}{roleLabel}: {issue.Message}");
            }
            SubagentRolesDiagnosticsText = lines.Count == 0 ? "No subagent roles registered." : string.Join(Environment.NewLine, lines);
            SynchronizePluginSubagentRoleSettings(pluginSnapshot.DeclaredRoles);
        }

        private void SynchronizePluginSubagentRoleSettings(IReadOnlyList<CopilotPluginSubagentRoleInfo> roles)
        {
            var pendingValues = PluginSubagentRoles.ToDictionary(role => role.Key, role => role.IsEnabled, StringComparer.OrdinalIgnoreCase);
            PluginSubagentRoles.Clear();
            foreach (var role in roles)
            {
                var isEnabled = pendingValues.TryGetValue(role.Key, out var pendingValue) ? pendingValue : role.IsEnabled;
                PluginSubagentRoles.Add(new CopilotPluginSubagentRoleSetting(
                    role,
                    isEnabled,
                    $"Read access: {role.ContextScope} · {FormatSubagentCapabilities(role.ReadCapabilities)}",
                    $"Limit: {role.MaximumToolCalls} tool calls · {role.MaximumAgentPasses} passes · {role.MaximumDuration.TotalSeconds:0}s · {role.MaximumAnswerCharacters:N0} answer chars · {role.AdvertisedCharacters:N0} prompt chars",
                    OnPluginSubagentRoleSettingChanged));
            }
        }

        private void OnPluginSubagentRoleSettingChanged()
        {
            MarkSettingsPending("Plugin subagent role selection changed. Click Apply or Save to update the model tool list.");
        }

        private void RefreshAgentSkillDiagnostics()
        {
            try
            {
                var snapshot = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
                var usedCount = snapshot.Entries.Count(entry => entry.LoadedRuns > 0);
                AgentSkillsSummaryText = snapshot.RecordedRuns == 0
                    ? "No skill usage has been recorded yet."
                    : $"{snapshot.Entries.Count} tracked across {snapshot.RecordedRuns} run(s); {usedCount} loaded; {snapshot.HistoricalExplicitOnlySkills.Count} low-use explicit-only.";
                AgentSkillsDiagnosticsText = snapshot.Entries.Count == 0
                    ? "Run Copilot with Agent Skills enabled to collect bounded usage evidence."
                    : string.Join(Environment.NewLine, snapshot.Entries.Select(entry =>
                        $"{entry.Name}: loaded {entry.LoadedRuns}/{entry.SelectedRuns} selected run(s) ({entry.LoadRate:P0}); last selected {FormatLocalTime(entry.LastSelectedAtUtc)}"
                        + (entry.SelectedRuns >= CopilotAgentSkillUsageStore.LowUseMinimumSelectedRuns && entry.LoadedRuns == 0 ? " · explicit-only until directly requested" : string.Empty)));
            }
            catch (Exception ex)
            {
                AgentSkillsSummaryText = "Skill usage history is unavailable.";
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
            builder.AppendLine("Agent Skills:");
            builder.AppendLine(AgentSkillsSummaryText);
            builder.AppendLine(AgentSkillsDiagnosticsText);
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
