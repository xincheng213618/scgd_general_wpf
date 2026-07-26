using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotExecutionSourceKind
    {
        Unknown,
        InProcess,
        ExternalMcp,
        InAppAgent,
    }

    internal enum CopilotExecutionAuthorizationChannel
    {
        Standard,
        AgentFrameworkApproved,
    }

    /// <summary>
    /// Immutable identity carried across one Copilot execution path.
    /// Authorization identity deliberately excludes trace and parent-run metadata.
    /// </summary>
    internal sealed class CopilotExecutionScope
    {
        private const string FingerprintVersion = "copilot-scope-v1";

        private CopilotExecutionScope(
            CopilotExecutionSourceKind sourceKind,
            CopilotExecutionAuthorizationChannel authorizationChannel,
            string sessionIdentity,
            string conversationId,
            string taskId,
            string runId,
            string parentRunId,
            string callerIdentity,
            string workspacePath,
            string workspaceSnapshotId,
            string traceId,
            long capabilityRevision,
            string toolName,
            string providerCallId,
            string argumentsSignature)
        {
            SourceKind = Enum.IsDefined(sourceKind) ? sourceKind : CopilotExecutionSourceKind.Unknown;
            AuthorizationChannel = Enum.IsDefined(authorizationChannel)
                ? authorizationChannel
                : CopilotExecutionAuthorizationChannel.Standard;
            SessionIdentity = NormalizeIdentifier(sessionIdentity);
            ConversationId = NormalizeIdentifier(conversationId);
            TaskId = NormalizeIdentifier(taskId);
            RunId = NormalizeIdentifier(runId);
            ParentRunId = NormalizeIdentifier(parentRunId);
            CallerIdentity = NormalizeIdentifier(callerIdentity);
            WorkspacePath = NormalizeWorkspacePath(workspacePath);
            WorkspaceIdentity = CreateWorkspaceIdentity(WorkspacePath);
            WorkspaceSnapshotId = NormalizeIdentifier(workspaceSnapshotId);
            CapabilityRevision = Math.Max(0, capabilityRevision);
            ToolName = NormalizeIdentifier(toolName);
            ProviderCallId = NormalizeIdentifier(providerCallId);
            ArgumentsSignature = NormalizeIdentifier(argumentsSignature);

            AuthorizationScopeId = CreateFingerprint(
                "authorization",
                ((int)SourceKind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ((int)AuthorizationChannel).ToString(System.Globalization.CultureInfo.InvariantCulture),
                SessionIdentity,
                CallerIdentity,
                ConversationId,
                TaskId,
                RunId,
                WorkspaceIdentity,
                CapabilityRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
            TraceId = NormalizeIdentifier(traceId);
            if (TraceId.Length == 0)
                TraceId = "trace:" + AuthorizationScopeId[..24];
            OperationBindingId = CreateFingerprint(
                "operation",
                AuthorizationScopeId,
                ToolName,
                ProviderCallId,
                ArgumentsSignature);
            ScopeId = CreateFingerprint(
                "scope",
                OperationBindingId,
                ParentRunId,
                WorkspaceSnapshotId,
                TraceId);
        }

        public static CopilotExecutionScope Empty { get; } = new(
            CopilotExecutionSourceKind.Unknown,
            CopilotExecutionAuthorizationChannel.Standard,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty);

        public CopilotExecutionSourceKind SourceKind { get; }

        public CopilotExecutionAuthorizationChannel AuthorizationChannel { get; }

        /// <summary>
        /// One-way session identity. The MCP session token is never stored here.
        /// </summary>
        public string SessionIdentity { get; }

        public string ConversationId { get; }

        public string TaskId { get; }

        public string RunId { get; }

        public string ParentRunId { get; }

        public string CallerIdentity { get; }

        public string WorkspacePath { get; }

        public string WorkspaceIdentity { get; }

        public string WorkspaceSnapshotId { get; }

        public string TraceId { get; }

        public long CapabilityRevision { get; }

        public string ToolName { get; }

        public string ProviderCallId { get; }

        public string ArgumentsSignature { get; }

        public string AuthorizationScopeId { get; }

        public string OperationBindingId { get; }

        public string ScopeId { get; }

        public bool IsEmpty => SourceKind == CopilotExecutionSourceKind.Unknown
            && SessionIdentity.Length == 0
            && ConversationId.Length == 0
            && TaskId.Length == 0
            && RunId.Length == 0
            && CallerIdentity.Length == 0
            && WorkspacePath.Length == 0;

        public bool HasToolCallBinding => ToolName.Length > 0
            && ProviderCallId.Length > 0
            && ArgumentsSignature.Length > 0;

        public static CopilotExecutionScope ForAgentRequest(
            CopilotAgentRequest request,
            string? runId = null,
            string? parentRunId = null,
            string? traceId = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (runId == null
                && parentRunId == null
                && traceId == null
                && !request.RuntimeExecutionScope.IsEmpty)
            {
                return request.RuntimeExecutionScope;
            }

            var normalizedRunId = FirstNonEmpty(runId, request.TaskId);
            return new CopilotExecutionScope(
                CopilotExecutionSourceKind.InAppAgent,
                CopilotExecutionAuthorizationChannel.Standard,
                string.Empty,
                request.ConversationId,
                request.TaskId,
                normalizedRunId,
                parentRunId ?? string.Empty,
                "in-app-agent",
                request.WorkspacePath,
                string.Empty,
                traceId ?? string.Empty,
                0,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        internal static CopilotExecutionScope ForAgentRun(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.RuntimeExecutionScope.IsEmpty
                && CopilotAgentTaskEventIds.IsKey(request.RuntimeExecutionScope.RunId, "run", 32))
            {
                return request.RuntimeExecutionScope;
            }

            var taskRunId = NormalizeIdentifier(request.TaskId);
            var runId = CopilotAgentTaskEventIds.IsKey(taskRunId, "run", 32)
                ? taskRunId
                : CopilotAgentTaskEventIds.CreateRunId();
            return ForAgentRequest(request, runId);
        }

        public static CopilotExecutionScope ForExternalMcpSession(
            string sessionId,
            string callerIdentity,
            string? workspacePath = null)
        {
            var normalizedSessionId = NormalizeIdentifier(sessionId);
            var sessionIdentity = normalizedSessionId.Length == 0
                ? string.Empty
                : "mcp:" + CreateFingerprint("session", normalizedSessionId);
            return new CopilotExecutionScope(
                CopilotExecutionSourceKind.ExternalMcp,
                CopilotExecutionAuthorizationChannel.Standard,
                sessionIdentity,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                callerIdentity,
                workspacePath ?? string.Empty,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public static CopilotExecutionScope ForInProcess(
            string callerIdentity,
            string? workspacePath = null)
        {
            return new CopilotExecutionScope(
                CopilotExecutionSourceKind.InProcess,
                CopilotExecutionAuthorizationChannel.Standard,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                callerIdentity,
                workspacePath ?? string.Empty,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public static CopilotExecutionScope ForAgentCaller(
            string? workspacePath = null,
            CopilotExecutionAuthorizationChannel authorizationChannel = CopilotExecutionAuthorizationChannel.Standard)
        {
            return new CopilotExecutionScope(
                CopilotExecutionSourceKind.InAppAgent,
                authorizationChannel,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "in-app-agent",
                workspacePath ?? string.Empty,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public CopilotExecutionScope WithWorkspace(string? workspacePath)
        {
            return Copy(workspacePath: workspacePath ?? string.Empty);
        }

        public CopilotExecutionScope WithRuntimeSnapshot(
            string? workspaceSnapshotId,
            long capabilityRevision)
        {
            return Copy(
                workspaceSnapshotId: workspaceSnapshotId ?? string.Empty,
                capabilityRevision: capabilityRevision);
        }

        public CopilotExecutionScope WithAuthorizationChannel(
            CopilotExecutionAuthorizationChannel authorizationChannel)
        {
            return Copy(authorizationChannel: authorizationChannel);
        }

        public CopilotExecutionScope BindToolCall(
            string toolName,
            string providerCallId,
            string argumentsSignature)
        {
            return Copy(
                toolName: toolName,
                providerCallId: providerCallId,
                argumentsSignature: argumentsSignature);
        }

        public CopilotExecutionScope DeriveChild(string runId)
        {
            var childRunId = NormalizeIdentifier(runId);
            if (childRunId.Length == 0)
                throw new ArgumentException("A child run id is required.", nameof(runId));

            return new CopilotExecutionScope(
                SourceKind,
                AuthorizationChannel,
                SessionIdentity,
                ConversationId,
                TaskId,
                childRunId,
                RunId,
                CallerIdentity,
                WorkspacePath,
                WorkspaceSnapshotId,
                TraceId,
                CapabilityRevision,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public bool MatchesAuthorizationScope(CopilotExecutionScope? other)
        {
            return other != null
                && FixedTimeFingerprintEquals(AuthorizationScopeId, other.AuthorizationScopeId);
        }

        public bool MatchesOperationBinding(CopilotExecutionScope? other)
        {
            return other != null
                && HasToolCallBinding
                && other.HasToolCallBinding
                && FixedTimeFingerprintEquals(OperationBindingId, other.OperationBindingId);
        }

        private CopilotExecutionScope Copy(
            CopilotExecutionAuthorizationChannel? authorizationChannel = null,
            string? workspacePath = null,
            string? workspaceSnapshotId = null,
            long? capabilityRevision = null,
            string? toolName = null,
            string? providerCallId = null,
            string? argumentsSignature = null)
        {
            return new CopilotExecutionScope(
                SourceKind,
                authorizationChannel ?? AuthorizationChannel,
                SessionIdentity,
                ConversationId,
                TaskId,
                RunId,
                ParentRunId,
                CallerIdentity,
                workspacePath ?? WorkspacePath,
                workspaceSnapshotId ?? WorkspaceSnapshotId,
                TraceId,
                capabilityRevision ?? CapabilityRevision,
                toolName ?? ToolName,
                providerCallId ?? ProviderCallId,
                argumentsSignature ?? ArgumentsSignature);
        }

        private static string NormalizeIdentifier(string? value)
        {
            var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= 512 ? normalized : normalized[..512];
        }

        private static string NormalizeWorkspacePath(string? value)
        {
            var normalized = NormalizeIdentifier(value);
            if (normalized.Length == 0)
                return string.Empty;

            try
            {
                var fullPath = Path.GetFullPath(normalized);
                var root = Path.GetPathRoot(fullPath) ?? string.Empty;
                return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                    ? fullPath
                    : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static string CreateWorkspaceIdentity(string workspacePath)
        {
            return workspacePath.Length == 0
                ? string.Empty
                : "workspace:" + CreateFingerprint("workspace", workspacePath.ToUpperInvariant());
        }

        private static string CreateFingerprint(string purpose, params string[] values)
        {
            var builder = new StringBuilder(FingerprintVersion);
            AppendCanonical(builder, purpose);
            foreach (var value in values)
                AppendCanonical(builder, value);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        }

        private static void AppendCanonical(StringBuilder builder, string? value)
        {
            var normalized = value ?? string.Empty;
            builder.Append('|')
                .Append(normalized.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(':')
                .Append(normalized);
        }

        private static bool FixedTimeFingerprintEquals(string left, string right)
        {
            return left.Length == right.Length
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(left),
                    Encoding.ASCII.GetBytes(right));
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                var normalized = NormalizeIdentifier(value);
                if (normalized.Length > 0)
                    return normalized;
            }
            return string.Empty;
        }
    }
}
