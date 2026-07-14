using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotWorkspacePatchSchemas
    {
        public static CopilotToolInputSchema Preview { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["path"] = new { type = "string", description = "Absolute path of an existing text file inside the writable workspace scope." },
                    ["oldText"] = new { type = "string", description = "Exact existing text to replace. Include enough surrounding text to match exactly once." },
                    ["newText"] = new { type = "string", description = "Replacement text. May be empty to delete the matched region." },
                },
                ["required"] = new[] { "path", "oldText", "newText" },
                ["additionalProperties"] = false,
            }));

        public static CopilotToolInputSchema PreviewId { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["previewId"] = new { type = "string", description = "Exact preview_id returned by PreviewWorkspacePatch or PreviewCreateWorkspaceFile." },
                },
                ["required"] = new[] { "previewId" },
                ["additionalProperties"] = false,
            }));

        public static CopilotToolInputSchema Create { get; } = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["path"] = new { type = "string", description = "Absolute path for a new text file inside a writable workspace root." },
                    ["content"] = new { type = "string", description = "Complete UTF-8 text content for the new file." },
                },
                ["required"] = new[] { "path", "content" },
                ["additionalProperties"] = false,
            }));
    }

    public sealed class CopilotPreviewCreateWorkspaceFileTool : ICopilotTool
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotPreviewCreateWorkspaceFileTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "PreviewCreateWorkspaceFile";

        public string Description => "Preview creation of one new UTF-8 text file inside the writable workspace. It validates the path and returns a short-lived preview_id without writing. Call this before ApplyCreateWorkspaceFile.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.None);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchSchemas.Create;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.PreviewCreateAsync(request, toolInput, cancellationToken);
        }
    }

    public sealed class CopilotApplyCreateWorkspaceFileTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotApplyCreateWorkspaceFileTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "ApplyCreateWorkspaceFile";

        public string Description => "Create a file from PreviewCreateWorkspaceFile. Requires native approval and refuses to overwrite a path created after preview.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchSchemas.PreviewId;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceCreate(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => _store.GetConcurrencyKey(toolInput, Name);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Workspace file creation requires Microsoft Agent Framework approval.",
                ErrorMessage = "The tool was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.ApplyCreateAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateApprovalPresentation(toolInput, rollback: false);
    }

    public sealed class CopilotPreviewWorkspacePatchTool : ICopilotTool
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotPreviewWorkspacePatchTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "PreviewWorkspacePatch";

        public string Description => "Preview one exact text replacement in an existing workspace file. The preview binds the current file SHA-256 and returns a short-lived preview_id; it never writes. Call this before ApplyWorkspacePatch.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.None);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchSchemas.Preview;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceEdit(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.PreviewAsync(request, toolInput, cancellationToken);
        }
    }

    public sealed class CopilotApplyWorkspacePatchTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotApplyWorkspacePatchTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "ApplyWorkspacePatch";

        public string Description => "Apply a previously generated workspace patch preview. Requires native user approval and writes only when the file SHA-256 still matches the preview.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchSchemas.PreviewId;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceEdit(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => _store.GetConcurrencyKey(toolInput, Name);

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
            return _store.ApplyAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateApprovalPresentation(toolInput, rollback: false);

        private static CopilotToolResult ApprovalRequired(string toolName)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = "Workspace writes require Microsoft Agent Framework approval.",
                ErrorMessage = "The tool was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            };
        }
    }

    public sealed class CopilotRollbackWorkspacePatchTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly CopilotWorkspacePatchStore _store;

        public CopilotRollbackWorkspacePatchTool(CopilotWorkspacePatchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public string Name => "RollbackWorkspacePatch";

        public string Description => "Roll back an applied workspace patch or Agent-created file using its preview_id. Requires native approval and changes state only when the current file still matches the applied SHA-256.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => CopilotWorkspacePatchSchemas.PreviewId;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsWorkspaceRollback(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => _store.GetConcurrencyKey(toolInput, Name);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Workspace rollbacks require Microsoft Agent Framework approval.",
                ErrorMessage = "The tool was invoked without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _store.RollbackAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput) => _store.CreateApprovalPresentation(toolInput, rollback: true);
    }
}
