using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotVendorType
    {
        Custom,
        DeepSeek,
        OpenAI,
        Claude,
        Grok,
        Gemini,
        GLM,
        MiniMax,
        Xiaomi,
    }

    public sealed class CopilotVendorOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotVendorType Value { get; init; }
    }

    public sealed class CopilotVendorPreset
    {
        public CopilotVendorType VendorType { get; init; }

        public string Label { get; init; } = string.Empty;

        public CopilotProviderType DefaultProviderType { get; init; } = CopilotProviderType.OpenAICompatible;

        public string OpenAICompatibleBaseUrl { get; init; } = string.Empty;

        public string AnthropicCompatibleBaseUrl { get; init; } = string.Empty;

        public IReadOnlyList<string> ModelPresets { get; init; } = Array.Empty<string>();
    }

    public static class CopilotVendorCatalog
    {
        private static readonly IReadOnlyList<CopilotVendorPreset> Presets = new ReadOnlyCollection<CopilotVendorPreset>(new[]
        {
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.DeepSeek,
                Label = "DeepSeek",
                DefaultProviderType = CopilotProviderType.AnthropicCompatible,
                OpenAICompatibleBaseUrl = "https://api.deepseek.com/v1",
                AnthropicCompatibleBaseUrl = "https://api.deepseek.com/anthropic",
                ModelPresets = new[] { "deepseek-v4-flash", "deepseek-v4-pro" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.OpenAI,
                Label = "OpenAI",
                DefaultProviderType = CopilotProviderType.OpenAICompatible,
                OpenAICompatibleBaseUrl = "https://api.openai.com/v1",
                ModelPresets = new[] { "gpt-5.5", "gpt-4o"},
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.Claude,
                Label = "Claude",
                DefaultProviderType = CopilotProviderType.AnthropicCompatible,
                AnthropicCompatibleBaseUrl = "https://api.anthropic.com",
                ModelPresets = new[] { "claude-sonnet-4-7", "claude-opus-4-7" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.Grok,
                Label = "Grok / xAI",
                DefaultProviderType = CopilotProviderType.OpenAICompatible,
                OpenAICompatibleBaseUrl = "https://api.x.ai/v1",
                ModelPresets = new[] { "grok-4", "grok-3", "grok-3-mini" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.Gemini,
                Label = "Gemini",
                DefaultProviderType = CopilotProviderType.OpenAICompatible,
                OpenAICompatibleBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai",
                ModelPresets = new[] { "gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.0-flash" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.GLM,
                Label = "Zhipu GLM",
                DefaultProviderType = CopilotProviderType.OpenAICompatible,
                OpenAICompatibleBaseUrl = "https://open.bigmodel.cn/api/paas/v4",
                ModelPresets = new[] { "glm-4.5", "glm-4.5-air", "glm-4-flash" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.MiniMax,
                Label = "MiniMax",
                DefaultProviderType = CopilotProviderType.AnthropicCompatible,
                AnthropicCompatibleBaseUrl = "https://api.minimaxi.com/anthropic",
                ModelPresets = new[] { "MiniMax-M2.7", "MiniMax-M1", "MiniMax-Text-01" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.Xiaomi,
                Label = "Xiaomi Mimo",
                DefaultProviderType = CopilotProviderType.AnthropicCompatible,
                AnthropicCompatibleBaseUrl = "https://api.xiaomimimo.com/anthropic",
                ModelPresets = new[] { "mimo-v2.5-pro", "mimo-v2.5" },
            },
            new CopilotVendorPreset
            {
                VendorType = CopilotVendorType.Custom,
                Label = "Custom",
                DefaultProviderType = CopilotProviderType.OpenAICompatible,
                ModelPresets = Array.Empty<string>(),
            },
        });

        public static IReadOnlyList<CopilotVendorOption> VendorOptions { get; } = new ReadOnlyCollection<CopilotVendorOption>(
            Presets.Select(preset => new CopilotVendorOption
            {
                Label = preset.Label,
                Value = preset.VendorType,
            }).ToArray());

        public static CopilotVendorPreset GetPreset(CopilotVendorType vendorType)
        {
            return Presets.FirstOrDefault(preset => preset.VendorType == vendorType)
                ?? Presets.First(preset => preset.VendorType == CopilotVendorType.Custom);
        }

        public static string GetLabel(CopilotVendorType vendorType) => GetPreset(vendorType).Label;

        public static IReadOnlyList<string> GetModelPresets(CopilotVendorType vendorType) => GetPreset(vendorType).ModelPresets;

        public static string GetDefaultBaseUrl(CopilotVendorType vendorType, CopilotProviderType providerType)
        {
            var preset = GetPreset(vendorType);
            return providerType == CopilotProviderType.AnthropicCompatible
                ? preset.AnthropicCompatibleBaseUrl
                : preset.OpenAICompatibleBaseUrl;
        }

        public static CopilotVendorType InferVendorType(string? baseUrl, string? model)
        {
            var normalizedBaseUrl = (baseUrl ?? string.Empty).Trim();
            var normalizedModel = (model ?? string.Empty).Trim();

            if (ContainsAny(normalizedBaseUrl, "deepseek") || ContainsAny(normalizedModel, "deepseek"))
                return CopilotVendorType.DeepSeek;

            if (ContainsAny(normalizedBaseUrl, "xiaomimimo", "mimo") || ContainsAny(normalizedModel, "mimo"))
                return CopilotVendorType.Xiaomi;

            if (ContainsAny(normalizedBaseUrl, "minimaxi", "minimax") || ContainsAny(normalizedModel, "minimax"))
                return CopilotVendorType.MiniMax;

            if (ContainsAny(normalizedBaseUrl, "bigmodel", "zhipu") || ContainsAny(normalizedModel, "glm"))
                return CopilotVendorType.GLM;

            if (ContainsAny(normalizedBaseUrl, "generativelanguage", "googleapis") || ContainsAny(normalizedModel, "gemini"))
                return CopilotVendorType.Gemini;

            if (ContainsAny(normalizedBaseUrl, "x.ai") || ContainsAny(normalizedModel, "grok"))
                return CopilotVendorType.Grok;

            if (ContainsAny(normalizedBaseUrl, "api.anthropic.com") || ContainsAny(normalizedModel, "claude"))
                return CopilotVendorType.Claude;

            if (ContainsAny(normalizedBaseUrl, "openai") || StartsWithAny(normalizedModel, "gpt-", "o1", "o3", "o4"))
                return CopilotVendorType.OpenAI;

            return CopilotVendorType.Custom;
        }

        private static bool ContainsAny(string value, params string[] markers)
        {
            return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static bool StartsWithAny(string value, params string[] markers)
        {
            return markers.Any(marker => value.StartsWith(marker, StringComparison.OrdinalIgnoreCase));
        }
    }
}
