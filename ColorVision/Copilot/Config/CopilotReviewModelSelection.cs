using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotConfiguredModelSelection
    {
        public const int MaximumModelCharacters = 256;

        public static bool TryNormalize(string? value, out string model)
        {
            model = (value ?? string.Empty).Trim();
            return model.Length is > 0 and <= MaximumModelCharacters
                && !model.Any(char.IsControl);
        }
    }

    internal static class CopilotReviewModelSelection
    {
        public const int MaximumModelCharacters = CopilotConfiguredModelSelection.MaximumModelCharacters;

        public static CopilotProfileConfig CreateRequestProfile(
            CopilotProfileConfig source,
            CopilotAgentMode mode,
            CopilotResponsePersonality personality,
            string? configuredModelInstructions,
            string? configuredReviewModel,
            string? configuredModel = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            var profile = CopilotResponsePresentationGuidance.CreateRequestProfile(
                source,
                personality,
                configuredModelInstructions);
            if (CopilotConfiguredModelSelection.TryNormalize(configuredModel, out var model))
            {
                profile.Model = model;
            }
            if (mode == CopilotAgentMode.Review
                && TryNormalize(configuredReviewModel, out var reviewModel))
            {
                profile.Model = reviewModel;
            }
            return profile;
        }

        public static string ResolveEffectiveModel(
            CopilotAgentMode mode,
            string? configuredReviewModel,
            string? fallbackModel,
            string? configuredModel = null)
        {
            return mode == CopilotAgentMode.Review
                && TryNormalize(configuredReviewModel, out var reviewModel)
                    ? reviewModel
                    : CopilotConfiguredModelSelection.TryNormalize(configuredModel, out var model)
                        ? model
                        : (fallbackModel ?? string.Empty).Trim();
        }

        public static bool TryNormalize(string? value, out string model)
        {
            return CopilotConfiguredModelSelection.TryNormalize(value, out model);
        }
    }
}
