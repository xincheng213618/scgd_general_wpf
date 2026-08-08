using System;
using System.Globalization;

namespace ColorVision.Copilot
{
    internal static class CopilotResponsePresentationGuidance
    {
        private const string WorkspaceFileLinkInstruction = "When referencing an existing local file that was observed inside the current workspace, format it as a Markdown link such as [FileName.cs](relative/path/FileName.cs:42) so the app can open it. Use an angle-bracket target when an absolute path contains spaces, for example [FileName.cs](<C:/workspace/My Project/FileName.cs:42>). Include a verified line number when available. Never invent a path, link a directory, use file:// links, or link outside the available workspace/search roots. Keep public web citations as HTTP/HTTPS links.";
        private const string PersonalityBoundaryInstruction = "Treat this only as the default communication style. It must not change the task scope, permissions, safety rules, evidence standards, requested output format, or explicit user instructions.";

        public static CopilotProfileConfig CreateRequestProfile(
            CopilotProfileConfig source,
            CopilotResponsePersonality personality = CopilotResponsePersonality.None,
            string? configuredModelInstructions = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            var profile = source.Clone();
            if (!source.HasSystemPromptOverride)
            {
                var configuredBasePrompt = CopilotConfiguredModelInstructions.Compose(configuredModelInstructions);
                if (configuredBasePrompt.Length > 0)
                    profile.UseSystemPromptOverride(configuredBasePrompt);
            }
            var basePrompt = profile.EffectiveSystemPrompt.Trim();
            var personalityInstruction = BuildPersonalityInstruction(personality);
            var requestInstructions = WorkspaceFileLinkInstruction
                + (personalityInstruction.Length == 0 ? string.Empty : "\n\n" + personalityInstruction)
                + "\n\n"
                + BuildResponseLanguageInstruction(CultureInfo.CurrentUICulture);
            profile.UseSystemPromptOverride(string.IsNullOrWhiteSpace(basePrompt)
                ? requestInstructions
                : basePrompt + "\n\n" + requestInstructions);
            return profile;
        }

        internal static string BuildPersonalityInstruction(CopilotResponsePersonality personality)
        {
            var styleInstruction = CopilotResponsePersonalitySelection.Normalize(personality) switch
            {
                CopilotResponsePersonality.Friendly =>
                    "Use a warm, collaborative communication style while remaining direct and evidence-led. Avoid filler, praise, or forced enthusiasm.",
                CopilotResponsePersonality.Pragmatic =>
                    "Use a pragmatic, outcome-first communication style. Keep responses concise and direct, and mention tradeoffs only when they materially affect the result.",
                _ => string.Empty,
            };
            return styleInstruction.Length == 0
                ? string.Empty
                : "<response_personality>\n"
                    + styleInstruction
                    + "\n"
                    + PersonalityBoundaryInstruction
                    + "\n</response_personality>";
        }

        internal static string BuildResponseLanguageInstruction(CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(culture);
            var language = string.IsNullOrWhiteSpace(culture.Name)
                ? "English (en)"
                : $"{culture.EnglishName} ({culture.Name})";
            return $"Respond in {language} unless the user explicitly requests another language.";
        }
    }
}
