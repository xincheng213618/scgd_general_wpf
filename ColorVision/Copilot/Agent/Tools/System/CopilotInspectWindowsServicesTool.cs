using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectWindowsServicesTool : ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["query"] = new
                    {
                        type = "string",
                        minLength = 1,
                        maxLength = 256,
                        description = "Optional case-insensitive text contained in the Windows service name or display name.",
                    },
                    ["status"] = new
                    {
                        type = "string",
                        @enum = new[] { "all", "running", "stopped", "paused", "pending" },
                        description = "Optional service status filter. Defaults to all.",
                    },
                    ["sortBy"] = new
                    {
                        type = "string",
                        @enum = new[] { "name", "display_name", "status" },
                        description = "Sort order. Defaults to service name.",
                    },
                    ["limit"] = new
                    {
                        type = "integer",
                        minimum = 1,
                        maximum = CopilotWindowsServiceInspectionService.MaximumResults,
                        description = "Maximum number of service entries to return. Defaults to 20.",
                    },
                },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotWindowsServiceInspectionService _service;

        public CopilotInspectWindowsServicesTool()
            : this(new CopilotWindowsServiceInspectionService())
        {
        }

        public CopilotInspectWindowsServicesTool(CopilotWindowsServiceInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectWindowsServices";

        public string Description => "Inspect installed Windows services using a fixed in-process .NET diagnostic. Optionally filter by text in the exact service/display name or by running, stopped, paused, or pending state. Returns a bounded structured list without accepting command text or requiring approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            executionTimeout: TimeSpan.FromSeconds(10),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.Summary);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => request != null
            && request.Mode != CopilotAgentMode.Chat
            && OperatingSystem.IsWindows();

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }
    }
}
