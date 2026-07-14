using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotWorkspaceChangeSetSchemas
    {
        public static CopilotToolInputSchema Preview { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["previewIds"] = new
                    {
                        type = "array",
                        minItems = 2,
                        maxItems = 8,
                        uniqueItems = true,
                        items = new { type = "string" },
                        description = "Two through eight unused preview_id values returned by PreviewWorkspacePatch or PreviewCreateWorkspaceFile.",
                    },
                },
                ["required"] = new[] { "previewIds" },
                ["additionalProperties"] = false,
            }));

        public static CopilotToolInputSchema ChangeSetId { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["changeSetId"] = new { type = "string", description = "Exact change_set_id returned by PreviewWorkspaceChangeSet." },
                },
                ["required"] = new[] { "changeSetId" },
                ["additionalProperties"] = false,
            }));
    }

    public sealed class CopilotPreviewWorkspaceChangeSetTool : ICopilotTool
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotPreviewWorkspaceChangeSetTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "PreviewWorkspaceChangeSet";

        public string Description => "Bundle 2-8 unused workspace patch or file-creation previews into one guarded multi-file change set. It reserves every preview, binds the complete file list and per-file SHA-256 values, and never writes. Call this before ApplyWorkspaceChangeSet.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.None);

        public CopilotToolInputSchema InputSchema => CopilotWorkspaceChangeSetSchemas.Preview;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceEdit(request)
            || CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.PreviewChangeSetAsync(request, toolInput, cancellationToken);
        }
    }

    public sealed class CopilotApplyWorkspaceChangeSetTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotApplyWorkspaceChangeSetTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "ApplyWorkspaceChangeSet";

        public string Description => "Apply one previously previewed multi-file workspace change set after a single native approval. It validates every path and SHA-256 before the first write and compensates earlier writes if a later operation fails.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspaceChangeSetSchemas.ChangeSetId;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceEdit(request)
            || CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => CopilotWorkspacePatchStore.GetChangeSetConcurrencyKey(toolInput, Name);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ApprovalRequired(Name));
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.ApplyChangeSetAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateChangeSetApprovalPresentation(toolInput, rollback: false);

        private static CopilotToolResult ApprovalRequired(string toolName)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = "Workspace change sets require Microsoft Agent Framework approval.",
                ErrorMessage = "The multi-file write was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            };
        }
    }

    public sealed class CopilotRollbackWorkspaceChangeSetTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotRollbackWorkspaceChangeSetTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "RollbackWorkspaceChangeSet";

        public string Description => "Roll back all files from one applied workspace change set after native approval. It validates the complete applied state before restoring anything and attempts to preserve the applied state if a later rollback operation fails.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspaceChangeSetSchemas.ChangeSetId;

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
                Summary = "Workspace change-set rollbacks require Microsoft Agent Framework approval.",
                ErrorMessage = "The multi-file rollback was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.RollbackChangeSetAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateChangeSetApprovalPresentation(toolInput, rollback: true);
    }
}
