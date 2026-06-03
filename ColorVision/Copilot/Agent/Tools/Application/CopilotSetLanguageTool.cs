using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetLanguageTool : ICopilotTool
    {
        public string Name => "SetLanguage";

        public string Description => "Switch the UI language requested by the user. input.query can contain a language or culture name such as English, Chinese, en-US, or zh-Hans. The change may ask for confirmation and restart the application.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotApplicationCapability.HasLanguageIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var sourceText = string.IsNullOrWhiteSpace(toolInput?.Query)
                ? request.UserText
                : toolInput.Query;

            var result = await CopilotApplicationCapability.SetLanguageAsync(sourceText, cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
