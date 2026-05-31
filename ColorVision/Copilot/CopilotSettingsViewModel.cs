using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotSettingsViewModel : ViewModelBase
    {
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

            McpEnabled = config.McpEnabled;
            McpPort = config.McpPort;
            McpEndpoint = BuildMcpEndpoint();
            McpBearerToken = config.McpBearerToken;
        }

        public ObservableCollection<CopilotProfileConfig> Profiles { get; } = new();

        public IReadOnlyList<CopilotProviderOption> ProviderOptions { get; }

        public IReadOnlyList<CopilotVendorOption> VendorOptions { get; }

        public RelayCommand AddProfileCommand { get; }

        public RelayCommand DuplicateProfileCommand { get; }

        public RelayCommand DeleteProfileCommand { get; }

        public RelayCommand RegenerateMcpTokenCommand { get; }

        public bool McpEnabled
        {
            get => _mcpEnabled;
            set
            {
                if (SetProperty(ref _mcpEnabled, value))
                    OnPropertyChanged(nameof(McpStatusText));
            }
        }
        private bool _mcpEnabled;

        public string McpEndpoint
        {
            get => _mcpEndpoint;
            private set => SetProperty(ref _mcpEndpoint, value ?? string.Empty);
        }
        private string _mcpEndpoint = string.Empty;

        public int McpPort
        {
            get => _mcpPort;
            set
            {
                if (SetProperty(ref _mcpPort, value))
                    McpEndpoint = BuildMcpEndpoint();
            }
        }
        private int _mcpPort = CopilotConfig.DefaultMcpPort;

        public string McpBearerToken
        {
            get => _mcpBearerToken;
            set => SetProperty(ref _mcpBearerToken, value ?? string.Empty);
        }
        private string _mcpBearerToken = string.Empty;

        public string McpStatusText => McpEnabled
            ? "The local MCP server will listen on 127.0.0.1 after settings are saved."
            : "The local MCP server is disabled.";

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
            profile.Name = $"{SelectedProfile.DisplayLabel} 副本";
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
            McpBearerToken = CopilotConfig.GenerateMcpBearerToken();
        }

        private string BuildMcpEndpoint()
        {
            return $"http://127.0.0.1:{McpPort}/mcp";
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
    }
}