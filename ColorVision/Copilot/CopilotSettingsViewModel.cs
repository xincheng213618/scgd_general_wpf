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
    public sealed class CopilotExternalMcpClientStatusItem
    {
        public string ServerName { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;

        public string StateText { get; init; } = string.Empty;

        public string DetailText { get; init; } = string.Empty;

        public string CheckedText { get; init; } = string.Empty;
    }

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

    public sealed record CopilotAgentSkillOverrideOption(
        CopilotAgentSkillOverrideState State,
        string Label);

    public sealed class CopilotAgentSkillSetting : ViewModelBase
    {
        private readonly Action _changed;

        public CopilotAgentSkillSetting(
            string name,
            CopilotAgentSkillOverrideState state,
            CopilotAgentSkillUsageEntry? usage,
            bool isHistoricalExplicitOnly,
            Action changed)
        {
            Name = name;
            _state = state;
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
            UpdateUsage(usage, isHistoricalExplicitOnly);
        }

        public string Name { get; }

        public CopilotAgentSkillOverrideState State
        {
            get => _state;
            set
            {
                var normalized = Enum.IsDefined(value) ? value : CopilotAgentSkillOverrideState.Auto;
                if (SetProperty(ref _state, normalized))
                    _changed();
            }
        }
        private CopilotAgentSkillOverrideState _state;

        public bool IsTracked
        {
            get => _isTracked;
            private set => SetProperty(ref _isTracked, value);
        }
        private bool _isTracked;

        public string UsageSummary
        {
            get => _usageSummary;
            private set => SetProperty(ref _usageSummary, value ?? string.Empty);
        }
        private string _usageSummary = string.Empty;

        public void UpdateUsage(CopilotAgentSkillUsageEntry? usage, bool isHistoricalExplicitOnly)
        {
            IsTracked = usage != null;
            if (usage == null)
            {
                UsageSummary = "尚无本地使用证据；发现该 Skill 时仍会应用此覆盖设置。";
                return;
            }

            UsageSummary = $"已加载 {usage.LoadedRuns}/{usage.SelectedRuns} 次选中运行（{usage.LoadRate:P0}）；连续未加载 {usage.ConsecutiveSelectedWithoutLoad}/{CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold}"
                + (isHistoricalExplicitOnly ? " · 自动策略当前解析为仅显式调用" : string.Empty);
        }
    }

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

        public string ActiveProfileId => _activeProfileId;

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



        private async Task SyncBackendConfigAsync()
        {
            if (!CanSyncBackendConfig)
                return;

            IsSyncingBackendConfig = true;
            BackendSyncStatusText = "Downloading backend Copilot configuration...";
            SetSettingsNotice("Downloading backend Copilot configuration...");
            try
            {
                var response = await _backendSyncClient.FetchAsync(
                    BackendSyncUrl,
                    AllowInsecureBackendSync,
                    _lifetimeCancellation.Token);

                var previousSelectedId = SelectedProfile?.Id ?? string.Empty;
                CopilotBackendMergeResult mergeResult;
                _isApplyingPreset = true;
                _isSavingSettings = true;
                try
                {
                    mergeResult = CopilotBackendSyncClient.MergeProfiles(
                        Profiles,
                        response,
                        BackendSyncUrl);
                    if (Profiles.Count == 0)
                        Profiles.Add(CopilotProfileConfig.CreateDefault());

                    SelectedProfile = Profiles.FirstOrDefault(profile =>
                            string.Equals(profile.Id, mergeResult.DefaultLocalProfileId, StringComparison.Ordinal))
                        ?? Profiles.FirstOrDefault(profile =>
                            string.Equals(profile.Id, previousSelectedId, StringComparison.Ordinal))
                        ?? Profiles.FirstOrDefault(profile => profile.IsConfigured)
                        ?? Profiles.FirstOrDefault();
                }
                finally
                {
                    _isSavingSettings = false;
                    _isApplyingPreset = false;
                }

                RefreshSelectedProfileTestState("This profile was synchronized from the backend.");
                OnSelectedProfileUsageChanged();
                var revision = string.IsNullOrWhiteSpace(response.Revision) ? "unknown" : response.Revision;
                BackendSyncStatusText =
                    $"Revision {revision}: {mergeResult.Added} added, {mergeResult.Updated} updated, {mergeResult.Removed} removed and saved.";
                SaveSynchronizedProfiles();
                SetSettingsNotice(HasUnsavedSettings
                    ? BackendSyncStatusText + " Other settings still have unsaved changes."
                    : BackendSyncStatusText);
            }
            catch (OperationCanceledException) when (_disposed)
            {
            }
            catch (Exception ex)
            {
                BackendSyncStatusText = "Sync failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(BackendSyncStatusText);
            }
            finally
            {
                IsSyncingBackendConfig = false;
            }
        }

        private void SaveSynchronizedProfiles()
        {
            var config = CopilotConfig.Instance;
            config.Profiles.Clear();
            foreach (var profile in Profiles.Select(profile => profile.Clone()))
            {
                profile.EnsureValid();
                config.Profiles.Add(profile);
            }

            config.EnsureInitialized();
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            _activeProfileId = SelectedProfile?.Id ?? _activeProfileId;
            HasAppliedChanges = true;
        }

        private void RunUiOperation(Func<Task> operation, string operationName)
        {
            if (_disposed)
                return;

            CopilotUiTaskObserver.Run(
                operation,
                operationName,
                message => SetSettingsNotice($"{operationName}失败：{message}"));
        }

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
