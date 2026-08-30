#pragma warning disable CA1822
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
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
            : "Fix invalid settings before adding and using a model, because this action saves settings.";

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

                InvalidateSelectedProfileConnectionTest();
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

    }
}
