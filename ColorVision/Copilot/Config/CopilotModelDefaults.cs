using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed record CopilotModelCapabilities(int? ContextWindowTokens, int? MaxOutputTokens, bool SupportsImages);

    public static class CopilotModelDefaults
    {
        // Exact IDs only: an arbitrary compatible endpoint/model must not inherit guessed capabilities.
        // Sources: developers.openai.com/api/docs/models/{model}; platform.claude.com/docs/en/models/overview.
        private static readonly IReadOnlyDictionary<string, CopilotModelCapabilities> Models = new Dictionary<string, CopilotModelCapabilities>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-6-astra"] = new(1_050_000, 128_000, true),
            ["gpt-6.1-sol"] = new(1_050_000, 128_000, true),
            ["gpt-6-sol"] = new(1_050_000, 128_000, true),
            ["gpt-6-luna"] = new(1_050_000, 128_000, true),
            ["gpt-5.6"] = new(1_050_000, 128_000, true),
            ["gpt-5.6-sol"] = new(1_050_000, 128_000, true),
            ["gpt-5.6-terra"] = new(1_050_000, 128_000, true),
            ["gpt-5.6-luna"] = new(1_050_000, 128_000, true),
            ["gpt-5.4"] = new(1_050_000, 128_000, true),
            ["claude-fable-5-1"] = new(1_000_000, 128_000, true),
            ["claude-opus-5-5"] = new(1_000_000, 128_000, true),
            ["claude-sonnet-5-5"] = new(1_000_000, 128_000, true),
            ["claude-haiku-5-5"] = new(1_000_000, 128_000, true),
            // api-docs.deepseek.com/api/list-models/
            ["deepseek-flash"] = new(1_048_576, 393_216, true),
            ["deepseek-v4-pro"] = new(1_048_576, 393_216, false),
            // mimo.mi.com/docs/tokenplan/integration/opencode
            ["mimo-v2.6-pro"] = new(1_048_576, 131_072, true),
            ["mimo-v2.6-flash"] = new(1_048_576, 131_072, true),
            // ai.google.dev/gemini-api/docs/models/{model}
            ["gemini-3.1-pro-preview"] = new(1_048_576, 65_536, true),
            ["gemini-3.8-flash"] = new(1_048_576, 65_536, true),
            ["gemini-3.7-flash"] = new(1_048_576, 65_536, true),
            ["gemini-3.6-flash"] = new(1_048_576, 65_536, true),
            ["gemini-3.5-flash-lite"] = new(1_048_576, 65_536, true),
            // docs.bigmodel.cn/cn/guide/models/: only exact verified modalities; K/M limits remain manual.
            ["glm-5.3"] = new(null, null, false),
            ["glm-5.2"] = new(null, null, false),
            ["glm-5.3-flash"] = new(null, null, true),
            ["glm-5.3-flashx"] = new(null, null, true),
            ["glm-5-turbo"] = new(null, null, false),
            ["glm-4.7-flash"] = new(null, null, false),
            ["glm-4.5-air"] = new(null, null, false),
            // docs.x.ai/developers/models/{model}; unspecified output limits use the application default.
            ["grok-4.7"] = new(500_000, null, true),
            ["grok-4.6"] = new(500_000, null, true),
            ["grok-4.5"] = new(500_000, null, true),
            ["grok-4.3"] = new(1_000_000, null, true),
            // platform.minimax.io/docs/api-reference/text-anthropic-api
            ["MiniMax-M3"] = new(1_000_000, null, true),
            ["MiniMax-M2.7"] = new(null, null, false),
            ["MiniMax-M2.7-highspeed"] = new(null, null, false),
            ["MiniMax-M2.5"] = new(null, null, false),
            ["MiniMax-M2.5-highspeed"] = new(null, null, false),
            // github.com/OpenSenseNova/SenseNova6.8/blob/main/API.md
            ["sensenova-6.8-flash-lite"] = new(null, null, true),
        };

        public static CopilotModelCapabilities? Find(CopilotProfileConfig profile)
        {
            if (!profile.IsLocalCodex && profile.VendorType == CopilotVendorType.Custom) return null;
            return Models.TryGetValue(profile.Model, out var metadata) ? metadata : null;
        }

        public static bool Apply(CopilotProfileConfig profile)
        {
            var metadata = Find(profile);
            profile.ContextWindowTokens = metadata?.ContextWindowTokens ?? 0;
            profile.MaxTokens = metadata?.MaxOutputTokens ?? CopilotProfileConfig.DefaultMaxTokens;
            profile.SupportsImageInput = metadata?.SupportsImages ?? (profile.IsLocalCodex && string.IsNullOrWhiteSpace(profile.Model));
            return metadata != null;
        }
    }
}
