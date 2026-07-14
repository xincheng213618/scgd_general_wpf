using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotQueryFlowExecutionStatsTool : ICopilotTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["period"] = new
                    {
                        type = "string",
                        @enum = new[] { "today", "yesterday", "last7days" },
                        description = "Local-time reporting period. Defaults to today.",
                    },
                },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotFlowExecutionStatisticsService _service;

        public CopilotQueryFlowExecutionStatsTool()
            : this(new CopilotFlowExecutionStatisticsService())
        {
        }

        public CopilotQueryFlowExecutionStatsTool(CopilotFlowExecutionStatisticsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "QueryFlowExecutionStats";

        public string Description => "Query read-only aggregate flow execution counts, statuses, completion rate, and average duration for today, yesterday, or the last seven local calendar days. This tool never accepts SQL or database credentials.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            executionTimeout: TimeSpan.FromSeconds(15),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.Summary);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowExecutionStatistics(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return _service.ExecuteAsync(toolInput, cancellationToken);
        }
    }
}
