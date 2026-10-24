using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Copilot
{
    // A settings projection over existing profiles; chat/history keep their stable model IDs.
    public sealed class CopilotProviderSettings : ViewModelBase, IDisposable
    {
        private readonly Action _changed;
        private int _connectionUpdateDepth;
        private bool _connectionChangePending;
        public CopilotProviderSettings(IEnumerable<CopilotProfileConfig> models, Action changed)
        {
            Models = models.ToArray();
            _changed = changed;
            foreach (var model in Models) model.PropertyChanged += ModelChanged;
        }

        public IReadOnlyList<CopilotProfileConfig> Models { get; }
        public CopilotProfileConfig Connection => Models[0];
        public string Id => Connection.EffectiveConnectionId;
        public string Name
        {
            get => string.IsNullOrWhiteSpace(Connection.ConnectionName) ? Connection.IsLocalCodex ? "本地 Codex" : Connection.DisplayLabel : Connection.ConnectionName;
            set => UpdateConnection(value?.Trim() ?? string.Empty, model => model.ConnectionName, (model, name) => model.ConnectionName = name);
        }
        public bool IsLocalCodex => Connection.IsLocalCodex;
        public bool IsApiProvider => !IsLocalCodex;
        public bool IsEnabled
        {
            get => Connection.ConnectionEnabled;
            set => UpdateConnection(value, model => model.ConnectionEnabled, (model, enabled) => model.ConnectionEnabled = enabled);
        }
        public string BaseUrl
        {
            get => Connection.BaseUrl;
            set => UpdateConnection(value?.Trim() ?? string.Empty, model => model.BaseUrl, (model, url) => model.BaseUrl = url, refreshCapabilities: true);
        }
        public string ApiKey
        {
            get => Connection.ApiKey;
            set => UpdateConnection(value?.Trim() ?? string.Empty, model => model.ApiKey, (model, key) => model.ApiKey = key);
        }
        public CopilotProviderType ProviderType
        {
            get => Connection.ProviderType;
            set => UpdateConnection(value, model => model.ProviderType, (model, type) => model.ProviderType = type, refreshCapabilities: true);
        }
        public bool AllowInsecureHttp
        {
            get => Connection.AllowInsecureHttp;
            set => UpdateConnection(value, model => model.AllowInsecureHttp, (model, allow) => model.AllowInsecureHttp = allow);
        }
        public bool IsReady => IsEnabled && Models.Any(model => model.IsConfigured);
        public string StatusText => !IsEnabled ? "已停用" : IsLocalCodex ? "本地账户 · 请检测登录状态" : IsReady ? "已配置" : "待配置";

        private void UpdateConnection<T>(T value, Func<CopilotProfileConfig, T> currentValue, Action<CopilotProfileConfig, T> update, bool refreshCapabilities = false)
        {
            _connectionUpdateDepth++;
            try
            {
                foreach (var model in Models)
                {
                    if (EqualityComparer<T>.Default.Equals(currentValue(model), value)) continue;
                    _connectionChangePending = true;
                    update(model, value);
                    if (refreshCapabilities && model.UseModelDefaults)
                        model.SupportsImageInput = CopilotModelDefaults.Find(model)?.SupportsImages ?? false;
                }
            }
            finally
            {
                if (--_connectionUpdateDepth == 0 && _connectionChangePending)
                {
                    _connectionChangePending = false;
                    Changed();
                }
            }
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_connectionUpdateDepth > 0) _connectionChangePending = true;
            else Changed();
        }
        private void Changed()
        {
            OnPropertyChanged(string.Empty);
            _changed();
        }
        public void Dispose()
        {
            foreach (var model in Models) model.PropertyChanged -= ModelChanged;
        }
    }
}
