using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectWindowsSystemTool : ICopilotAgentDrivenTool
    {
        private readonly CopilotWindowsSystemInspectionService _service;

        public CopilotInspectWindowsSystemTool()
            : this(new CopilotWindowsSystemInspectionService())
        {
        }

        public CopilotInspectWindowsSystemTool(CopilotWindowsSystemInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectWindowsSystem";

        public string Description => "Inspect the current Windows installation using a fixed read-only diagnostic. Returns product name, display version, edition, installation type, OS/build revision, OS and process architecture, and .NET runtime. It accepts no arguments, never runs model-provided command text, and does not require approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            executionTimeout: TimeSpan.FromSeconds(15),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.Summary);

        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => request != null
            && request.Mode != CopilotAgentMode.Chat
            && OperatingSystem.IsWindows()
            && CopilotToolIntentPolicy.NeedsWindowsSystemInspection(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, cancellationToken);
        }
    }
}
