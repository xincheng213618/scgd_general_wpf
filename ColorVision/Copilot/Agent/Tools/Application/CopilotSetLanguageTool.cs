using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetLanguageTool : ICopilotTool
    {
        public string Name => "SetLanguage";

        public string Description => "按用户要求切换界面语言；input.query 可填写语言或文化名，例如 英文、中文、en-US、zh-Hans。切换时可能提示确认并重启应用。";

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