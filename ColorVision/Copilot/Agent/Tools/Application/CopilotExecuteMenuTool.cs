using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotExecuteMenuTool : ICopilotTool
    {
        public string Name => "ExecuteMenu";

        public string Description => "按菜单名称或菜单路径执行主菜单命令，例如 选项、VAM、检查更新、深色主题、英文。input.query 直接填写目标菜单即可。";

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