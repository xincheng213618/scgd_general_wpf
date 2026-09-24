using System;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    [Flags]
    internal enum CopilotAgentHarnessFeatures
    {
        None = 0,
        TaskLedger = 1,
        AgentMode = 2,
        Skills = 4,
        Full = TaskLedger | AgentMode | Skills,
    }

    internal enum CopilotAgentRuntimePurpose
    {
        Standard,
        DelegatedEvidenceFinalization,
    }

    public enum CopilotAgentMode
    {
        Chat,
        Auto,
        Explain,
        Web,
        Code,
        Review,
        Diagnose,
        Plan,
    }

    public enum CopilotAgentAccessMode
    {
        ConfirmProtectedActions,
        FullAccess,
        UnrestrictedFullAccess,
    }

    public sealed class CopilotAgentAccessContext
    {
        internal static readonly TimeSpan MaximumFullAccessLifetime = TimeSpan.FromMinutes(15);
        private readonly object _syncRoot = new();
        private FullAccessGrant? _grant;

        public CopilotAgentAccessMode Mode
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                        ? _grant!.Mode
                        : CopilotAgentAccessMode.ConfirmProtectedActions;
            }
        }

        public bool IsPreparedForNextTask
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                        && string.IsNullOrWhiteSpace(_grant!.TaskId);
            }
        }

        public string GrantedTaskId
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow) ? _grant!.TaskId : string.Empty;
            }
        }

        public string WorkspacePath
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow) ? _grant!.WorkspacePath : string.Empty;
            }
        }

        public DateTimeOffset? ExpiresAtUtc
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow) && _grant!.Mode != CopilotAgentAccessMode.UnrestrictedFullAccess
                        ? _grant!.ExpiresAtUtc : null;
            }
        }

        public bool AllowsUnattendedProtectedActions
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                        && !string.IsNullOrWhiteSpace(_grant!.TaskId);
            }
        }

        internal void PrepareFullAccess(
            string conversationId,
            string workspacePath,
            string? taskId,
            DateTimeOffset expiresAtUtc)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedTaskId = NormalizeIdentifier(taskId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            var now = DateTimeOffset.UtcNow;
            var normalizedExpiresAtUtc = expiresAtUtc.ToUniversalTime();
            if (normalizedExpiresAtUtc > now.Add(MaximumFullAccessLifetime))
                normalizedExpiresAtUtc = now.Add(MaximumFullAccessLifetime);
            lock (_syncRoot)
            {
                _grant = string.IsNullOrWhiteSpace(normalizedConversationId)
                    || string.IsNullOrWhiteSpace(normalizedWorkspacePath)
                    || normalizedExpiresAtUtc <= now
                    ? null
                    : new FullAccessGrant(
                        normalizedConversationId,
                        normalizedTaskId,
                        normalizedWorkspacePath,
                        normalizedExpiresAtUtc);
            }
        }

        internal bool BindToTask(string conversationId, string taskId, string workspacePath)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedTaskId = NormalizeIdentifier(taskId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                if (!IsGrantCurrentNoLock(DateTimeOffset.UtcNow))
                {
                    _grant = null;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(normalizedTaskId)
                    || !string.Equals(_grant!.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                    || !WorkspaceMatches(_grant.WorkspacePath, normalizedWorkspacePath))
                {
                    _grant = null;
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_grant.TaskId))
                    return string.Equals(_grant.TaskId, normalizedTaskId, StringComparison.Ordinal);

                _grant = _grant with { TaskId = normalizedTaskId };
                return true;
            }
        }

        internal void PrepareUnrestrictedFullAccess(string conversationId, string workspacePath, string? taskId)
        {
            var conversation = NormalizeIdentifier(conversationId);
            var workspace = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                _grant = string.IsNullOrWhiteSpace(conversation)
                    ? null
                    : new FullAccessGrant(conversation, NormalizeIdentifier(taskId), workspace,
                        DateTimeOffset.MaxValue, CopilotAgentAccessMode.UnrestrictedFullAccess);
            }
        }

        internal bool EndTask(string? taskId)
        {
            lock (_syncRoot)
            {
                if (_grant == null || !string.Equals(_grant.TaskId, NormalizeIdentifier(taskId), StringComparison.Ordinal))
                    return false;
                if (_grant.Mode == CopilotAgentAccessMode.UnrestrictedFullAccess)
                    _grant = _grant with { TaskId = string.Empty };
                else
                    _grant = null;
                return true;
            }
        }

        internal bool Revoke(string? taskId = null)
        {
            var normalizedTaskId = NormalizeIdentifier(taskId);
            lock (_syncRoot)
            {
                if (_grant == null
                    || (!string.IsNullOrWhiteSpace(normalizedTaskId)
                        && !string.Equals(_grant.TaskId, normalizedTaskId, StringComparison.Ordinal)))
                {
                    return false;
                }

                _grant = null;
                return true;
            }
        }

        internal bool ExpireIfNeeded()
        {
            lock (_syncRoot)
            {
                if (_grant == null || _grant.ExpiresAtUtc > DateTimeOffset.UtcNow)
                    return false;

                _grant = null;
                return true;
            }
        }

        internal bool AllowsUnattendedProtectedActionsFor(
            string conversationId,
            string taskId,
            string workspacePath)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedTaskId = NormalizeIdentifier(taskId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                    && !string.IsNullOrWhiteSpace(normalizedTaskId)
                    && string.Equals(_grant!.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                    && string.Equals(_grant.TaskId, normalizedTaskId, StringComparison.Ordinal)
                    && WorkspaceMatches(_grant.WorkspacePath, normalizedWorkspacePath);
            }
        }

        internal bool RevokeIfWorkspaceChanged(string workspacePath)
        {
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                if (!IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                    || WorkspaceMatches(_grant!.WorkspacePath, normalizedWorkspacePath))
                {
                    return false;
                }

                _grant = null;
                return true;
            }
        }

        private bool IsGrantCurrentNoLock(DateTimeOffset now)
        {
            return _grant != null && _grant.ExpiresAtUtc > now;
        }

        private static bool WorkspaceMatches(string grantedPath, string requestPath)
        {
            return string.Equals(grantedPath, requestPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeIdentifier(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length <= 160 ? normalized : normalized[..160];
        }

        private static string NormalizeWorkspacePath(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            try
            {
                return Path.GetFullPath(normalized)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalized.Length <= 1000 ? normalized : normalized[..1000];
            }
        }

        private sealed record FullAccessGrant(
            string ConversationId,
            string TaskId,
            string WorkspacePath,
            DateTimeOffset ExpiresAtUtc,
            CopilotAgentAccessMode Mode = CopilotAgentAccessMode.FullAccess);
    }

    internal static class CopilotAgentAccessPolicy
    {
        public static bool CanAutoApprove(
            CopilotAgentRequest request,
            ICopilotTool tool,
            string currentWorkspacePath)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            if (request.AccessContext.RevokeIfWorkspaceChanged(currentWorkspacePath))
                return false;
            if (request.AccessContext.Mode == CopilotAgentAccessMode.UnrestrictedFullAccess)
            {
                var noWorkspace = string.IsNullOrWhiteSpace(currentWorkspacePath) && string.IsNullOrWhiteSpace(request.WorkspacePath);
                return (noWorkspace || WorkspacePathsMatch(request.WorkspacePath, currentWorkspacePath))
                    && request.AccessContext.AllowsUnattendedProtectedActionsFor(request.ConversationId, request.TaskId, currentWorkspacePath)
                    && !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                    && tool.Capability.RequiresNativeApproval
                    && (noWorkspace
                        ? !request.WritableLocalRootPaths.Any() && !request.WritableLocalFilePaths.Any()
                        : IsWriteScopeContainedByWorkspace(request, currentWorkspacePath));
            }
            if (string.IsNullOrWhiteSpace(currentWorkspacePath)
                || !WorkspacePathsMatch(request.WorkspacePath, currentWorkspacePath))
            {
                return false;
            }

            return request.AccessContext.AllowsUnattendedProtectedActionsFor(
                    request.ConversationId,
                    request.TaskId,
                    currentWorkspacePath)
                && !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                && tool.Capability.RequiresNativeApproval
                && tool.Capability.AllowsTemporaryFullAccess
                && IsWriteScopeContainedByWorkspace(request, currentWorkspacePath);
        }

        public static bool CanAutoReview(
            CopilotAgentRequest request,
            ICopilotTool tool,
            string currentWorkspacePath,
            CopilotApprovalPromptCategory? approvalPromptCategory = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            if (request.AccessContext.Mode == CopilotAgentAccessMode.UnrestrictedFullAccess)
                return false;
            var revokedMismatchedGrant = request.AccessContext.RevokeIfWorkspaceChanged(
                currentWorkspacePath);
            if (string.IsNullOrWhiteSpace(currentWorkspacePath)
                || !WorkspacePathsMatch(request.WorkspacePath, currentWorkspacePath))
            {
                return false;
            }

            if (!request.CodexGuardianApprovalEnabled)
                return false;
            if (request.CodexApprovalsReviewer == CopilotCodexApprovalsReviewer.User)
                return false;
            if (request.CodexApprovalsReviewer == CopilotCodexApprovalsReviewer.AutoReview)
            {
                return CopilotCodexApprovalPolicySelection.AllowsAutomaticReview(
                        request.CodexApprovalPolicy,
                        approvalPromptCategory ?? tool.Capability.ApprovalPromptCategory)
                    && !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                    && tool.Capability.RequiresNativeApproval;
            }
            if (revokedMismatchedGrant)
                return false;

            return request.AccessContext.AllowsUnattendedProtectedActionsFor(
                    request.ConversationId,
                    request.TaskId,
                    currentWorkspacePath)
                && !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                && tool.Capability.RequiresNativeApproval
                && !tool.Capability.AllowsTemporaryFullAccess;
        }

        private static bool IsWriteScopeContainedByWorkspace(
            CopilotAgentRequest request,
            string workspacePath)
        {
            var writablePaths = request.WritableLocalRootPaths
                .Concat(request.WritableLocalFilePaths)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            if (string.IsNullOrWhiteSpace(workspacePath))
                return false;
            if (writablePaths.Length == 0)
                return true;

            return writablePaths.All(path =>
                IsPathContainedByWorkspace(path, workspacePath));
        }

        private static bool WorkspacePathsMatch(string requestPath, string currentPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath) || string.IsNullOrWhiteSpace(currentPath))
                return false;

            try
            {
                return string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(requestPath)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentPath)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPathContainedByWorkspace(string path, string workspacePath)
        {
            string candidate;
            string workspace;
            try
            {
                candidate = Path.GetFullPath(path);
                workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
                var relativePath = Path.GetRelativePath(workspace, candidate);
                if (Path.IsPathRooted(relativePath)
                    || string.Equals(relativePath, "..", StringComparison.Ordinal)
                    || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            var existingPath = candidate;
            while (!File.Exists(existingPath)
                && !Directory.Exists(existingPath)
                && !string.Equals(existingPath, workspace, StringComparison.OrdinalIgnoreCase))
            {
                var parentPath = Path.GetDirectoryName(existingPath);
                if (string.IsNullOrWhiteSpace(parentPath)
                    || string.Equals(parentPath, existingPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                existingPath = parentPath;
            }

            return CopilotWorkspaceSearchSupport.IsPathWithinRoots(existingPath, [workspace]);
        }
    }

}
