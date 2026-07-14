using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectTcpPortTool : ICopilotTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["port"] = new
                    {
                        type = "integer",
                        minimum = 1,
                        maximum = 65535,
                        description = "TCP port number to inspect on the current Windows machine.",
                    },
                },
                ["required"] = new[] { "port" },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotTcpPortInspectionService _service;

        public CopilotInspectTcpPortTool()
            : this(new CopilotTcpPortInspectionService())
        {
        }

        public CopilotInspectTcpPortTool(CopilotTcpPortInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectTcpPort";

        public string Description => "Inspect one TCP port on the current Windows machine using a fixed read-only diagnostic. Returns whether the port is in use plus bounded local/remote endpoints, connection state, owning PID, and process name. It never accepts command text and does not require approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            executionTimeout: TimeSpan.FromSeconds(45),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.Summary);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsTcpPortInspection(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }
    }
}
