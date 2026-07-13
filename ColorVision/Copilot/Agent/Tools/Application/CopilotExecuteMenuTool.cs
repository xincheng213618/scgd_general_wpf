using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotExecuteMenuTool : ICopilotTool
    {
        private readonly CopilotMcpToolDispatcher _dispatcher;

        public CopilotExecuteMenuTool()
            : this(new CopilotMcpToolDispatcher())
        {
        }

        public CopilotExecuteMenuTool(CopilotMcpToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "ExecuteMenu";

        public string Description => "Execute a main-menu command by menu name or path, such as Options, VAM, Check for Updates, Dark Theme, or English. Put the target menu directly in input.query.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || Application.Current == null)
                return false;

            if (CopilotFlowCreationSupport.HasCreateIntent(request.UserText))
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

            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = JsonSerializer.SerializeToElement(sourceText),
                ["dry_run"] = JsonSerializer.SerializeToElement(false),
            };
            var result = await _dispatcher.CallAsync("execute_menu", arguments, cancellationToken, CopilotMcpToolDispatcher.InAppAgentCallerSource);
            var isWaitingForApproval = string.Equals(result.ErrorCode, "confirmation_required", StringComparison.OrdinalIgnoreCase);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval
                    ? "Menu command is waiting for explicit ColorVision approval."
                    : result.Success ? "Menu command executed." : "Menu command execution failed.",
                Content = result.Text,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Text,
            };
        }
    }
}
