using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public string WebPagePref64PrefixesText
        {
            get => _webPagePref64PrefixesText;
            set
            {
                if (SetProperty(ref _webPagePref64PrefixesText, value ?? string.Empty))
                {
                    ValidateWebPagePref64Prefixes(updateNotice: _isReadyForUserChanges);
                    if (_isReadyForUserChanges)
                        MarkSettingsPending("Public web Pref64 fallback settings changed. Click Apply or Save to use them.");
                }
            }
        }
        private string _webPagePref64PrefixesText = string.Empty;

        public bool IsWebPagePref64PrefixesValid
        {
            get => _isWebPagePref64PrefixesValid;
            private set
            {
                if (SetProperty(ref _isWebPagePref64PrefixesValid, value))
                {
                    OnPropertyChanged(nameof(CanApplySettings));
                    OnPropertyChanged(nameof(CanSaveSettings));
                    OnPropertyChanged(nameof(CanAddAndUseProfile));
                    OnPropertyChanged(nameof(NewProfileUseNowButtonToolTip));
                    OnSelectedProfileUsageChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private bool _isWebPagePref64PrefixesValid = true;

        public string WebPagePref64PrefixesValidationText
        {
            get => _webPagePref64PrefixesValidationText;
            private set => SetProperty(ref _webPagePref64PrefixesValidationText, value ?? string.Empty);
        }
        private string _webPagePref64PrefixesValidationText = "Optional. Leave empty to use RFC 7050 discovery only.";

        private bool ValidateWebPagePref64Prefixes(bool updateNotice)
        {
            if (CopilotWebPagePref64Configuration.TryParse(
                WebPagePref64PrefixesText,
                out var prefixes,
                out var error))
            {
                IsWebPagePref64PrefixesValid = true;
                WebPagePref64PrefixesValidationText = prefixes.Count == 0
                    ? "Optional. Leave empty to use RFC 7050 discovery only."
                    : $"{prefixes.Count} Pref64 fallback prefix(es) configured. Automatic RFC 7050 discovery remains enabled.";
                return true;
            }

            IsWebPagePref64PrefixesValid = false;
            WebPagePref64PrefixesValidationText = error;
            if (updateNotice)
                SetSettingsNotice(error);
            return false;
        }
    }
}
