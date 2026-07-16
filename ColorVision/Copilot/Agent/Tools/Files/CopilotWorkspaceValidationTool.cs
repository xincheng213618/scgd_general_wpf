using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotWorkspaceValidationTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["task"] = new { type = "string", @enum = new[] { "build", "test" }, description = "Whitelisted validation task." },
                    ["path"] = new { type = "string", description = "Workspace-relative path, or an absolute path to a solution or project file inside the writable workspace." },
                    ["configuration"] = new { type = "string", @enum = new[] { "Debug", "Release" }, description = "Build configuration. Defaults to Debug." },
                    ["platform"] = new { type = "string", @enum = new[] { "x64", "x86", "AnyCPU", "ARM64" }, description = "Optional whitelisted MSBuild platform. Omit to use the project default." },
                    ["timeoutSeconds"] = new { type = "integer", minimum = 10, maximum = 600, description = "Process timeout in seconds. Defaults to 300." },
                },
                ["required"] = new[] { "task", "path" },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotWorkspaceValidationService _service;

        public CopilotWorkspaceValidationTool()
            : this(new CopilotWorkspaceValidationService())
        {
        }

        public CopilotWorkspaceValidationTool(CopilotWorkspaceValidationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "RunWorkspaceValidation";

        public string Description => "Run an approved, bounded dotnet build or dotnet test for a solution/project inside the current workspace, with an optional whitelisted platform. No shell or arbitrary arguments are accepted; nonzero exits are returned as terminal failed validation evidence.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            executionTimeout: TimeSpan.FromMinutes(10),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceValidation(request)
            || CopilotToolIntentPolicy.NeedsWorkspaceEdit(request)
            || CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            return string.IsNullOrWhiteSpace(toolInput.Path) ? "workspace:validation" : "path:" + toolInput.Path;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Workspace validation requires Microsoft Agent Framework approval.",
                ErrorMessage = "The validation process was requested without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            var task = ReadString(toolInput, "task", "validation");
            var configuration = ReadString(toolInput, "configuration", "Debug");
            var platform = ReadString(toolInput, "platform", string.Empty);
            var target = string.IsNullOrWhiteSpace(toolInput.Path) ? "<missing target>" : toolInput.Path;
            var platformText = string.IsNullOrWhiteSpace(platform) ? "the project-default platform" : $"platform {platform}";
            return new CopilotToolApprovalPresentation(
                $"Run dotnet {task}",
                $"Run the whitelisted dotnet {task} task for {target} using {configuration} and {platformText}. Project build targets may execute repository-defined code; no shell or additional command-line arguments are allowed.");
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
