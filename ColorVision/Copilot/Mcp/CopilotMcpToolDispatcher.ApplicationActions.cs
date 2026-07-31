using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private async Task<CopilotMcpToolCallResult> CreateFlowAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            var flowName = CopilotFlowCreationSupport.ResolveFlowName(null, GetString(arguments, "name"));
            var normalizedArguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement(flowName),
            };

            if (IsInAppAgentFrameworkApproved(executionScope))
                return await _environment.CreateFlowHandler(flowName, cancellationToken);

            return CreateConfirmableActionResult(
                "Confirm new flow creation",
                $"Create a new empty ColorVision flow: {flowName}",
                "create_flow",
                normalizedArguments,
                $"Flow name: {flowName}{Environment.NewLine}The flow will be created but will not be opened or executed automatically.",
                token => _environment.CreateFlowHandler(flowName, token),
                executeOnApproval: IsInAppAgent(executionScope),
                executionScope: executionScope);
        }

        private async Task<CopilotMcpToolCallResult> ConfirmActionAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            var actionId = GetString(arguments, "action_id");
            var toolName = NormalizeToolName(GetString(arguments, "tool_name"));
            var argumentsDigest = GetString(arguments, "arguments_digest");

            if (string.IsNullOrWhiteSpace(actionId))
                return CopilotMcpToolCallResult.Fail("missing_action_id", "The confirm_action tool requires action_id.");

            if (string.IsNullOrWhiteSpace(toolName))
                return CopilotMcpToolCallResult.Fail("missing_tool_name", "The confirm_action tool requires tool_name.");

            if (string.IsNullOrWhiteSpace(argumentsDigest))
                return CopilotMcpToolCallResult.Fail("missing_arguments_digest", "The confirm_action tool requires the exact arguments_digest returned with the action.");

            return await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                actionId,
                toolName,
                argumentsDigest,
                executionScope,
                cancellationToken);
        }

        private async Task<CopilotMcpToolCallResult> SetThemeAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var themeQuery = FirstNonEmpty(GetString(arguments, "theme"), GetString(arguments, "query"));
            if (string.IsNullOrWhiteSpace(themeQuery))
                return CopilotMcpToolCallResult.Fail("missing_theme", "The set_theme tool requires a non-empty theme argument.");

            if (_environment.SetThemeHandler != null)
                return await _environment.SetThemeHandler(themeQuery, cancellationToken);

            var result = await CopilotApplicationCapability.SetThemeAsync(themeQuery, cancellationToken);
            return ToMcpResult(result, "theme_change_failed");
        }

        private async Task<CopilotMcpToolCallResult> SetLanguageAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            var languageQuery = FirstNonEmpty(GetString(arguments, "language"), GetString(arguments, "query"));
            if (string.IsNullOrWhiteSpace(languageQuery))
                return CopilotMcpToolCallResult.Fail("missing_language", "The set_language tool requires a non-empty language argument.");

            if (IsInAppAgentFrameworkApproved(executionScope))
            {
                return _environment.SetLanguageHandler != null
                    ? await _environment.SetLanguageHandler(languageQuery, cancellationToken)
                    : ToMcpResult(await CopilotApplicationCapability.SetLanguageAsync(languageQuery, cancellationToken), "language_change_failed");
            }

            if (_environment.SetLanguageHandler != null)
            {
                return CreateConfirmableActionResult(
                    "Confirm language change",
                    $"Change ColorVision UI language: {languageQuery}",
                    "set_language",
                    arguments,
                    "Changing language may affect UI state and can trigger the existing restart confirmation flow.",
                    token => _environment.SetLanguageHandler(languageQuery, token),
                    executeOnApproval: IsInAppAgent(executionScope),
                    executionScope: executionScope);
            }

            return CreateConfirmableActionResult(
                "Confirm language change",
                $"Change ColorVision UI language: {languageQuery}",
                "set_language",
                arguments,
                "Changing language may affect UI state and can trigger the existing restart confirmation flow.",
                async token => ToMcpResult(await CopilotApplicationCapability.SetLanguageAsync(languageQuery, token), "language_change_failed"),
                executeOnApproval: IsInAppAgent(executionScope),
                executionScope: executionScope);
        }
    }
}
