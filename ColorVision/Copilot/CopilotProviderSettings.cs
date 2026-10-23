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
            set { foreach (var model in Models) model.ConnectionName = value.Trim(); Changed(); }
        }
        public bool IsLocalCodex => Connection.IsLocalCodex;
        public bool IsApiProvider => !IsLocalCodex;
        public bool IsEnabled
        {
            get => Connection.ConnectionEnabled;
            set { foreach (var model in Models) model.ConnectionEnabled = value; Changed(); }
        }
        public string BaseUrl
        {
            get => Connection.BaseUrl;
            set => UpdateConnection(model => model.BaseUrl = value, refreshCapabilities: true);
        }
        public string ApiKey
        {
            get => Connection.ApiKey;
            set => UpdateConnection(model => model.ApiKey = value);
        }
        public CopilotProviderType ProviderType
        {
            get => Connection.ProviderType;
            set => UpdateConnection(model => model.ProviderType = value, refreshCapabilities: true);
        }
        public bool AllowInsecureHttp
        {
            get => Connection.AllowInsecureHttp;
            set => UpdateConnection(model => model.AllowInsecureHttp = value);
        }
        public bool IsReady => IsEnabled && Models.Any(model => model.IsConfigured);
        public string StatusText => !IsEnabled ? "已停用" : IsLocalCodex ? "本地账户 · 请检测登录状态" : IsReady ? "已配置" : "待配置";

        private void UpdateConnection(Action<CopilotProfileConfig> update, bool refreshCapabilities = false)
        {
            foreach (var model in Models)
            {
                update(model);
                if (refreshCapabilities && model.UseModelDefaults)
                    model.SupportsImageInput = CopilotModelDefaults.Find(model)?.SupportsImages ?? false;
            }
            Changed();
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs e) => Changed();
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
