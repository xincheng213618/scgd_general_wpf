using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public enum CopilotReasoningMode
    {
        Default,
        Disabled,
        Enabled,
        High,
        Max,
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
            var modes = profile?.VendorType switch
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

            var options = new List<CopilotReasoningOption>(modes.Length);
            foreach (var mode in modes)
                options.Add(new CopilotReasoningOption(mode, GetLabel(mode), GetDescription(profile?.VendorType, mode), mode == selected));

            return options;
        }

        public static CopilotReasoningMode GetEffectiveMode(CopilotProfileConfig? profile)
        {
            return profile == null
                ? CopilotReasoningMode.Default
                : Normalize(profile.VendorType, profile.ReasoningMode);
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
                _ => CopilotReasoningMode.Default,
            };
        }

        public static string GetLabel(CopilotReasoningMode mode)
        {
            return mode switch
            {
                CopilotReasoningMode.Disabled => "关闭",
                CopilotReasoningMode.Enabled => "开启",
                CopilotReasoningMode.High => "高",
                CopilotReasoningMode.Max => "最高",
                _ => "默认",
            };
        }

        public static string GetToolTip(CopilotProfileConfig? profile)
        {
            if (profile == null)
                return "没有选中的模型配置。";

            var mode = GetEffectiveMode(profile);
            return $"{profile.DisplayLabel} · 推理{GetLabel(mode)}\n{GetDescription(profile.VendorType, mode)}";
        }

        public static bool HasConfigurableReasoning(CopilotProfileConfig? profile)
        {
            return profile?.VendorType is CopilotVendorType.DeepSeek or CopilotVendorType.Xiaomi;
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

            return "当前供应商没有声明可配置的推理强度，使用服务端默认值。";
        }
    }

    internal static class CopilotReasoningRequestMapper
    {
        public static void Apply(CopilotProfileConfig profile, IDictionary<string, object?> payload)
        {
            var mode = CopilotReasoningCapabilities.GetEffectiveMode(profile);
            if (mode == CopilotReasoningMode.Default)
                return;

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
                payload["thinking"] = CreateThinking(mode != CopilotReasoningMode.Disabled);
        }

        public static bool ShouldIncludeTemperature(CopilotProfileConfig profile)
        {
            var mode = CopilotReasoningCapabilities.GetEffectiveMode(profile);
            if (profile.VendorType == CopilotVendorType.DeepSeek)
                return mode is CopilotReasoningMode.Default or CopilotReasoningMode.Disabled;

            return profile.VendorType != CopilotVendorType.Xiaomi || mode != CopilotReasoningMode.Enabled;
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
