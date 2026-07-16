using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotWorkspacePatchEnvelopeSchemas
    {
        public static CopilotToolInputSchema Preview { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["operations"] = new
                    {
                        type = "array",
                        minItems = 1,
                        maxItems = 8,
                        description = "Complete ordered workspace change envelope. Each path may appear once.",
                        items = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object?>
                            {
                                ["operation"] = new { type = "string", @enum = new[] { "add", "update", "delete" }, description = "Add a new file, update one exact text region, or delete an existing text file." },
                                ["path"] = new { type = "string", description = "Workspace-relative path, or an absolute path inside the current writable workspace scope." },
                                ["oldText"] = new { type = "string", description = "For update only: exact existing text that must match once." },
                                ["newText"] = new { type = "string", description = "For update only: replacement text; may be empty." },
                                ["content"] = new { type = "string", description = "For add only: complete UTF-8 file content." },
                            },
                            required = new[] { "operation", "path" },
                            additionalProperties = false,
                        },
                    },
                },
                ["required"] = new[] { "operations" },
                ["additionalProperties"] = false,
            }));

        public static CopilotToolInputSchema ChangeSetId { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["changeSetId"] = new { type = "string", description = "Exact change_set_id returned by PreviewWorkspacePatchEnvelope." },
                },
                ["required"] = new[] { "changeSetId" },
                ["additionalProperties"] = false,
            }));
    }

    public sealed class CopilotPreviewWorkspacePatchEnvelopeTool : ICopilotTool
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotPreviewWorkspacePatchEnvelopeTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "PreviewWorkspacePatchEnvelope";

        public string Description => "Preview one guarded workspace change envelope containing 1-8 Add, Update, or Delete operations. One call validates every path and exact replacement, binds per-file SHA-256 state, and returns a changeSetId without writing.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.None);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchEnvelopeSchemas.Preview;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceEdit(request)
            || CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.PreviewPatchEnvelopeAsync(request, toolInput, cancellationToken);
        }
    }

    public sealed class CopilotApplyWorkspacePatchEnvelopeTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotApplyWorkspacePatchEnvelopeTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "ApplyWorkspacePatchEnvelope";

        public string Description => "Apply one previously previewed Add/Update/Delete workspace envelope after a single native approval. Every path and SHA-256 is revalidated before the first write, and completed operations are compensated if a later operation fails.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchEnvelopeSchemas.ChangeSetId;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceEdit(request)
            || CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => CopilotWorkspacePatchStore.GetChangeSetConcurrencyKey(toolInput, Name);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Workspace patch envelopes require Microsoft Agent Framework approval.",
                ErrorMessage = "The Add/Update/Delete envelope was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.ApplyPatchEnvelopeAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateChangeSetApprovalPresentation(toolInput, rollback: false);
    }

    public sealed class CopilotRollbackWorkspacePatchEnvelopeTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotRollbackWorkspacePatchEnvelopeTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "RollbackWorkspacePatchEnvelope";

        public string Description => "Roll back every Add, Update, and Delete operation in an applied workspace patch envelope after one fresh native approval and whole-envelope state validation.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchEnvelopeSchemas.ChangeSetId;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceRollback(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => CopilotWorkspacePatchStore.GetChangeSetConcurrencyKey(toolInput, Name);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Workspace patch-envelope rollbacks require Microsoft Agent Framework approval.",
                ErrorMessage = "The envelope rollback was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.RollbackPatchEnvelopeAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateChangeSetApprovalPresentation(toolInput, rollback: true);
    }
}
