using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace ColorVision
{
    public class CopilotConfig : ViewModelBase, IConfigSecure
    {
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "CopilotConfig";

        public static CopilotConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CopilotConfig>();

        [DisplayName("协议类型")]
        [Description("OpenAI 兼容接口或 Anthropic 兼容接口")]
        public CopilotProviderType ProviderType
        {
            get => _providerType;
            set
            {
                _providerType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsConfigured));
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
                _apiKey = value?.Trim() ?? string.Empty;
                OnPropertyChanged();
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
                _baseUrl = NormalizeText(value);
                OnPropertyChanged();
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
                _model = NormalizeText(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsConfigured));
            }
        }
        private string _model = "deepseek-v4-pro";

        [DisplayName("系统提示词")]
        [Description("定义助手的默认行为")]
        public string SystemPrompt
        {
            get => _systemPrompt;
            set
            {
                _systemPrompt = value?.Trim() ?? string.Empty;
                OnPropertyChanged();
            }
        }
        private string _systemPrompt = "你是 ColorVision 内嵌 AI 助手，请用简洁、专业的中文回答，并尽量结合设备、流程、算法和插件场景给出建议。";

        [DisplayName("最大输出 Token")]
        [Description("单次回复允许生成的最大 Token 数")]
        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                _maxTokens = Math.Clamp(value, 128, 8192);
                OnPropertyChanged();
            }
        }
        private int _maxTokens = 2048;

        [DisplayName("温度")]
        [Description("数值越低越稳定，越高越发散")]
        public double Temperature
        {
            get => _temperature;
            set
            {
                _temperature = Math.Clamp(value, 0, 2);
                OnPropertyChanged();
            }
        }
        private double _temperature = 0.2;

        [JsonIgnore]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(Model);

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

        public void Encryption()
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
                ApiKey = Cryptography.AESEncrypt(ApiKey, ConfigAESKey, ConfigAESVector);
        }

        public void Decrypt()
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
                ApiKey = Cryptography.AESDecrypt(ApiKey, ConfigAESKey, ConfigAESVector);
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
