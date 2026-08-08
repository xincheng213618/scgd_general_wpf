using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotAutomaticApprovalDenialSnapshot(
        string DenialId,
        string ConversationId,
        string TaskId,
        string WorkspacePath,
        string ToolName,
        string ArgumentsDigest,
        DateTimeOffset DeniedAtUtc);

    internal sealed class CopilotAutomaticApprovalOverrideStore
    {
        internal const int MaximumRecentDenialsPerConversation = 10;
        internal const int MaximumRetainedConversations = 10;

        private static readonly TimeSpan DenialRetention = TimeSpan.FromHours(24);
        private static readonly TimeSpan RetryOverrideLifetime = TimeSpan.FromMinutes(30);
        private static readonly Lazy<CopilotAutomaticApprovalOverrideStore> LazyShared =
            new(() => new CopilotAutomaticApprovalOverrideStore());
        private readonly object _syncRoot = new();
        private readonly List<CopilotAutomaticApprovalDenialSnapshot> _denials = new();
        private readonly List<GrantedRetryOverride> _grantedOverrides = new();

        internal static CopilotAutomaticApprovalOverrideStore Shared => LazyShared.Value;

        internal void RecordDenial(ConfirmableAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (action.Status != ConfirmableActionStatus.Rejected
                || !string.Equals(action.ApprovalDecisionSource, "automatic-review", StringComparison.Ordinal)
                || !action.ResumesAgentOnApproval
                || action.RequestContext.SourceKind != CopilotApprovalSourceKind.InAppAgent
                || !TryNormalizeIdentity(action, out var denial))
            {
                return;
            }

            lock (_syncRoot)
            {
                PruneNoLock(DateTimeOffset.UtcNow);
                _denials.RemoveAll(item => string.Equals(item.DenialId, denial.DenialId, StringComparison.Ordinal));
                _denials.Add(denial);
                PruneCapacityNoLock(denial.ConversationId);
            }
        }

        internal IReadOnlyList<CopilotAutomaticApprovalDenialSnapshot> GetRecentDenials(
            string? conversationId,
            string? workspacePath)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            if (normalizedConversationId.Length == 0 || normalizedWorkspacePath.Length == 0)
                return Array.Empty<CopilotAutomaticApprovalDenialSnapshot>();

            lock (_syncRoot)
            {
                PruneNoLock(DateTimeOffset.UtcNow);
                return _denials
                    .Where(item => string.Equals(item.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                        && WorkspacePathsMatch(item.WorkspacePath, normalizedWorkspacePath))
                    .OrderByDescending(item => item.DeniedAtUtc)
                    .Take(MaximumRecentDenialsPerConversation)
                    .ToArray();
            }
        }

        internal bool TryAuthorizeOneRetry(
            string? denialId,
            string? conversationId,
            string? workspacePath,
            out CopilotAutomaticApprovalDenialSnapshot denial)
        {
            denial = null!;
            var normalizedDenialId = NormalizeIdentifier(denialId);
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            if (normalizedDenialId.Length == 0
                || normalizedConversationId.Length == 0
                || normalizedWorkspacePath.Length == 0)
            {
                return false;
            }

            lock (_syncRoot)
            {
                var nowUtc = DateTimeOffset.UtcNow;
                PruneNoLock(nowUtc);
                var match = _denials.FirstOrDefault(item =>
                    string.Equals(item.DenialId, normalizedDenialId, StringComparison.Ordinal)
                    && string.Equals(item.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                    && WorkspacePathsMatch(item.WorkspacePath, normalizedWorkspacePath));
                if (match == null)
                    return false;

                _denials.Remove(match);
                _grantedOverrides.RemoveAll(item => string.Equals(
                    item.Denial.DenialId,
                    match.DenialId,
                    StringComparison.Ordinal));
                _grantedOverrides.Add(new GrantedRetryOverride(match, nowUtc.Add(RetryOverrideLifetime)));
                var excessOverrides = _grantedOverrides
                    .Where(item => string.Equals(
                        item.Denial.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .OrderByDescending(item => item.Denial.DeniedAtUtc)
                    .Skip(MaximumRecentDenialsPerConversation)
                    .ToHashSet();
                _grantedOverrides.RemoveAll(excessOverrides.Contains);
                denial = match;
                return true;
            }
        }

        internal bool TryConsume(
            CopilotAgentRequest request,
            ICopilotTool tool,
            ConfirmableAction action)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(action);
            if (action.Status != ConfirmableActionStatus.Pending
                || !action.ResumesAgentOnApproval
                || action.RequestContext.SourceKind != CopilotApprovalSourceKind.InAppAgent
                || !string.Equals(action.ToolName, tool.Name, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(action.RequestContext.ConversationId, request.ConversationId, StringComparison.Ordinal)
                || !string.Equals(action.RequestContext.TaskId, request.TaskId, StringComparison.Ordinal)
                || !WorkspacePathsMatch(action.RequestContext.WorkspacePath, request.WorkspacePath))
            {
                return false;
            }

            var conversationId = NormalizeIdentifier(request.ConversationId);
            var workspacePath = NormalizeWorkspacePath(request.WorkspacePath);
            var toolName = NormalizeIdentifier(tool.Name);
            var argumentsDigest = NormalizeDigest(action.ArgumentsDigest);
            if (conversationId.Length == 0
                || workspacePath.Length == 0
                || toolName.Length == 0
                || argumentsDigest.Length == 0)
            {
                return false;
            }

            lock (_syncRoot)
            {
                PruneNoLock(DateTimeOffset.UtcNow);
                var match = _grantedOverrides
                    .OrderBy(item => item.Denial.DeniedAtUtc)
                    .FirstOrDefault(item =>
                        string.Equals(item.Denial.ConversationId, conversationId, StringComparison.Ordinal)
                        && WorkspacePathsMatch(item.Denial.WorkspacePath, workspacePath)
                        && string.Equals(item.Denial.ToolName, toolName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.Denial.ArgumentsDigest, argumentsDigest, StringComparison.Ordinal));
                if (match == null || !action.TryMarkAutomaticReviewRetryOverride())
                    return false;

                _grantedOverrides.Remove(match);
                return true;
            }
        }

        private static bool TryNormalizeIdentity(
            ConfirmableAction action,
            out CopilotAutomaticApprovalDenialSnapshot denial)
        {
            var context = action.RequestContext;
            var denialId = NormalizeIdentifier(action.ActionId);
            var conversationId = NormalizeIdentifier(context.ConversationId);
            var taskId = NormalizeIdentifier(context.TaskId);
            var workspacePath = NormalizeWorkspacePath(context.WorkspacePath);
            var toolName = NormalizeIdentifier(action.ToolName);
            var argumentsDigest = NormalizeDigest(action.ArgumentsDigest);
            if (denialId.Length == 0
                || conversationId.Length == 0
                || taskId.Length == 0
                || workspacePath.Length == 0
                || toolName.Length == 0
                || argumentsDigest.Length == 0)
            {
                denial = null!;
                return false;
            }

            denial = new CopilotAutomaticApprovalDenialSnapshot(
                denialId,
                conversationId,
                taskId,
                workspacePath,
                toolName,
                argumentsDigest,
                action.CompletedAt ?? DateTimeOffset.UtcNow);
            return true;
        }

        private void PruneNoLock(DateTimeOffset nowUtc)
        {
            _denials.RemoveAll(item => nowUtc - item.DeniedAtUtc > DenialRetention);
            _grantedOverrides.RemoveAll(item => item.ExpiresAtUtc <= nowUtc);
        }

        private void PruneCapacityNoLock(string conversationId)
        {
            var excessForConversation = _denials
                .Where(item => string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal))
                .OrderByDescending(item => item.DeniedAtUtc)
                .Skip(MaximumRecentDenialsPerConversation)
                .ToHashSet();
            _denials.RemoveAll(excessForConversation.Contains);

            var retainedConversationIds = _denials
                .Select(item => (item.ConversationId, TimestampUtc: item.DeniedAtUtc))
                .Concat(_grantedOverrides.Select(item => (
                    item.Denial.ConversationId,
                    TimestampUtc: item.Denial.DeniedAtUtc)))
                .GroupBy(item => item.ConversationId, StringComparer.Ordinal)
                .OrderByDescending(group => group.Max(item => item.TimestampUtc))
                .Take(MaximumRetainedConversations)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            _denials.RemoveAll(item => !retainedConversationIds.Contains(item.ConversationId));
            _grantedOverrides.RemoveAll(item => !retainedConversationIds.Contains(item.Denial.ConversationId));
        }

        private static string NormalizeIdentifier(string? value) => (value ?? string.Empty).Trim();

        private static string NormalizeDigest(string? value)
        {
            var normalized = NormalizeIdentifier(value).ToLowerInvariant();
            return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
                ? normalized
                : string.Empty;
        }

        private static string NormalizeWorkspacePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value.Trim()));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool WorkspacePathsMatch(string? expected, string? actual)
        {
            var normalizedExpected = NormalizeWorkspacePath(expected);
            var normalizedActual = NormalizeWorkspacePath(actual);
            return normalizedExpected.Length > 0
                && string.Equals(normalizedExpected, normalizedActual, StringComparison.OrdinalIgnoreCase);
        }

        private sealed record GrantedRetryOverride(
            CopilotAutomaticApprovalDenialSnapshot Denial,
            DateTimeOffset ExpiresAtUtc);
    }
}
