using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotProviderToolCallLedger
    {
        private readonly Dictionary<string, ProviderToolCallState> _calls = new(StringComparer.Ordinal);
        private readonly object _syncRoot = new();

        public bool TryReserveApproval(string? callId, string signature, out string error)
        {
            var normalizedCallId = NormalizeCallId(callId);
            if (normalizedCallId == null)
            {
                error = string.Empty;
                return true;
            }

            lock (_syncRoot)
            {
                if (_calls.TryGetValue(normalizedCallId, out var existing))
                {
                    error = CreateConflictError(existing.Signature, signature, approvalAlreadyReserved: existing.Execution == null);
                    return false;
                }

                _calls.Add(normalizedCallId, new ProviderToolCallState(signature));
            }

            error = string.Empty;
            return true;
        }

        public async Task<CopilotProviderToolCallResult> ExecuteOnceAsync(
            string? callId,
            string signature,
            Func<Task<string>> executionFactory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(executionFactory);
            var normalizedCallId = NormalizeCallId(callId);
            if (normalizedCallId == null)
                return CopilotProviderToolCallResult.Executed(await executionFactory());

            Lazy<Task<string>> execution;
            lock (_syncRoot)
            {
                if (!_calls.TryGetValue(normalizedCallId, out var state))
                {
                    state = new ProviderToolCallState(signature);
                    _calls.Add(normalizedCallId, state);
                }
                else if (!string.Equals(state.Signature, signature, StringComparison.Ordinal))
                {
                    return CopilotProviderToolCallResult.Conflict(CreateConflictError(state.Signature, signature, approvalAlreadyReserved: false));
                }

                if (state.Execution == null)
                {
                    state.Execution = new Lazy<Task<string>>(executionFactory, LazyThreadSafetyMode.ExecutionAndPublication);
                }
                execution = state.Execution;
            }

            var content = await execution.Value.WaitAsync(cancellationToken);
            return CopilotProviderToolCallResult.Executed(content);
        }

        private static string? NormalizeCallId(string? callId)
        {
            return string.IsNullOrWhiteSpace(callId) ? null : callId.Trim();
        }

        private static string CreateConflictError(string existingSignature, string requestedSignature, bool approvalAlreadyReserved)
        {
            if (string.Equals(existingSignature, requestedSignature, StringComparison.Ordinal))
            {
                return approvalAlreadyReserved
                    ? "The provider repeated a tool call id that is already awaiting approval. Reuse the existing approval response."
                    : "The provider repeated a tool call id that has already been executed. The existing tool result was preserved.";
            }

            return "The provider reused a tool call id with a different tool or argument set. The conflicting call was rejected.";
        }

        private sealed class ProviderToolCallState(string signature)
        {
            public string Signature { get; } = signature;

            public Lazy<Task<string>>? Execution { get; set; }
        }
    }

    internal sealed class CopilotProviderToolCallResult
    {
        private CopilotProviderToolCallResult(string content, string error)
        {
            Content = content;
            Error = error;
        }

        public string Content { get; }

        public string Error { get; }

        public bool HasConflict => Error.Length > 0;

        public static CopilotProviderToolCallResult Executed(string content) => new(content, string.Empty);

        public static CopilotProviderToolCallResult Conflict(string error) => new(string.Empty, error);
    }
}
