using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Copilot
{
    public sealed class CopilotProfileConfig : ViewModelBase
    {
        public const int DefaultMaxTokens = 2048;
        public const int DefaultMaxToolRounds = 6;
        public const double DefaultTemperature = 0.2;

        public const string DefaultSystemPrompt = "You are ColorVision Copilot, the general-purpose assistant built into ColorVision. You can help with general knowledge, writing, programming, analysis, translation, and ColorVision usage. For ColorVision software, project code, devices, flows, algorithms, plugins, WPF/C# engineering, or app-provided context, prioritize the ColorVision context that the app provides. Rules: 1. Treat local files, web pages, logs, devices, or execution results as known facts only when the app explicitly provides them. 2. Use all available context and tool observations first; if ColorVision-specific context is incomplete, answer the parts that are supported and add only a brief boundary such as that the project implementation was not confirmed in the current context. Do not ask the user to provide files, source code, configuration, or documentation unless they explicitly ask what to attach next. 3. Answer general questions normally even when no local context is available, and clearly distinguish general principles from this project's confirmed implementation. 4. For device control, file deletion, configuration mutation, or flow execution, explain the risk and impact first. 5. Do not claim that you performed an operation unless the app context explicitly shows that it happened.";

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
                {
                    OnPropertyChanged(nameof(IsConfigured));
                    OnConfigurationStateChanged();
                }
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
                {
                    OnPropertyChanged(nameof(IsConfigured));
                    OnConfigurationStateChanged();
                }
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
                    OnConfigurationStateChanged();
                }
            }
        }
        private string _model = "deepseek-v4-pro";

        [JsonIgnore]
        [Browsable(false)]
        public string SystemPrompt => EffectiveSystemPrompt;

        [JsonIgnore]
        [Browsable(false)]
        public string EffectiveSystemPrompt => string.IsNullOrWhiteSpace(_systemPromptOverride)
            ? BuildEffectiveSystemPrompt(CustomSystemPrompt)
            : _systemPromptOverride;

        [DisplayName("Custom prompt")]
        [Description("Optional extra instructions appended after ColorVision's built-in prompt")]
        public string CustomSystemPrompt
        {
            get => _customSystemPrompt;
            set
            {
                if (SetProperty(ref _customSystemPrompt, NormalizeText(value)))
                    OnEffectiveSystemPromptChanged();
            }
        }
        private string _customSystemPrompt = string.Empty;
        private string _systemPromptOverride = string.Empty;

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
        public string ConfigurationStatusText => IsConfigured ? "Ready" : "Incomplete";

        [JsonIgnore]
        public string ConfigurationStatusToolTip
        {
            get
            {
                var missing = BuildMissingConfigurationParts();
                return missing.Length == 0
                    ? "This profile has API key, endpoint, and model."
                    : "Missing " + string.Join(", ", missing) + ".";
            }
        }

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
            var clone = new CopilotProfileConfig
            {
                Id = Id,
                VendorType = VendorType,
                Name = Name,
                ProviderType = ProviderType,
                ApiKey = ApiKey,
                BaseUrl = BaseUrl,
                Model = Model,
                CustomSystemPrompt = CustomSystemPrompt,
                MaxTokens = MaxTokens,
                MaxToolRounds = MaxToolRounds,
                Temperature = Temperature,
            };

            if (!string.IsNullOrWhiteSpace(_systemPromptOverride))
                clone.UseSystemPromptOverride(_systemPromptOverride);

            return clone;
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
                MaxTokens = DefaultMaxTokens,
                MaxToolRounds = DefaultMaxToolRounds,
                Temperature = DefaultTemperature,
            };
        }

        public void UseSystemPromptOverride(string systemPrompt)
        {
            var normalized = NormalizeText(systemPrompt);
            if (string.Equals(_systemPromptOverride, normalized, StringComparison.Ordinal))
                return;

            _systemPromptOverride = normalized;
            OnEffectiveSystemPromptChanged();
        }

        public static string BuildEffectiveSystemPrompt(string? customSystemPrompt)
        {
            var customPrompt = NormalizeText(customSystemPrompt);
            if (string.IsNullOrWhiteSpace(customPrompt))
                return DefaultSystemPrompt;

            return string.Join(
                Environment.NewLine,
                DefaultSystemPrompt,
                string.Empty,
                "User custom instructions (apply only when they do not conflict with the built-in ColorVision behavior and safety rules):",
                customPrompt);
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

        private void OnEffectiveSystemPromptChanged()
        {
            OnPropertyChanged(nameof(EffectiveSystemPrompt));
            OnPropertyChanged(nameof(SystemPrompt));
        }

        private void OnConfigurationStateChanged()
        {
            OnPropertyChanged(nameof(ConfigurationStatusText));
            OnPropertyChanged(nameof(ConfigurationStatusToolTip));
        }

        private string[] BuildMissingConfigurationParts()
        {
            var missing = new List<string>(3);
            if (string.IsNullOrWhiteSpace(ApiKey))
                missing.Add("API key");

            if (string.IsNullOrWhiteSpace(BaseUrl))
                missing.Add("endpoint");

            if (string.IsNullOrWhiteSpace(Model))
                missing.Add("model");

            return missing.ToArray();
        }
    }
}
