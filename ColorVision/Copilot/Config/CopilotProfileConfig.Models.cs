using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotProfileConfig
    {
        // Empty connection IDs keep legacy profiles independent, even when credentials match.
        public string ConnectionId { get; set; } = string.Empty;
        public string ConnectionName { get; set; } = string.Empty;
        [JsonIgnore] public string EffectiveConnectionId => string.IsNullOrWhiteSpace(ConnectionId) ? Id : ConnectionId;

        private bool _connectionEnabled = true;
        public bool ConnectionEnabled
        {
            get => _connectionEnabled;
            set { if (SetProperty(ref _connectionEnabled, value)) OnAvailabilityChanged(); }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (SetProperty(ref _isEnabled, value)) OnAvailabilityChanged(); }
        }

        [JsonIgnore] public bool IsAvailable => IsEnabled && ConnectionEnabled;
        [JsonIgnore] public string ModelIdLabel => string.IsNullOrWhiteSpace(Model) ? "默认模型" : Model;

        // Legacy/manual profiles retain their explicit capability declarations on load.
        public bool UseModelDefaults { get; set; }

        private int _contextWindowTokens;
        public int ContextWindowTokens
        {
            get => _contextWindowTokens;
            set
            {
                if (SetProperty(ref _contextWindowTokens, Math.Max(0, value)))
                    OnPropertyChanged(nameof(ContextWindowLabel));
            }
        }

        [JsonIgnore] public string ContextWindowLabel => ContextWindowTokens switch
        {
            <= 0 => "自动",
            >= 1_000_000 => (ContextWindowTokens / 1_000_000d).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M",
            _ => (ContextWindowTokens / 1000d).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K",
        };

        private void OnAvailabilityChanged()
        {
            OnPropertyChanged(nameof(IsAvailable));
            OnConfigurationStateChanged();
        }
    }
}
