using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotSettingsViewModel : ViewModelBase
    {
        public CopilotSettingsViewModel()
        {
            ProviderOptions = new ReadOnlyCollection<CopilotProviderOption>(new[]
            {
                new CopilotProviderOption { Label = "OpenAI Compatible", Value = CopilotProviderType.OpenAICompatible },
                new CopilotProviderOption { Label = "Anthropic Compatible", Value = CopilotProviderType.AnthropicCompatible },
            });

            foreach (var profile in CopilotConfig.Instance.Profiles.Select(profile => profile.Clone()))
            {
                Profiles.Add(profile);
            }

            if (Profiles.Count == 0)
                Profiles.Add(CopilotProfileConfig.CreateDefault());

            var state = CopilotChatStateStore.Instance.Load();
            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == state.ActiveProfileId) ?? Profiles.FirstOrDefault();

            AddProfileCommand = new RelayCommand(_ => AddProfile());
            DuplicateProfileCommand = new RelayCommand(_ => DuplicateSelectedProfile());
            DeleteProfileCommand = new RelayCommand(_ => DeleteSelectedProfile());
        }

        public ObservableCollection<CopilotProfileConfig> Profiles { get; } = new();

        public IReadOnlyList<CopilotProviderOption> ProviderOptions { get; }

        public RelayCommand AddProfileCommand { get; }

        public RelayCommand DuplicateProfileCommand { get; }

        public RelayCommand DeleteProfileCommand { get; }

        public CopilotProfileConfig? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                    OnPropertyChanged(nameof(CanEditSelectedProfile));
            }
        }
        private CopilotProfileConfig? _selectedProfile;

        public bool CanEditSelectedProfile => SelectedProfile != null;

        public bool Save()
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

            var stateStore = CopilotChatStateStore.Instance;
            var state = stateStore.Load();
            state.ActiveProfileId = SelectedProfile?.Id ?? state.ActiveProfileId;
            state.EnsureInitialized(config);
            stateStore.Save(state);
            return true;
        }

        private void AddProfile()
        {
            var profile = CopilotProfileConfig.CreateDefault();
            profile.Name = $"模型 {Profiles.Count + 1}";
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
    }
}