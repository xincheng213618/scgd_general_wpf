using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public ObservableCollection<CopilotProviderSettings> Providers { get; } = new();
        private CopilotProviderSettings? _selectedProvider;
        public CopilotProviderSettings? SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (!SetProperty(ref _selectedProvider, value)) return;
                IsSelectedProfileApiKeyVisible = false;
                if (value != null && !value.Models.Contains(SelectedProfile)) SelectedProfile = value.Connection;
                OnPropertyChanged(nameof(HasSelectedProvider));
            }
        }
        public bool HasSelectedProvider => SelectedProvider != null;

        private void RebuildProviders()
        {
            var id = SelectedProfile?.EffectiveConnectionId ?? SelectedProvider?.Id;
            foreach (var provider in Providers) provider.Dispose();
            Providers.Clear();
            foreach (var group in Profiles.GroupBy(profile => profile.EffectiveConnectionId))
                Providers.Add(new CopilotProviderSettings(group, () => MarkSettingsPending("模型设置已修改，点击应用或保存后生效。")));
            SelectedProvider = Providers.FirstOrDefault(provider => provider.Id == id) ?? Providers.FirstOrDefault();
        }

        public void AddProvider(CopilotConnectProviderOption option)
        {
            if (option.IsLocalCodex)
            {
                var existing = Profiles.FirstOrDefault(profile => profile.IsLocalCodex);
                if (existing != null)
                {
                    SelectedProfile = existing;
                    RebuildProviders();
                    IsAddingModel = false;
                    return;
                }
            }
            var connectionId = Guid.NewGuid().ToString("N");
            var presetModels = option.IsLocalCodex ? LocalCodexModels : CopilotVendorCatalog.GetModelPresets(option.VendorType);
            var models = presetModels.Count == 0 ? new[] { string.Empty } : presetModels.ToArray();
            CopilotProfileConfig? first = null;
            foreach (var model in models)
            {
                var profile = option.IsLocalCodex
                    ? new CopilotProfileConfig { VendorType = CopilotVendorType.OpenAI, ProviderType = CopilotProviderType.LocalCodex, BaseUrl = string.Empty, ApiKey = string.Empty }
                    : CreateProfileForVendor(option.VendorType);
                profile.ConnectionId = connectionId;
                profile.ConnectionName = option.Label;
                if (option.VendorType == CopilotVendorType.Custom && !option.IsLocalCodex) profile.BaseUrl = string.Empty;
                profile.Model = model;
                profile.Name = string.IsNullOrWhiteSpace(model) ? option.Label : model;
                profile.IsEnabled = first == null;
                profile.UseModelDefaults = true;
                CopilotModelDefaults.Apply(profile);
                first ??= profile;
                Profiles.Add(profile);
            }
            SelectedProfile = first;
            RebuildProviders();
            IsAddingModel = false;
            MarkSettingsPending("已添加供应商，可填写连接信息和选择模型，点击应用或保存后生效。");
        }

        public CopilotProfileConfig CreateModelDraft(CopilotProfileConfig? source)
        {
            var draft = (source ?? SelectedProvider!.Connection).Clone();
            if (source == null)
            {
                draft.Id = Guid.NewGuid().ToString("N");
                draft.Model = string.Empty;
                draft.Name = string.Empty;
                draft.IsEnabled = true;
                draft.UseModelDefaults = true;
                CopilotModelDefaults.Apply(draft);
            }
            return draft;
        }

        public bool CommitModelDraft(CopilotProfileConfig draft, CopilotProfileConfig? original, out string error)
        {
            error = string.Empty;
            var provider = SelectedProvider;
            if (provider == null) return false;
            if (!draft.IsLocalCodex && string.IsNullOrWhiteSpace(draft.Model)) error = "请输入模型 ID。";
            else if (provider.Models.Any(model => !ReferenceEquals(model, original) && string.Equals(model.Model, draft.Model, StringComparison.OrdinalIgnoreCase))) error = "此供应商已存在相同模型。";
            else if (draft.ContextWindowTokens > 0 && draft.MaxTokens >= draft.ContextWindowTokens) error = "最大输出 Token 必须小于上下文窗口。";
            if (error.Length > 0) return false;
            foreach (var model in provider.Models) model.ConnectionId = provider.Id;
            draft.ConnectionId = provider.Id;
            draft.ConnectionName = provider.Name;
            draft.Name = string.IsNullOrWhiteSpace(draft.Model) ? provider.Name : draft.Model;
            if (original == null) Profiles.Add(draft);
            else Profiles[Profiles.IndexOf(original)] = draft;
            SelectedProfile = draft;
            RebuildProviders();
            MarkSettingsPending("模型配置已更新，点击应用或保存后生效。");
            return true;
        }

        public void DeleteProvider()
        {
            if (SelectedProvider == null) return;
            foreach (var model in SelectedProvider.Models) Profiles.Remove(model);
            SelectedProfile = Profiles.FirstOrDefault();
            RebuildProviders();
            MarkSettingsPending("已删除供应商，点击应用或保存后生效。");
        }

        public void DeleteModel(CopilotProfileConfig model)
        {
            if (SelectedProvider == null || SelectedProvider.Models.Count <= 1) return;
            Profiles.Remove(model);
            SelectedProfile = SelectedProvider.Models.First(item => !ReferenceEquals(item, model));
            RebuildProviders();
            MarkSettingsPending("已删除模型，点击应用或保存后生效。");
        }
    }
}
