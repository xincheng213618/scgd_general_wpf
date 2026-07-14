using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectWindowsProcessesTool : ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["processId"] = new
                    {
                        type = "integer",
                        minimum = 1,
                        maximum = int.MaxValue,
                        description = "Optional exact Windows process ID.",
                    },
                    ["name"] = new
                    {
                        type = "string",
                        minLength = 1,
                        maxLength = 260,
                        description = "Optional exact process name, with or without the .exe suffix.",
                    },
                    ["sortBy"] = new
                    {
                        type = "string",
                        @enum = new[] { "cpu", "memory", "name", "process_id" },
                        description = "Sort order. cpu uses a short recent sample and is the default.",
                    },
                    ["limit"] = new
                    {
                        type = "integer",
                        minimum = 1,
                        maximum = CopilotWindowsProcessInspectionService.MaximumResults,
                        description = "Maximum number of process entries to return. Defaults to 10.",
                    },
                },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotWindowsProcessInspectionService _service;

        public CopilotInspectWindowsProcessesTool()
            : this(new CopilotWindowsProcessInspectionService())
        {
        }

        public CopilotInspectWindowsProcessesTool(CopilotWindowsProcessInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectWindowsProcesses";

        public string Description => "Inspect running Windows processes using a fixed in-process .NET diagnostic. Filter by an exact PID or process name, or return a bounded list sorted by recent CPU, working-set memory, name, or PID. Returns structured resource, session, start-time, and executable-path data where Windows permits it. It accepts no command text and requires no approval.";

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
