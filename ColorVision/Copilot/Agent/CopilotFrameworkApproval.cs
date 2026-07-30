using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotFrameworkApprovalDecisionKind
    {
        Approved,
        Rejected,
        Expired,
        Cancelled,
        PolicyDenied,
    }

    internal enum CopilotFrameworkApprovalDecisionSource
    {
        None,
        User,
        TemporaryGrant,
        AutomaticReview,
    }

    internal sealed record CopilotFrameworkApprovalDecision
    {
        private CopilotFrameworkApprovalDecision(
            CopilotFrameworkApprovalDecisionKind kind,
            string reason,
            string failureCode,
            CopilotFrameworkApprovalDecisionSource source)
        {
            Kind = kind;
            Reason = reason;
            FailureCode = CopilotToolFailureCode.Normalize(failureCode);
            Source = source;
        }

        public CopilotFrameworkApprovalDecisionKind Kind { get; }

        public string Reason { get; }

        public string FailureCode { get; }

        public CopilotFrameworkApprovalDecisionSource Source { get; }

        public bool IsApproved => Kind == CopilotFrameworkApprovalDecisionKind.Approved;

        public string FormatStatus(string toolName)
        {
            var name = string.IsNullOrWhiteSpace(toolName) ? "The protected tool" : toolName.Trim();
            return Kind switch
            {
                CopilotFrameworkApprovalDecisionKind.Approved
                    when Source == CopilotFrameworkApprovalDecisionSource.AutomaticReview =>
                    $"{name} was approved by the automatic permission reviewer. Agent Framework is resuming the same session.",
                CopilotFrameworkApprovalDecisionKind.Approved
                    when Source == CopilotFrameworkApprovalDecisionSource.TemporaryGrant =>
                    $"{name} was approved by the temporary structured-workspace grant. Agent Framework is resuming the same session.",
                CopilotFrameworkApprovalDecisionKind.Approved =>
                    $"{name} was approved by the ColorVision user. Agent Framework is resuming the same session.",
                CopilotFrameworkApprovalDecisionKind.Rejected => $"{name} was rejected by the ColorVision user. Agent Framework will continue without executing it.",
                CopilotFrameworkApprovalDecisionKind.Expired => $"{name} approval expired. Agent Framework will continue without executing it.",
                CopilotFrameworkApprovalDecisionKind.Cancelled => $"{name} approval was cancelled. Agent Framework will continue without executing it.",
                _ => $"{name} was denied by ColorVision policy. Agent Framework will continue without executing it.",
            };
        }

        public string FormatToolSummary(string toolName)
        {
            var name = string.IsNullOrWhiteSpace(toolName) ? "The protected tool" : toolName.Trim();
            return Kind switch
            {
                CopilotFrameworkApprovalDecisionKind.Rejected => $"{name} was rejected by the user.",
                CopilotFrameworkApprovalDecisionKind.Expired => $"{name} approval expired.",
                CopilotFrameworkApprovalDecisionKind.Cancelled => $"{name} approval was cancelled.",
                CopilotFrameworkApprovalDecisionKind.PolicyDenied => $"{name} was denied by policy.",
                _ => $"{name} was approved.",
            };
        }

        public static CopilotFrameworkApprovalDecision FromStatus(ConfirmableActionStatus status)
        {
            return status switch
            {
                ConfirmableActionStatus.Approved => new(
                    CopilotFrameworkApprovalDecisionKind.Approved,
                    "Approved in ColorVision.",
                    string.Empty,
                    CopilotFrameworkApprovalDecisionSource.User),
                ConfirmableActionStatus.Rejected => new(
                    CopilotFrameworkApprovalDecisionKind.Rejected,
                    "Rejected by the ColorVision user.",
                    "approval_rejected",
                    CopilotFrameworkApprovalDecisionSource.User),
                ConfirmableActionStatus.Expired => new(
                    CopilotFrameworkApprovalDecisionKind.Expired,
                    "The ColorVision approval expired before a decision.",
                    "approval_expired",
                    CopilotFrameworkApprovalDecisionSource.None),
                ConfirmableActionStatus.Cancelled => new(
                    CopilotFrameworkApprovalDecisionKind.Cancelled,
                    "The ColorVision approval was cancelled before execution.",
                    "approval_cancelled",
                    CopilotFrameworkApprovalDecisionSource.None),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The confirmation action has no terminal approval decision."),
            };
        }

        public static CopilotFrameworkApprovalDecision FromAction(ConfirmableAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (action.Status != ConfirmableActionStatus.Approved)
                return FromStatus(action.Status);

            if (string.Equals(action.ApprovalDecisionSource, "automatic-review", StringComparison.Ordinal))
            {
                var detail = string.IsNullOrWhiteSpace(action.ApprovalDecisionReason)
                    ? "The protected action satisfied the automatic permission policy."
                    : action.ApprovalDecisionReason.Trim();
                return new CopilotFrameworkApprovalDecision(
                    CopilotFrameworkApprovalDecisionKind.Approved,
                    "Approved by the automatic ColorVision permission reviewer: " + detail,
                    string.Empty,
                    CopilotFrameworkApprovalDecisionSource.AutomaticReview);
            }

            return FromStatus(action.Status);
        }

        public static CopilotFrameworkApprovalDecision PolicyDenied(
            string reason,
            string failureCode = "approval_policy_denied")
        {
            var detail = string.IsNullOrWhiteSpace(reason) ? "The protected tool call did not satisfy the approval policy." : reason.Trim();
            return new CopilotFrameworkApprovalDecision(
                CopilotFrameworkApprovalDecisionKind.PolicyDenied,
                "ColorVision policy denied this protected tool call: " + detail,
                string.IsNullOrWhiteSpace(failureCode) ? "approval_policy_denied" : failureCode,
                CopilotFrameworkApprovalDecisionSource.None);
        }

        public static CopilotFrameworkApprovalDecision Cancelled(string reason)
        {
            var detail = string.IsNullOrWhiteSpace(reason)
                ? "The protected tool call was cancelled before execution."
                : reason.Trim();
            return new CopilotFrameworkApprovalDecision(
                CopilotFrameworkApprovalDecisionKind.Cancelled,
                detail,
                "approval_cancelled",
                CopilotFrameworkApprovalDecisionSource.None);
        }

        public static CopilotFrameworkApprovalDecision ApprovedByFullAccess()
        {
            return new CopilotFrameworkApprovalDecision(
                CopilotFrameworkApprovalDecisionKind.Approved,
                "Approved by the current ColorVision task's temporary structured-workspace grant.",
                string.Empty,
                CopilotFrameworkApprovalDecisionSource.TemporaryGrant);
        }
    }

    internal sealed class CopilotFrameworkApprovalHandle
    {
        public ConfirmableAction Action { get; init; } = null!;

        public Task<CopilotFrameworkApprovalDecision> Decision { get; init; } =
            Task.FromResult(CopilotFrameworkApprovalDecision.PolicyDenied("The approval handle was not initialized."));
    }

    internal static class CopilotAgentToolInputExactBinding
    {
        public static string Create(CopilotAgentToolInput? input)
        {
            input ??= CopilotAgentToolInput.Empty;
            var arguments = JsonSerializer.SerializeToElement(input.Arguments);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("arguments");
                WriteCanonicalJsonElement(writer, arguments);
                writer.WriteString("cursor", input.Cursor ?? string.Empty);
                WriteNullableInt(writer, "endLine", input.EndLine);
                writer.WriteString("path", input.Path ?? string.Empty);
                writer.WriteString("query", input.Query ?? string.Empty);
                WriteNullableInt(writer, "startColumn", input.StartColumn);
                WriteNullableInt(writer, "startLine", input.StartLine);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static string CreateExecutionSignature(string? toolName, CopilotAgentToolInput? input)
        {
            var normalizedToolName = JsonSerializer.Serialize(toolName?.Trim() ?? string.Empty);
            var bytes = Encoding.UTF8.GetBytes(normalizedToolName + Create(input));
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        public static bool MatchesExecutionSignature(
            string? toolName,
            CopilotAgentToolInput? input,
            string? expectedSignature)
        {
            if (string.IsNullOrWhiteSpace(expectedSignature))
                return false;

            try
            {
                var actualBytes = Convert.FromHexString(CreateExecutionSignature(toolName, input));
                var expectedBytes = Convert.FromHexString(expectedSignature.Trim());
                return actualBytes.Length == expectedBytes.Length
                    && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
            }
            catch
            {
                return false;
            }
        }

        private static void WriteNullableInt(Utf8JsonWriter writer, string propertyName, int? value)
        {
            writer.WritePropertyName(propertyName);
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }

        private static void WriteCanonicalJsonElement(Utf8JsonWriter writer, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonicalJsonElement(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    return;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray())
                        WriteCanonicalJsonElement(writer, item);
                    writer.WriteEndArray();
                    return;
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    return;
                default:
                    value.WriteTo(writer);
                    return;
            }
        }
    }

    internal static class CopilotAgentToolInputSnapshot
    {
        public static bool TryCreate(
            CopilotAgentToolInput? input,
            out CopilotAgentToolInput snapshot,
            out string error)
        {
            input ??= CopilotAgentToolInput.Empty;
            try
            {
                var serializedArguments = JsonSerializer.SerializeToElement(input.Arguments);
                if (serializedArguments.ValueKind != JsonValueKind.Object)
                {
                    snapshot = CopilotAgentToolInput.Empty;
                    error = "Tool arguments must serialize to a JSON object.";
                    return false;
                }

                var frozenArguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in serializedArguments.EnumerateObject())
                {
                    if (!frozenArguments.TryAdd(property.Name, property.Value.Clone()))
                    {
                        snapshot = CopilotAgentToolInput.Empty;
                        error = $"Tool arguments contain the duplicate field '{property.Name}'.";
                        return false;
                    }
                }

                snapshot = new CopilotAgentToolInput
                {
                    Arguments = new ReadOnlyDictionary<string, object?>(frozenArguments),
                    Query = input.Query ?? string.Empty,
                    Path = input.Path ?? string.Empty,
                    Cursor = input.Cursor ?? string.Empty,
                    StartLine = input.StartLine,
                    StartColumn = input.StartColumn,
                    EndLine = input.EndLine,
                };
                error = string.Empty;
                return true;
            }
            catch
            {
                snapshot = CopilotAgentToolInput.Empty;
                error = "Tool arguments could not be frozen into an immutable approval snapshot.";
                return false;
            }
        }
    }

    internal readonly record struct CopilotFrameworkApprovalReservationKey(
        string ProviderCallId,
        string ExecutionSignature)
    {
        public static CopilotFrameworkApprovalReservationKey Create(
            string? providerCallId,
            string executionSignature)
        {
            return new CopilotFrameworkApprovalReservationKey(
                string.IsNullOrWhiteSpace(providerCallId) ? string.Empty : providerCallId.Trim(),
                executionSignature ?? string.Empty);
        }
    }

    internal sealed class CopilotFrameworkApprovalCoordinator
    {
        private readonly CopilotMcpConfirmationStore _confirmationStore;

        public CopilotFrameworkApprovalCoordinator()
            : this(CopilotMcpConfirmationStore.Instance)
        {
        }

        internal CopilotFrameworkApprovalCoordinator(CopilotMcpConfirmationStore confirmationStore)
        {
            _confirmationStore = confirmationStore ?? throw new ArgumentNullException(nameof(confirmationStore));
        }

        public CopilotFrameworkApprovalHandle RequestApproval(
            ICopilotTool tool,
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            string callId,
            CancellationToken cancellationToken,
            CopilotExecutionScope? executionScope = null)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var completion = new TaskCompletionSource<CopilotFrameworkApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            var argumentsSummary = CopilotToolApprovalArgumentFormatter.Create(input);
            var exactArgumentsBinding = CopilotAgentToolInputExactBinding.Create(input);
            var presentation = tool switch
            {
                ICopilotFrameworkContextualApprovalPresentation contextualPresenter => contextualPresenter.CreateApprovalPresentation(request, input),
                ICopilotFrameworkApprovalPresentation presenter => presenter.CreateApprovalPresentation(input),
                _ => new CopilotToolApprovalPresentation(
                    $"Approve {tool.Name}",
                    $"Microsoft Agent Framework wants to run the protected ColorVision tool {tool.Name} with {argumentsSummary}."),
            };
            ConfirmableAction? action = null;
            EventHandler<ConfirmableActionChangedEventArgs>? statusChanged = null;
            statusChanged = (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Action, action))
                    return;

                switch (eventArgs.Action.Status)
                {
                    case ConfirmableActionStatus.Approved:
                    case ConfirmableActionStatus.Rejected:
                    case ConfirmableActionStatus.Expired:
                    case ConfirmableActionStatus.Cancelled:
                        completion.TrySetResult(CopilotFrameworkApprovalDecision.FromAction(eventArgs.Action));
                        break;
                }
            };
            _confirmationStore.ActionStatusChanged += statusChanged;

            var cancellationRegistration = cancellationToken.Register(() =>
            {
                var currentAction = action;
                if (currentAction?.Status == ConfirmableActionStatus.Pending)
                    _confirmationStore.Cancel(currentAction.ActionId, out _, "The approval request was cancelled with the Agent run.");
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                action = _confirmationStore.CreateAgentFrameworkApproval(
                    presentation.Title,
                    presentation.Description,
                    tool.Name,
                    argumentsSummary,
                    exactArgumentsBinding,
                    callId,
                    CopilotConfirmationRequestContext.ForAgent(
                        request,
                        presentation,
                        "in-app-agent-framework",
                        executionScope),
                    createdAction => action = createdAction,
                    reviewDetails: presentation.ReviewDetails);

                if (cancellationToken.IsCancellationRequested)
                {
                    _confirmationStore.Cancel(action.ActionId, out _, "The approval request was cancelled with the Agent run.");
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch
            {
                cancellationRegistration.Dispose();
                _confirmationStore.ActionStatusChanged -= statusChanged;
                throw;
            }

            return new CopilotFrameworkApprovalHandle
            {
                Action = action,
                Decision = AwaitDecisionAsync(action, completion.Task, statusChanged, cancellationRegistration, cancellationToken),
            };
        }

        public void Cancel(CopilotFrameworkApprovalHandle handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            if (handle.Action.Status == ConfirmableActionStatus.Pending)
                _confirmationStore.Cancel(handle.Action.ActionId, out _, "The approval request was cancelled with the Agent run.");
        }

        public bool ApproveAfterAutomaticReview(
            CopilotFrameworkApprovalHandle handle,
            CopilotAgentRequest request,
            ICopilotTool tool,
            string currentWorkspacePath,
            string decisionReason,
            out string message)
        {
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            if (!CopilotAgentAccessPolicy.CanAutoReview(request, tool, currentWorkspacePath))
            {
                message = "The temporary task grant expired or its workspace scope changed.";
                return false;
            }
            if (handle.Action.Status != ConfirmableActionStatus.Pending)
            {
                message = $"The action is {handle.Action.StatusLabel}.";
                return false;
            }

            return _confirmationStore.ApproveAutomatically(
                handle.Action.ActionId,
                new CopilotConfirmationReviewContext(
                    request.ConversationId,
                    request.TaskId,
                    currentWorkspacePath),
                decisionReason,
                out message);
        }

        public void Cancel(string? actionId, string reason)
        {
            if (!string.IsNullOrWhiteSpace(actionId))
                _confirmationStore.Cancel(actionId, out _, reason);
        }

        public bool BeginIfRequired(
            string? actionId,
            CopilotAgentRequest request,
            string currentWorkspacePath,
            string argumentsDigest,
            string agentCallId,
            CopilotExecutionScope? executionScope = null) =>
            !string.IsNullOrWhiteSpace(actionId)
            && _confirmationStore.BeginAgentFrameworkAction(
                actionId,
                request,
                currentWorkspacePath,
                argumentsDigest,
                agentCallId,
                executionScope);

        public void Complete(string? actionId, CopilotToolResult result)
        {
            if (!string.IsNullOrWhiteSpace(actionId))
                _confirmationStore.CompleteAgentFrameworkAction(actionId, result);
        }

        private async Task<CopilotFrameworkApprovalDecision> AwaitDecisionAsync(
            ConfirmableAction action,
            Task<CopilotFrameworkApprovalDecision> decision,
            EventHandler<ConfirmableActionChangedEventArgs> statusChanged,
            CancellationTokenRegistration cancellationRegistration,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!decision.IsCompleted)
                {
                    var remaining = action.ExpiresAt - DateTimeOffset.UtcNow;
                    if (remaining > TimeSpan.Zero
                        && await Task.WhenAny(decision, Task.Delay(remaining, CancellationToken.None)) == decision)
                    {
                        break;
                    }

                    _confirmationStore.ExpireStaleActions();
                    if (!decision.IsCompleted)
                        await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
                }
                return await decision.WaitAsync(cancellationToken);
            }
            finally
            {
                cancellationRegistration.Dispose();
                _confirmationStore.ActionStatusChanged -= statusChanged;
            }
        }
    }
}
