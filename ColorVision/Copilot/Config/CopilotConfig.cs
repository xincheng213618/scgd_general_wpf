using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public class CopilotConfig : ViewModelBase, IConfigSecure
    {
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "CopilotConfig";

        public static CopilotConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CopilotConfig>();

        public ObservableCollection<CopilotProfileConfig> Profiles { get; set; } = new();

        [JsonIgnore]
        public bool IsConfigured => Profiles.Any(profile => profile.IsConfigured);

        [Browsable(false)]
        public bool AutoShowPanelOnFirstLaunch
        {
            get => _autoShowPanelOnFirstLaunch;
            set => SetProperty(ref _autoShowPanelOnFirstLaunch, value);
        }
        private bool _autoShowPanelOnFirstLaunch = true;

        public bool EnsureInitialized()
        {
            var changed = false;

            Profiles ??= new ObservableCollection<CopilotProfileConfig>();

            if (Profiles.Count == 0)
            {
                Profiles.Add(CopilotProfileConfig.CreateDefault());
                changed = true;
            }

            changed |= CopilotTemporaryProfileSource.Sync(Profiles, DateTimeOffset.UtcNow);

            foreach (var profile in Profiles)
            {
                changed |= profile.EnsureValid();
            }

            OnPropertyChanged(nameof(IsConfigured));
            return changed;
        }

        public CopilotProfileConfig? FindProfile(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, System.StringComparison.Ordinal));
        }

        public CopilotProfileConfig? GetPreferredDefaultProfile()
        {
            return Profiles.FirstOrDefault(profile => profile.IsConfigured)
                ?? Profiles.FirstOrDefault();
        }

        public void Encryption()
        {
            foreach (var profile in Profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.ApiKey))
                    profile.ApiKey = Cryptography.AESEncrypt(profile.ApiKey, ConfigAESKey, ConfigAESVector);
            }
        }

        public void Decrypt()
        {
            foreach (var profile in Profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.ApiKey))
                    profile.ApiKey = Cryptography.AESDecrypt(profile.ApiKey, ConfigAESKey, ConfigAESVector);
            }
        }
    }
}