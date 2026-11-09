using LocalizedText = global::ColorVision.DisplayText;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotReasoningMode
    {
        Default = 0,
        Disabled = 1,
        Enabled = 2,
        High = 3,
        Max = 4,
        Low = 5,
        Medium = 6,
        XHigh = 7,
    }

    public sealed class CopilotReasoningOption
    {
        public CopilotReasoningOption(CopilotReasoningMode mode, string label, string description, bool isSelected)
        {
            Mode = mode;
            Label = label;
            Description = description;
            IsSelected = isSelected;
        }

        public CopilotReasoningMode Mode { get; }

        public string Label { get; }

        public string Description { get; }

        public bool IsSelected { get; }
    }

    public static class CopilotReasoningCapabilities
    {
        public static IReadOnlyList<CopilotReasoningOption> GetOptions(CopilotProfileConfig? profile)
        {
            var selected = GetEffectiveMode(profile);
            CopilotReasoningMode[] modes;
            if (profile != null && SupportsClaudeEffort(profile))
            {
                modes = SupportsClaudeExtraHighEffort(profile.Model)
                    ? new[]
                    {
                        CopilotReasoningMode.Default,
                        CopilotReasoningMode.Low,
                        CopilotReasoningMode.Medium,
                        CopilotReasoningMode.High,
                        CopilotReasoningMode.XHigh,
                    }
                    : new[]
                    {
                        CopilotReasoningMode.Default,
                        CopilotReasoningMode.Low,
                        CopilotReasoningMode.Medium,
                        CopilotReasoningMode.High,
                    };
            }
            else if (profile != null && SupportsOpenAiReasoningModes(profile))
            {
                modes = HasMandatoryOpenAiReasoning(profile)
                    ? new[]
                    {
                        CopilotReasoningMode.Default,
                        CopilotReasoningMode.Low,
                        CopilotReasoningMode.Medium,
                        CopilotReasoningMode.High,
                        CopilotReasoningMode.XHigh,
                        CopilotReasoningMode.Max,
                    }
                    : new[]
                    {
                        CopilotReasoningMode.Default,
                        CopilotReasoningMode.Disabled,
                        CopilotReasoningMode.Low,
                        CopilotReasoningMode.Medium,
                        CopilotReasoningMode.High,
                        CopilotReasoningMode.XHigh,
                        CopilotReasoningMode.Max,
                    };
            }
            else
            {
                modes = profile?.VendorType switch
                {
                    CopilotVendorType.DeepSeek => new[]
                    {
                        CopilotReasoningMode.Default,
                        CopilotReasoningMode.Disabled,
                        CopilotReasoningMode.High,
                        CopilotReasoningMode.Max,
                    },
                    CopilotVendorType.Xiaomi => new[]
                    {
                        CopilotReasoningMode.Default,
                        CopilotReasoningMode.Disabled,
                        CopilotReasoningMode.Enabled,
                    },
                    _ => new[] { CopilotReasoningMode.Default },
                };
            }

            var options = new List<CopilotReasoningOption>(modes.Length);
            foreach (var mode in modes)
                options.Add(new CopilotReasoningOption(mode, GetLabel(mode), GetDescription(profile?.VendorType, mode), mode == selected));

            return options;
        }

        public static CopilotReasoningMode GetEffectiveMode(CopilotProfileConfig? profile)
        {
            return profile == null
                ? CopilotReasoningMode.Default
                : Normalize(profile, profile.ReasoningMode);
        }

        public static CopilotReasoningMode Normalize(CopilotProfileConfig profile, CopilotReasoningMode mode)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var normalized = Normalize(profile.VendorType, mode);
            if (profile.VendorType == CopilotVendorType.Claude)
            {
                if (!SupportsClaudeEffort(profile))
                    return CopilotReasoningMode.Default;
                return normalized == CopilotReasoningMode.XHigh && !SupportsClaudeExtraHighEffort(profile.Model)
                    ? CopilotReasoningMode.High
                    : normalized;
            }

            if (!SupportsOpenAiReasoningModes(profile))
                return profile.VendorType == CopilotVendorType.OpenAI
                    ? CopilotReasoningMode.Default
                    : normalized;

            if (HasMandatoryOpenAiReasoning(profile))
            {
                return normalized switch
                {
                    CopilotReasoningMode.Disabled => CopilotReasoningMode.Low,
                    CopilotReasoningMode.Enabled => CopilotReasoningMode.Medium,
                    _ => normalized,
                };
            }

            return normalized == CopilotReasoningMode.Enabled
                ? CopilotReasoningMode.Medium
                : normalized;
        }

        public static CopilotReasoningMode Normalize(CopilotVendorType vendorType, CopilotReasoningMode mode)
        {
            if (!Enum.IsDefined(mode))
                return CopilotReasoningMode.Default;

            return vendorType switch
            {
                CopilotVendorType.DeepSeek => mode switch
                {
                    CopilotReasoningMode.Enabled => CopilotReasoningMode.High,
                    _ when mode is CopilotReasoningMode.Default or CopilotReasoningMode.Disabled or CopilotReasoningMode.High or CopilotReasoningMode.Max => mode,
                    _ => CopilotReasoningMode.Default,
                },
                CopilotVendorType.Xiaomi => mode switch
                {
                    CopilotReasoningMode.High or CopilotReasoningMode.Max => CopilotReasoningMode.Enabled,
                    _ when mode is CopilotReasoningMode.Default or CopilotReasoningMode.Disabled or CopilotReasoningMode.Enabled => mode,
                    _ => CopilotReasoningMode.Default,
                },
                CopilotVendorType.OpenAI => mode switch
                {
                    CopilotReasoningMode.Enabled => CopilotReasoningMode.Medium,
                    _ when mode is CopilotReasoningMode.Default
                        or CopilotReasoningMode.Disabled
                        or CopilotReasoningMode.Low
                        or CopilotReasoningMode.Medium
                        or CopilotReasoningMode.High
                        or CopilotReasoningMode.XHigh
                        or CopilotReasoningMode.Max => mode,
                    _ => CopilotReasoningMode.Default,
                },
                // Thinking cannot be turned off on current Claude models, and "max" has no shared effort value.
                CopilotVendorType.Claude => mode switch
                {
                    CopilotReasoningMode.Max => CopilotReasoningMode.XHigh,
                    _ when mode is CopilotReasoningMode.Low
                        or CopilotReasoningMode.Medium
                        or CopilotReasoningMode.High
                        or CopilotReasoningMode.XHigh => mode,
                    _ => CopilotReasoningMode.Default,
                },
                _ => CopilotReasoningMode.Default,
            };
        }

        public static string GetLabel(CopilotReasoningMode mode)
        {
            return mode switch
            {
                CopilotReasoningMode.Disabled => LocalizedText.Get("关闭"),
                CopilotReasoningMode.Enabled => LocalizedText.Get("开启"),
                CopilotReasoningMode.Low => LocalizedText.Get("低"),
                CopilotReasoningMode.Medium => LocalizedText.Get("中"),
                CopilotReasoningMode.High => LocalizedText.Get("高"),
                CopilotReasoningMode.XHigh => LocalizedText.Get("极高"),
                CopilotReasoningMode.Max => LocalizedText.Get("最高"),
                _ => LocalizedText.Get("默认"),
            };
        }

        public static string GetToolTip(CopilotProfileConfig? profile)
        {
            if (profile == null)
                return LocalizedText.Get("没有选中的模型配置。");

            var mode = GetEffectiveMode(profile);
            return LocalizedText.Format($"{profile.DisplayLabel} · 推理{GetLabel(mode)}\n{GetDescription(profile.VendorType, mode)}");
        }

        public static bool HasConfigurableReasoning(CopilotProfileConfig? profile)
        {
            return profile?.VendorType is CopilotVendorType.DeepSeek or CopilotVendorType.Xiaomi
                || profile != null && (SupportsOpenAiReasoningModes(profile) || SupportsClaudeEffort(profile));
        }

        // Compatible gateways and older models may reject adaptive thinking, which effort always sends here.
        private const int ClaudeThinkingHeadroomTokens = 4096;

        internal static bool SupportsClaudeEffort(CopilotProfileConfig profile)
        {
            return profile.VendorType == CopilotVendorType.Claude
                && CopilotAnthropicPromptCacheChatClient.IsOfficialEndpoint(profile)
                && (SupportsClaudeExtraHighEffort(profile.Model)
                    || profile.Model.StartsWith("claude-opus-4-6", StringComparison.OrdinalIgnoreCase)
                    || profile.Model.StartsWith("claude-sonnet-4-6", StringComparison.OrdinalIgnoreCase));
        }

        // These models think whether or not it is requested, and by default stream nothing while they do.
        // A summary therefore changes no reasoning, shows the user the progress, and keeps a long silent
        // thinking phase from reading as a stalled provider to the inactivity timeouts.
        internal static bool RequestsClaudeThinkingSummaryByDefault(CopilotProfileConfig profile)
        {
            return profile.VendorType == CopilotVendorType.Claude
                && CopilotAnthropicPromptCacheChatClient.IsOfficialEndpoint(profile)
                && ClaudeModelAlwaysThinks(profile.Model);
        }

        // max_tokens caps thinking and text together on these models and thinking cannot be turned off,
        // so a budget sized for the visible answer alone can stop with max_tokens before any text.
        // The model decides this, so it also applies through a gateway.
        internal static int GetBackgroundOutputTokens(CopilotProfileConfig profile, int visibleTokens)
        {
            return Math.Min(profile.MaxTokens, ClaudeModelAlwaysThinks(profile.Model) ? visibleTokens + ClaudeThinkingHeadroomTokens : visibleTokens);
        }

        // For calls with nothing to reason about, which should not run at the effort chosen for real work.
        internal static void UseLowClaudeEffort(CopilotProfileConfig profile)
        {
            if (SupportsClaudeEffort(profile))
                profile.ReasoningMode = CopilotReasoningMode.Low;
        }

        private static bool ClaudeModelAlwaysThinks(string model)
        {
            return model.StartsWith("claude-fable-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-mythos-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-sonnet-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-haiku-5-5", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SupportsClaudeExtraHighEffort(string model)
        {
            return model.StartsWith("claude-fable-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-mythos-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-4-7", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-4-8", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-sonnet-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-haiku-5-5", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SupportsOpenAiReasoningModes(CopilotProfileConfig profile)
        {
            return CopilotOpenAiRequestPolicy.IsOfficialOpenAiReasoningModel(profile)
                || profile.ProviderType == CopilotProviderType.LocalCodex
                && profile.VendorType == CopilotVendorType.OpenAI
                && CopilotOpenAiRequestPolicy.IsOpenAiReasoningModelName(profile.Model);
        }

        private static bool HasMandatoryOpenAiReasoning(CopilotProfileConfig profile)
        {
            return CopilotOpenAiRequestPolicy.HasMandatoryReasoning(profile)
                || profile.ProviderType == CopilotProviderType.LocalCodex
                && CopilotOpenAiRequestPolicy.HasMandatoryReasoningModelName(profile.Model);
        }

        public static CopilotReasoningOption? FindCommandOption(
            CopilotProfileConfig? profile,
            string? query)
        {
            var normalized = (query ?? string.Empty).Trim();
            if (normalized.Length == 0 || !HasConfigurableReasoning(profile))
                return null;

            return GetOptions(profile).FirstOrDefault(option =>
                IsCommandToken(option.Mode, option.Label, normalized));
        }

        public static string GetCommandOptionSummary(CopilotProfileConfig? profile)
        {
            return string.Join(
                "、",
                GetOptions(profile).Select(option =>
                    $"{GetCommandToken(option.Mode)}（{option.Label}）"));
        }

        private static bool IsCommandToken(
            CopilotReasoningMode mode,
            string label,
            string query)
        {
            if (string.Equals(query, label, StringComparison.OrdinalIgnoreCase)
                || string.Equals(query, mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return mode switch
            {
                CopilotReasoningMode.Default =>
                    string.Equals(query, "auto", StringComparison.OrdinalIgnoreCase),
                CopilotReasoningMode.Disabled =>
                    string.Equals(query, "off", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(query, "none", StringComparison.OrdinalIgnoreCase),
                CopilotReasoningMode.Enabled =>
                    string.Equals(query, "on", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        internal static string GetCommandToken(CopilotReasoningMode mode)
        {
            return mode switch
            {
                CopilotReasoningMode.Default => "auto",
                CopilotReasoningMode.Disabled => "off",
                CopilotReasoningMode.Enabled => "on",
                CopilotReasoningMode.Low => "low",
                CopilotReasoningMode.Medium => "medium",
                CopilotReasoningMode.High => "high",
                CopilotReasoningMode.XHigh => "xhigh",
                CopilotReasoningMode.Max => "max",
                _ => "auto",
            };
        }

        private static string GetDescription(CopilotVendorType? vendorType, CopilotReasoningMode mode)
        {
            if (vendorType == CopilotVendorType.DeepSeek)
            {
                return mode switch
                {
                    CopilotReasoningMode.Disabled => "关闭 DeepSeek 思考模式，Temperature 等采样参数重新生效。",
                    CopilotReasoningMode.High => "使用 DeepSeek 高推理强度。",
                    CopilotReasoningMode.Max => "使用 DeepSeek 最高推理强度，通常耗时更长并消耗更多 Token。",
                    _ => "由 DeepSeek 按请求自动选择推理强度。",
                };
            }

            if (vendorType == CopilotVendorType.Xiaomi)
            {
                return mode switch
                {
                    CopilotReasoningMode.Disabled => "关闭 MiMo 深度思考。",
                    CopilotReasoningMode.Enabled => "开启 MiMo 深度思考；MiMo 当前没有可区分的多档强度。",
                    _ => "由 MiMo 使用服务端默认的思考模式。",
                };
            }

            if (vendorType == CopilotVendorType.OpenAI)
            {
                return mode switch
                {
                    CopilotReasoningMode.Disabled => "关闭支持该档位的 OpenAI 推理模型的显式推理。",
                    CopilotReasoningMode.Low => "使用 OpenAI 低推理强度；GPT-6 Astra 和 GPT-6.1 Sol 的最低可选档位。",
                    CopilotReasoningMode.Medium => "使用 OpenAI 中等推理强度。",
                    CopilotReasoningMode.High => "使用 OpenAI 高推理强度。",
                    CopilotReasoningMode.XHigh => "使用 OpenAI 极高推理强度。",
                    CopilotReasoningMode.Max => "使用 OpenAI 最高推理强度，通常耗时更长并消耗更多 Token。",
                    _ => "由 OpenAI 使用模型默认的推理强度。",
                };
            }

            if (vendorType == CopilotVendorType.Claude)
            {
                return mode switch
                {
                    CopilotReasoningMode.Low => "使用 Claude 低推理强度，响应更快、消耗更少 Token。",
                    CopilotReasoningMode.Medium => "使用 Claude 中等推理强度。",
                    CopilotReasoningMode.High => "使用 Claude 高推理强度。",
                    CopilotReasoningMode.XHigh => "使用 Claude 极高推理强度，适合复杂的编码和智能体任务；最大输出 Token 建议不低于 64000，避免回答被截断。",
                    _ => "由 Claude 使用模型默认的推理强度。",
                };
            }

            return "当前供应商没有声明可配置的推理强度，使用服务端默认值。";
        }
    }

    internal static class CopilotReasoningRequestMapper
    {
        public static void Apply(CopilotProfileConfig profile, IDictionary<string, object?> payload)
        {
            var mode = CopilotReasoningCapabilities.GetEffectiveMode(profile);
            if (mode == CopilotReasoningMode.Default)
            {
                if (CopilotReasoningCapabilities.RequestsClaudeThinkingSummaryByDefault(profile))
                    payload["thinking"] = CreateClaudeThinking();
                return;
            }

            if (CopilotOpenAiRequestPolicy.UsesResponsesApi(profile))
            {
                var effort = mode switch
                {
                    CopilotReasoningMode.Disabled => "none",
                    CopilotReasoningMode.Low => "low",
                    CopilotReasoningMode.Enabled or CopilotReasoningMode.Medium => "medium",
                    CopilotReasoningMode.High => "high",
                    CopilotReasoningMode.XHigh => "xhigh",
                    CopilotReasoningMode.Max => "max",
                    _ => string.Empty,
                };
                if (effort.Length > 0)
                    payload["reasoning"] = new Dictionary<string, object?> { ["effort"] = effort };
                return;
            }

            if (profile.VendorType == CopilotVendorType.DeepSeek)
            {
                payload["thinking"] = CreateThinking(mode != CopilotReasoningMode.Disabled);
                if (mode is CopilotReasoningMode.High or CopilotReasoningMode.Max)
                {
                    var effort = mode == CopilotReasoningMode.Max ? "max" : "high";
                    if (profile.ProviderType == CopilotProviderType.AnthropicCompatible)
                        payload["output_config"] = new Dictionary<string, object?> { ["effort"] = effort };
                    else
                        payload["reasoning_effort"] = effort;
                }

                return;
            }

            if (profile.VendorType == CopilotVendorType.Xiaomi)
            {
                payload["thinking"] = CreateThinking(mode != CopilotReasoningMode.Disabled);
                return;
            }

            if (profile.VendorType == CopilotVendorType.Claude)
            {
                payload["thinking"] = CreateClaudeThinking();
                payload["output_config"] = new Dictionary<string, object?>
                {
                    ["effort"] = CopilotReasoningCapabilities.GetCommandToken(mode),
                };
            }
        }

        public static bool ShouldIncludeTemperature(CopilotProfileConfig profile)
        {
            if (!CopilotOpenAiRequestPolicy.SupportsTemperature(profile))
                return false;

            if (profile.VendorType == CopilotVendorType.Claude
                && CopilotAnthropicPromptCacheChatClient.IsOfficialEndpoint(profile)
                && ClaudeRejectsSampling(profile.Model))
                return false;

            var mode = CopilotReasoningCapabilities.GetEffectiveMode(profile);
            // Adaptive thinking does not take a custom temperature on the older models that still accept one.
            if (profile.VendorType == CopilotVendorType.Claude)
                return mode == CopilotReasoningMode.Default;

            if (profile.VendorType == CopilotVendorType.DeepSeek)
                return mode is CopilotReasoningMode.Default or CopilotReasoningMode.Disabled;

            return profile.VendorType != CopilotVendorType.Xiaomi || mode == CopilotReasoningMode.Disabled;
        }

        private static bool ClaudeRejectsSampling(string model)
        {
            return model.StartsWith("claude-fable-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-mythos-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-4-7", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-opus-4-8", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-sonnet-5", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("claude-haiku-5-5", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, object?> CreateClaudeThinking()
        {
            return new Dictionary<string, object?> { ["type"] = "adaptive", ["display"] = "summarized" };
        }

        private static Dictionary<string, object?> CreateThinking(bool enabled)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = enabled ? "enabled" : "disabled",
            };
        }
    }
}
