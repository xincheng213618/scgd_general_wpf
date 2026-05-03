using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace ColorVision.Copilot
{
    public sealed class CopilotProfileConfig : ViewModelBase
    {
        public const int DefaultMaxTokens = 2048;
        public const double DefaultTemperature = 0.2;

        public const string DefaultSystemPrompt = "你是 ColorVision Copilot，是 ColorVision 软件内置的工程助手。你可以基于应用提供的上下文回答问题，包括当前流程、设备、算法节点、日志、附加文件和网页内容。规则：1. 只基于已提供的上下文回答，不要假设自己能直接访问本地文件、网页、日志或设备。2. 如果缺少必要上下文，明确说明还需要什么。3. 涉及设备、删除文件、修改配置或执行流程时，先提示风险。4. 回答要面向 ColorVision 的设备、流程、算法、插件和 WPF/C# 工程场景。5. 不要声称自己已经执行了未由应用上下文明确提供的操作。";

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
}