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

        public ObservableCollection<CopilotConversationRecord> Conversations { get; set; } = new();

        public string ActiveProfileId
        {
            get => _activeProfileId;
            set => SetProperty(ref _activeProfileId, NormalizeText(value));
        }
        private string _activeProfileId = string.Empty;

        public string ActiveConversationId
        {
            get => _activeConversationId;
            set => SetProperty(ref _activeConversationId, NormalizeText(value));
        }
        private string _activeConversationId = string.Empty;

        [JsonIgnore]
        public bool IsConfigured => Profiles.Any(profile => profile.IsConfigured);

        [Browsable(false)]
        public bool AutoShowPanelOnFirstLaunch
        {
            get => _autoShowPanelOnFirstLaunch;
            set
            {
                _autoShowPanelOnFirstLaunch = value;
                OnPropertyChanged();
            }
        }
        private bool _autoShowPanelOnFirstLaunch = true;

        public bool EnsureInitialized()
        {
            var changed = false;

            Profiles ??= new ObservableCollection<CopilotProfileConfig>();
            Conversations ??= new ObservableCollection<CopilotConversationRecord>();

            if (Profiles.Count == 0)
            {
                Profiles.Add(CopilotProfileConfig.CreateDefault());
                changed = true;
            }

            foreach (var profile in Profiles)
            {
                changed |= profile.EnsureValid();
            }

            if (Profiles.Count > 0 && (string.IsNullOrWhiteSpace(ActiveProfileId) || Profiles.All(profile => profile.Id != ActiveProfileId)))
            {
                ActiveProfileId = Profiles[0].Id;
                changed = true;
            }

            foreach (var conversation in Conversations)
            {
                changed |= conversation.EnsureValid();

                if (string.IsNullOrWhiteSpace(conversation.ProfileId) || Profiles.All(profile => profile.Id != conversation.ProfileId))
                {
                    conversation.ProfileId = ActiveProfileId;
                    changed = true;
                }

                var profile = FindProfile(conversation.ProfileId);
                var profileName = profile?.DisplayLabel ?? string.Empty;
                if (!string.Equals(conversation.ProfileDisplayName, profileName, StringComparison.Ordinal))
                {
                    conversation.ProfileDisplayName = profileName;
                    changed = true;
                }

                var previousTitle = conversation.Title;
                var previousPreview = conversation.PreviewText;
                conversation.RefreshSummary();
                if (!string.Equals(previousTitle, conversation.Title, StringComparison.Ordinal)
                    || !string.Equals(previousPreview, conversation.PreviewText, StringComparison.Ordinal))
                {
                    changed = true;
                }
            }

            if (Conversations.Count == 0)
            {
                var conversation = CopilotConversationRecord.CreateEmpty(ActiveProfileId, FindProfile(ActiveProfileId)?.DisplayLabel ?? string.Empty);
                Conversations.Add(conversation);
                ActiveConversationId = conversation.Id;
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(ActiveConversationId) || Conversations.All(conversation => conversation.Id != ActiveConversationId))
            {
                ActiveConversationId = Conversations[0].Id;
                changed = true;
            }

            OnPropertyChanged(nameof(IsConfigured));
            return changed;
        }

        public CopilotProfileConfig? FindProfile(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal));
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

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public sealed class CopilotProfileConfig : ViewModelBase
    {
        public const int DefaultMaxTokens = 2048;
        public const double DefaultTemperature = 0.2;
        public const string DefaultSystemPrompt = "你是 ColorVision 内嵌 AI 助手，请用简洁、专业的中文回答，并尽量结合设备、流程、算法和插件场景给出建议。";

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        [DisplayName("名称")]
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, NormalizeText(value)))
                {
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(SecondaryLabel));
                }
            }
        }
        private string _name = "DeepSeek 默认";

        [DisplayName("协议类型")]
        [Description("OpenAI 兼容接口或 Anthropic 兼容接口")]
        public CopilotProviderType ProviderType
        {
            get => _providerType;
            set
            {
                if (SetProperty(ref _providerType, value))
                {
                    OnPropertyChanged(nameof(ProviderLabel));
                    OnPropertyChanged(nameof(SecondaryLabel));
                }
            }
        }
        private CopilotProviderType _providerType = CopilotProviderType.AnthropicCompatible;

        [DisplayName("API Key")]
        [Description("本地会加密保存")]
        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (SetProperty(ref _apiKey, NormalizeText(value)))
                    OnPropertyChanged(nameof(IsConfigured));
            }
        }
        private string _apiKey = string.Empty;

        [DisplayName("API 基础地址")]
        [Description("例如 https://api.openai.com/v1 或 https://api.deepseek.com/anthropic")]
        public string BaseUrl
        {
            get => _baseUrl;
            set
            {
                if (SetProperty(ref _baseUrl, NormalizeText(value)))
                    OnPropertyChanged(nameof(IsConfigured));
            }
        }
        private string _baseUrl = "https://api.deepseek.com/anthropic";

        [DisplayName("模型")]
        [Description("例如 gpt-4.1、deepseek-v4-pro")]
        public string Model
        {
            get => _model;
            set
            {
                if (SetProperty(ref _model, NormalizeText(value)))
                {
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(IsConfigured));
                    OnPropertyChanged(nameof(SecondaryLabel));
                }
            }
        }
        private string _model = "deepseek-v4-pro";

        [DisplayName("系统提示词")]
        [Description("定义助手的默认行为")]
        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value?.Trim() ?? string.Empty);
        }
        private string _systemPrompt = DefaultSystemPrompt;

        [DisplayName("最大输出 Token")]
        [Description("单次回复允许生成的最大 Token 数")]
        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, Math.Clamp(value, 128, 8192));
        }
        private int _maxTokens = DefaultMaxTokens;

        [DisplayName("温度")]
        [Description("数值越低越稳定，越高越发散")]
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, Math.Clamp(value, 0, 2));
        }
        private double _temperature = DefaultTemperature;

        [JsonIgnore]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(Model);

        [JsonIgnore]
        public string ProviderLabel => ProviderType == CopilotProviderType.AnthropicCompatible ? "Anthropic Compatible" : "OpenAI Compatible";

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name))
                    return Name;

                if (!string.IsNullOrWhiteSpace(Model))
                    return Model;

                return "未命名模型";
            }
        }

        [JsonIgnore]
        public string SecondaryLabel => $"{ProviderLabel} · {(string.IsNullOrWhiteSpace(Model) ? "未设置模型" : Model)}";

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (MaxTokens <= 0)
            {
                MaxTokens = DefaultMaxTokens;
                changed = true;
            }

            if (double.IsNaN(Temperature))
            {
                Temperature = DefaultTemperature;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(SystemPrompt))
            {
                SystemPrompt = DefaultSystemPrompt;
                changed = true;
            }

            return changed;
        }

        public CopilotProfileConfig Clone()
        {
            return new CopilotProfileConfig
            {
                Id = Id,
                Name = Name,
                ProviderType = ProviderType,
                ApiKey = ApiKey,
                BaseUrl = BaseUrl,
                Model = Model,
                SystemPrompt = SystemPrompt,
                MaxTokens = MaxTokens,
                Temperature = Temperature,
            };
        }

        public static CopilotProfileConfig CreateDefault()
        {
            return new CopilotProfileConfig
            {
                Name = "DeepSeek 默认",
                ProviderType = CopilotProviderType.AnthropicCompatible,
                BaseUrl = "https://api.deepseek.com/anthropic",
                Model = "deepseek-v4-pro",
                SystemPrompt = DefaultSystemPrompt,
                MaxTokens = DefaultMaxTokens,
                Temperature = DefaultTemperature,
            };
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public enum CopilotProviderType
    {
        [Description("OpenAI Compatible")]
        OpenAICompatible,
        [Description("Anthropic Compatible")]
        AnthropicCompatible,
    }
}
