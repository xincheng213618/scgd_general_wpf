using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed record CopilotModelCapabilities(int ContextWindowTokens, int MaxOutputTokens, bool SupportsImages);

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
        };

        public static CopilotModelCapabilities? Find(CopilotProfileConfig profile)
        {
            if (!profile.IsLocalCodex && profile.VendorType is not (CopilotVendorType.OpenAI or CopilotVendorType.Claude)) return null;
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
