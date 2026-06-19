using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotExecuteMenuTool : ICopilotTool
    {
        public string Name => "ExecuteMenu";

        public string Description => "Execute a main-menu command by menu name or path, such as Options, VAM, Check for Updates, Dark Theme, or English. Put the target menu directly in input.query.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || Application.Current == null)
                return false;

            if (!CopilotApplicationCapability.HasMenuIntent(request.UserText))
                return false;

            return CopilotApplicationCapability.HasMenuCandidates(request.UserText);
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

            var result = await CopilotApplicationCapability.ExecuteMenuAsync(sourceText, cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
