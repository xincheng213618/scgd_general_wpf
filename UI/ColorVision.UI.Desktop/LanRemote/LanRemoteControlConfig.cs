using ColorVision.Common.MVVM;
using System;

namespace ColorVision.UI.Desktop.LanRemote
{
    public class LanRemoteControlConfig : ViewModelBase, IConfig
    {
        public static LanRemoteControlConfig Instance => ConfigService.Instance.GetRequiredService<LanRemoteControlConfig>();

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
        private bool _isEnabled;

        public int Port
        {
            get => _port;
            set
            {
                int normalized = NormalizePort(value);
                if (_port == normalized) return;
                _port = normalized;
                OnPropertyChanged();
            }
        }
        private int _port = DefaultPort;

        public string PreferredHost
        {
            get => _preferredHost;
            set
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (_preferredHost == normalized) return;
                _preferredHost = normalized;
                OnPropertyChanged();
            }
        }
        private string _preferredHost = string.Empty;

        public string PairingToken
        {
            get => _pairingToken;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? GeneratePairingToken() : value.Trim();
                if (_pairingToken == normalized) return;
                _pairingToken = normalized;
                OnPropertyChanged();
            }
        }
        private string _pairingToken = GeneratePairingToken();

        public const int DefaultPort = 8787;

        public bool EnsureInitialized()
        {
            bool changed = false;

            if (string.IsNullOrWhiteSpace(_pairingToken))
            {
                _pairingToken = GeneratePairingToken();
                OnPropertyChanged(nameof(PairingToken));
                changed = true;
            }

            int normalizedPort = NormalizePort(_port);
            if (_port != normalizedPort)
            {
                _port = normalizedPort;
                OnPropertyChanged(nameof(Port));
                changed = true;
            }

            return changed;
        }

        public void ResetPairingToken()
        {
            PairingToken = GeneratePairingToken();
        }

        public static int NormalizePort(int port)
        {
            return port is >= 1024 and <= 65535 ? port : DefaultPort;
        }

        public static string GeneratePairingToken()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
