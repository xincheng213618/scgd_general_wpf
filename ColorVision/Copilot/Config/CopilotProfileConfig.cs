using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotProfileConfig : ViewModelBase
    {
        public const int DefaultMaxTokens = 2048;
        public const int DefaultMaxToolRounds = 6;
        public const double DefaultTemperature = 0.2;

        public const string DefaultSystemPrompt = "You are ColorVision Copilot, the general-purpose assistant built into ColorVision. You can help with general knowledge, writing, programming, analysis, translation, and ColorVision usage. For ColorVision software, project code, devices, flows, algorithms, plugins, WPF/C# engineering, or app-provided context, prioritize the provided ColorVision context. Rules: 1. Treat local files, web pages, logs, devices, or execution results as known facts only when the app explicitly provides them. 2. If required context is missing, clearly say what is needed. 3. For device control, file deletion, configuration mutation, or flow execution, explain the risk and impact first. 4. Answer general questions normally, and use ColorVision context for ColorVision-related questions. 5. Do not claim that you performed an operation unless the app context explicitly shows that it happened.";

        private static readonly string[] LegacyDefaultSystemPromptMarkers =
        {
            "\u4f60\u662f ColorVision Copilot",
            "ColorVision \u8f6f\u4ef6\u5185\u7f6e",
            "\u4e0d\u8981\u58f0\u79f0\u81ea\u5df1\u5df2\u7ecf\u6267\u884c",
        };

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        [DisplayName("Vendor")]
        public CopilotVendorType VendorType
        {
            get => _vendorType;
            set
            {
                if (SetProperty(ref _vendorType, value))
                {
                    OnPropertyChanged(nameof(VendorLabel));
                    OnPropertyChanged(nameof(SecondaryLabel));
                }
            }
        }
        private CopilotVendorType _vendorType = CopilotVendorType.Custom;

        [DisplayName("Name")]
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
        private string _name = "DeepSeek Default";

        [DisplayName("Protocol")]
        [Description("OpenAI-compatible or Anthropic-compatible API")]
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
        [Description("Stored locally with encryption")]
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

        [DisplayName("Base URL")]
        [Description("For example, https://api.openai.com/v1 or https://api.deepseek.com/anthropic")]
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

        [DisplayName("Model")]
        [Description("For example, gpt-4.1 or deepseek-v4-pro")]
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

        [DisplayName("System prompt")]
        [Description("Defines the assistant's default behavior")]
        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value?.Trim() ?? string.Empty);
        }
        private string _systemPrompt = DefaultSystemPrompt;

        [DisplayName("Max output tokens")]
        [Description("Maximum tokens generated in one response")]
        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, Math.Clamp(value, 128, 8192));
        }
        private int _maxTokens = DefaultMaxTokens;

        [DisplayName("Temperature")]
        [Description("Lower values are more stable; higher values are more creative")]
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, Math.Clamp(value, 0, 2));
        }
        private double _temperature = DefaultTemperature;

        [DisplayName("Max tool rounds")]
        [Description("Maximum tool-call rounds allowed for one Agent request")]
        public int MaxToolRounds
        {
            get => _maxToolRounds;
            set => SetProperty(ref _maxToolRounds, Math.Max(1, value));
        }
        private int _maxToolRounds = DefaultMaxToolRounds;

        [JsonIgnore]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(Model);

        [JsonIgnore]
        public string VendorLabel => CopilotVendorCatalog.GetLabel(VendorType);

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

                return "Unnamed model";
            }
        }

        [JsonIgnore]
        public string SecondaryLabel => $"{VendorLabel} · {ProviderLabel} · {(string.IsNullOrWhiteSpace(Model) ? "Model not set" : Model)}";

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

            if (MaxToolRounds <= 0)
            {
                MaxToolRounds = DefaultMaxToolRounds;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(SystemPrompt) || IsLegacyDefaultSystemPrompt(SystemPrompt))
            {
                SystemPrompt = DefaultSystemPrompt;
                changed = true;
            }

            if (!Enum.IsDefined(VendorType))
            {
                VendorType = CopilotVendorType.Custom;
                changed = true;
            }

            if (VendorType == CopilotVendorType.Custom)
            {
                var inferredVendor = CopilotVendorCatalog.InferVendorType(BaseUrl, Model);
                if (inferredVendor != CopilotVendorType.Custom)
                {
                    VendorType = inferredVendor;
                    changed = true;
                }
            }

            return changed;
        }

        public CopilotProfileConfig Clone()
        {
            return new CopilotProfileConfig
            {
                Id = Id,
                VendorType = VendorType,
                Name = Name,
                ProviderType = ProviderType,
                ApiKey = ApiKey,
                BaseUrl = BaseUrl,
                Model = Model,
                SystemPrompt = SystemPrompt,
                MaxTokens = MaxTokens,
                MaxToolRounds = MaxToolRounds,
                Temperature = Temperature,
            };
        }

        public static CopilotProfileConfig CreateDefault()
        {
            return new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.DeepSeek,
                Name = "DeepSeek Default",
                ProviderType = CopilotProviderType.AnthropicCompatible,
                BaseUrl = "https://api.deepseek.com/anthropic",
                Model = "deepseek-v4-pro",
                SystemPrompt = DefaultSystemPrompt,
                MaxTokens = DefaultMaxTokens,
                MaxToolRounds = DefaultMaxToolRounds,
                Temperature = DefaultTemperature,
            };
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

        private static bool IsLegacyDefaultSystemPrompt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return LegacyDefaultSystemPromptMarkers.All(marker => value.Contains(marker, StringComparison.Ordinal));
        }
    }
}
