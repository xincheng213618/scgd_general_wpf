using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        private CopilotProfileConfig? _newProfileDraft;
        private bool _isAddingModel;

        public CopilotProfileConfig? NewProfileDraft => _newProfileDraft;

        public IReadOnlyList<string> NewProfileModelPresets => CopilotVendorCatalog.GetModelPresets(NewProfileVendorType);

        public bool IsAddingModel
        {
            get => _isAddingModel;
            set
            {
                if (SetProperty(ref _isAddingModel, value))
                {
                    OnPropertyChanged(nameof(IsEditingModel));
                    OnPropertyChanged(nameof(CanApplySettings));
                    OnPropertyChanged(nameof(CanSaveSettings));
                }
            }
        }

        public bool IsEditingModel => !IsAddingModel;

        private void ResetNewProfileDraft()
        {
            if (_newProfileDraft != null)
                _newProfileDraft.PropertyChanged -= NewProfileDraft_PropertyChanged;

            _newProfileDraft = CreateProfileForVendor(NewProfileVendorType);
            _newProfileDraft.ApiKey = NewProfileApiKey;
            if (NewProfileVendorType == CopilotVendorType.Custom)
            {
                _newProfileDraft.Name = "自定义模型";
                _newProfileDraft.BaseUrl = string.Empty;
                _newProfileDraft.Model = string.Empty;
            }
            _newProfileDraft.PropertyChanged += NewProfileDraft_PropertyChanged;
            OnPropertyChanged(nameof(NewProfileDraft));
            OnPropertyChanged(nameof(NewProfileModelPresets));
            OnPropertyChanged(nameof(CanAddProfile));
            CommandManager.InvalidateRequerySuggested();
        }

        private void NewProfileDraft_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_newProfileDraft == null)
                return;

            if (e.PropertyName == nameof(CopilotProfileConfig.ProviderType))
            {
                var endpoint = CopilotVendorCatalog.GetDefaultBaseUrl(NewProfileVendorType, _newProfileDraft.ProviderType);
                if (!string.IsNullOrWhiteSpace(endpoint))
                    _newProfileDraft.BaseUrl = endpoint;
            }
            OnPropertyChanged(nameof(CanAddProfile));
            OnPropertyChanged(nameof(CanAddAndUseProfile));
            CommandManager.InvalidateRequerySuggested();
        }

        public void CancelAddModel()
        {
            ClearQuickAddModelDraft();
            IsAddingModel = false;
        }
    }
}
