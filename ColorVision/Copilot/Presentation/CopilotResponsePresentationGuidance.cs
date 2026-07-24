using System;
using System.Globalization;

namespace ColorVision.Copilot
{
    internal static class CopilotResponsePresentationGuidance
    {
        private const string WorkspaceFileLinkInstruction = "When referencing an existing local file that was observed inside the current workspace, format it as a Markdown link such as [FileName.cs](relative/path/FileName.cs:42) so the app can open it. Use an angle-bracket target when an absolute path contains spaces, for example [FileName.cs](<C:/workspace/My Project/FileName.cs:42>). Include a verified line number when available. Never invent a path, link a directory, use file:// links, or link outside the available workspace/search roots. Keep public web citations as HTTP/HTTPS links.";

        public static CopilotProfileConfig CreateRequestProfile(CopilotProfileConfig source)
        {
            var profile = source.Clone();
            var basePrompt = profile.EffectiveSystemPrompt.Trim();
            var requestInstructions = WorkspaceFileLinkInstruction + "\n\n" + BuildResponseLanguageInstruction(CultureInfo.CurrentUICulture);
            profile.UseSystemPromptOverride(string.IsNullOrWhiteSpace(basePrompt)
                ? requestInstructions
                : basePrompt + "\n\n" + requestInstructions);
            return profile;
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
