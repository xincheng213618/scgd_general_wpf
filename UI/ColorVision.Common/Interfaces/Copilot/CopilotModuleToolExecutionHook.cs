using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public enum CopilotModuleToolExecutionHookMode
    {
        Sync,
        Async,
    }

    public sealed class CopilotModuleToolExecutionHookContext
    {
        public string CallId { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public CopilotModuleToolAccess Access { get; init; }

        public CopilotModuleAgentMode Mode { get; init; } = CopilotModuleAgentMode.Auto;

        public IReadOnlyDictionary<string, object?> Arguments { get; init; } =
            new Dictionary<string, object?>();

        public bool FrameworkApprovalGranted { get; init; }
    }

    public sealed class CopilotModuleToolExecutionHookDecision
    {
        public static CopilotModuleToolExecutionHookDecision Proceed { get; } = new()
        {
            ShouldProceed = true,
        };

        public bool ShouldProceed { get; init; }

        public string Reason { get; init; } = string.Empty;

        public string FailureCode { get; init; } = string.Empty;

        public static CopilotModuleToolExecutionHookDecision Deny(
            string reason,
            string failureCode = "extension_hook_denied")
        {
            return new CopilotModuleToolExecutionHookDecision
            {
                Reason = reason ?? string.Empty,
                FailureCode = failureCode ?? string.Empty,
            };
        }
    }

    public sealed class CopilotModuleToolPermissionRequestDecision
    {
        public static CopilotModuleToolPermissionRequestDecision Prompt { get; } = new()
        {
            ShouldPrompt = true,
        };

        public bool ShouldPrompt { get; init; }

        public string Reason { get; init; } = string.Empty;

        public string FailureCode { get; init; } = string.Empty;

        public static CopilotModuleToolPermissionRequestDecision PromptWithReason(string reason) => new()
        {
            ShouldPrompt = true,
            Reason = reason ?? string.Empty,
        };

        public static CopilotModuleToolPermissionRequestDecision Deny(
            string reason,
            string failureCode = "extension_permission_hook_denied")
        {
            return new CopilotModuleToolPermissionRequestDecision
            {
                ShouldPrompt = false,
                Reason = reason ?? string.Empty,
                FailureCode = failureCode ?? string.Empty,
            };
        }
    }

    public enum CopilotModuleToolExecutionState
    {
        Completed,
        Failed,
        TimedOut,
        Denied,
        Cancelled,
        AwaitingApproval,
    }

    public sealed class CopilotModuleToolExecutionHookOutcome
    {
        public CopilotModuleToolExecutionHookContext Context { get; init; } = new();

        public CopilotModuleToolExecutionState State { get; init; }

        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public string FailureCode { get; init; } = string.Empty;

        public long DurationMs { get; init; }
    }

    /// <summary>
    /// A trusted in-process lifecycle hook contributed by a ColorVision business module.
    /// Hooks can inspect exact tool arguments, but they do not grant framework approval.
    /// </summary>
    public interface ICopilotModuleToolExecutionHook
    {
        string Name { get; }

        string ToolNamePattern => "*";

        int Order => 0;

        /// <summary>
        /// Async hooks run as bounded background notifications. Their decisions
        /// cannot block, deny, or otherwise control the tool call that launched them.
        /// Async callbacks may overlap and implementations must be thread-safe.
        /// </summary>
        CopilotModuleToolExecutionHookMode ExecutionMode =>
            CopilotModuleToolExecutionHookMode.Sync;

        Task<CopilotModuleToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken);

        Task AfterExecuteAsync(
            CopilotModuleToolExecutionHookOutcome outcome,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// An optional approval-boundary callback for protected tool calls. Returning
    /// Prompt keeps the native ColorVision approval flow; hooks can deny a call
    /// but cannot approve it on the user's behalf.
    /// </summary>
    public interface ICopilotModuleToolPermissionRequestHook : ICopilotModuleToolExecutionHook
    {
        Task<CopilotModuleToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotModuleToolExecutionHookContext context,
            CancellationToken cancellationToken);
    }
}
