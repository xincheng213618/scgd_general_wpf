using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotShellCommandTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation, ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["command"] = new { type = "string", minLength = 1, maxLength = CopilotShellCommandService.MaximumCommandCharacters, description = "Complete non-interactive command text." },
                    ["shell"] = new { type = "string", @enum = new[] { "auto", "powershell", "cmd" }, description = "Shell to use. auto follows the configured default." },
                    ["workingDirectory"] = new { type = "string", description = "Optional existing working directory. Defaults to the active workspace or application directory." },
                    ["timeoutSeconds"] = new { type = "integer", minimum = 5, maximum = 600, description = "Process timeout in seconds. Defaults to 60." },
                },
                ["required"] = new[] { "command" },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotShellCommandService _service;

        public CopilotShellCommandTool()
            : this(new CopilotShellCommandService())
        {
        }

        public CopilotShellCommandTool(CopilotShellCommandService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "RunShellCommand";

        public string Description => "Run one bounded, non-interactive Windows PowerShell or CMD command and return its real exit code, stdout, and stderr. Use it for custom system inspection, multi-port/process/service diagnostics, developer commands, and user-authorized machine operations. Prefer InspectTcpPort for one specific TCP port. Every invocation requires native approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            executionTimeout: TimeSpan.FromMinutes(10),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => request != null && request.Mode != CopilotAgentMode.Chat;

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => "system:shell";

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Shell command execution requires Microsoft Agent Framework approval.",
                ErrorMessage = "The shell process was requested without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            var command = ReadString(toolInput, "command", "<missing command>");
            var shellText = ReadString(toolInput, "shell", "auto");
            var shellLabel = CopilotShellCommandService.TryParseShell(shellText, out var requestedShell)
                ? requestedShell == CopilotShellKind.Auto
                    ? "Auto (configured Windows shell)"
                    : CopilotShellCommandService.GetShellLabel(requestedShell)
                : shellText;
            var workingDirectory = ReadString(toolInput, "workingDirectory", "<active workspace or application directory>");
            return new CopilotToolApprovalPresentation(
                $"Run {shellLabel} command",
                $"Shell: {shellLabel}\nWorking directory: {workingDirectory}\nCommand:\n{command}");
        }

        private static string ReadString(CopilotAgentToolInput input, string name, string fallback)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return fallback;
            if (raw is string text && !string.IsNullOrWhiteSpace(text))
                return text;
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? fallback;
            return fallback;
        }
    }
}
