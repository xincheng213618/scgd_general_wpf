using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetThemeTool : ICopilotTool
    {
        public string Name => "SetTheme";

        public string Description => "Switch the application theme requested by the user. input.query can contain a target theme such as default, dark, light, pink, or cyan.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotApplicationCapability.HasThemeIntent(request.UserText);
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

            var result = await CopilotApplicationCapability.SetThemeAsync(sourceText, cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
