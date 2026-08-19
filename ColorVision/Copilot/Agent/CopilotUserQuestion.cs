using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotUserQuestionResolution
    {
        Pending,
        Answered,
        Cancelled,
    }

    public sealed class CopilotUserQuestionOption
    {
        public string RequestId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        internal bool IsStructurallyValid(string requestId, string taskId)
        {
            return string.Equals(RequestId, requestId, StringComparison.Ordinal)
                && string.Equals(TaskId, taskId, StringComparison.Ordinal)
                && CopilotUserQuestionSnapshot.IsBoundedDisplayText(Label, 80)
                && Description.Length <= 240
                && Description.All(character => !char.IsControl(character));
        }
    }

    public sealed class CopilotUserQuestionSnapshot
    {
        public const int MaximumAnswerCharacters = 4_000;

        public string RequestId { get; init; } = string.Empty;

        public string ConversationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string Header { get; init; } = string.Empty;

        public string Question { get; init; } = string.Empty;

        public IReadOnlyList<CopilotUserQuestionOption> Options { get; init; } =
            Array.Empty<CopilotUserQuestionOption>();

        public CopilotUserQuestionResolution Resolution { get; init; }

        public string Answer { get; init; } = string.Empty;

        public DateTimeOffset RequestedAtUtc { get; init; }

        public DateTimeOffset? ResolvedAtUtc { get; init; }

        public bool IsPending => Resolution == CopilotUserQuestionResolution.Pending;

        public bool IsStructurallyValid()
        {
            return CopilotAgentTaskEventIds.IsKey(RequestId, "question", 32)
                && ConversationId.Length is > 0 and <= 160
                && ConversationId.All(character => !char.IsControl(character))
                && CopilotAgentTaskEventIds.IsKey(TaskId, "run", 32)
                && IsBoundedDisplayText(Header, 12)
                && IsBoundedDisplayText(Question, 500)
                && Options.Count is >= 2 and <= 3
                && Options.All(option => option?.IsStructurallyValid(RequestId, TaskId) == true)
                && Options.Select(option => option.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Options.Count
                && Enum.IsDefined(Resolution)
                && RequestedAtUtc != default
                && IsResolutionValid();
        }

        internal static bool TryCreateSnapshot(
            CopilotUserQuestionSnapshot? source,
            out CopilotUserQuestionSnapshot snapshot)
        {
            snapshot = null!;
            if (source == null)
                return false;

            try
            {
                var candidate = new CopilotUserQuestionSnapshot
                {
                    RequestId = source.RequestId,
                    ConversationId = source.ConversationId,
                    TaskId = source.TaskId,
                    Header = source.Header,
                    Question = source.Question,
                    Options = Array.AsReadOnly(source.Options
                        .Take(4)
                        .Select(option => new CopilotUserQuestionOption
                        {
                            RequestId = option.RequestId,
                            TaskId = option.TaskId,
                            Label = option.Label,
                            Description = option.Description,
                        })
                        .ToArray()),
                    Resolution = source.Resolution,
                    Answer = source.Answer,
                    RequestedAtUtc = source.RequestedAtUtc,
                    ResolvedAtUtc = source.ResolvedAtUtc,
                };
                if (!candidate.IsStructurallyValid())
                    return false;

                snapshot = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryCreate(
            string conversationId,
            string taskId,
            CopilotUserQuestionInput input,
            out CopilotUserQuestionSnapshot snapshot,
            out string error)
        {
            snapshot = null!;
            error = string.Empty;
            if (input == null)
            {
                error = "Question input is missing.";
                return false;
            }

            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            var normalizedTaskId = (taskId ?? string.Empty).Trim();
            var normalizedHeader = (input.Header ?? string.Empty).Trim();
            var normalizedQuestion = (input.Question ?? string.Empty).Trim();
            if (normalizedConversationId.Length is < 1 or > 160
                || normalizedConversationId.Any(char.IsControl))
            {
                error = "The active conversation identity is invalid.";
                return false;
            }
            if (!CopilotAgentTaskEventIds.IsKey(normalizedTaskId, "run", 32))
            {
                error = "The active Agent task identity is invalid.";
                return false;
            }
            if (!IsBoundedDisplayText(normalizedHeader, 12))
            {
                error = "header must contain 1-12 display characters.";
                return false;
            }
            if (!IsBoundedDisplayText(normalizedQuestion, 500))
            {
                error = "question must contain 1-500 display characters.";
                return false;
            }
            if (input.Options?.Count is not (>= 2 and <= 3))
            {
                error = "options must contain 2-3 choices.";
                return false;
            }

            var requestId = "question:" + Guid.NewGuid().ToString("N");
            var options = new List<CopilotUserQuestionOption>(input.Options.Count);
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in input.Options)
            {
                var label = (option?.Label ?? string.Empty).Trim();
                var description = (option?.Description ?? string.Empty).Trim();
                if (!IsBoundedDisplayText(label, 80))
                {
                    error = "Every option label must contain 1-80 display characters.";
                    return false;
                }
                if (description.Length > 240 || description.Any(char.IsControl))
                {
                    error = "Every option description must contain at most 240 display characters.";
                    return false;
                }
                if (!labels.Add(label))
                {
                    error = "Option labels must be unique.";
                    return false;
                }

                options.Add(new CopilotUserQuestionOption
                {
                    RequestId = requestId,
                    TaskId = normalizedTaskId,
                    Label = label,
                    Description = description,
                });
            }

            snapshot = new CopilotUserQuestionSnapshot
            {
                RequestId = requestId,
                ConversationId = normalizedConversationId,
                TaskId = normalizedTaskId,
                Header = normalizedHeader,
                Question = normalizedQuestion,
                Options = Array.AsReadOnly(options.ToArray()),
                RequestedAtUtc = DateTimeOffset.UtcNow,
            };
            return true;
        }

        internal CopilotUserQuestionSnapshot Resolve(
            CopilotUserQuestionResolution resolution,
            string answer)
        {
            var candidate = new CopilotUserQuestionSnapshot
            {
                RequestId = RequestId,
                ConversationId = ConversationId,
                TaskId = TaskId,
                Header = Header,
                Question = Question,
                Options = Options,
                Resolution = resolution,
                Answer = resolution == CopilotUserQuestionResolution.Answered ? answer : string.Empty,
                RequestedAtUtc = RequestedAtUtc,
                ResolvedAtUtc = DateTimeOffset.UtcNow,
            };
            if (!TryCreateSnapshot(candidate, out var snapshot))
                throw new InvalidOperationException("The resolved user question snapshot is invalid.");
            return snapshot;
        }

        internal static bool TryNormalizeAnswer(string? answer, out string normalized)
        {
            normalized = (answer ?? string.Empty).Trim();
            return normalized.Length is > 0 and <= MaximumAnswerCharacters
                && normalized.All(character => character != '\0');
        }

        internal static bool IsBoundedDisplayText(string? value, int maximumLength)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= maximumLength
                && value.All(character => !char.IsControl(character));
        }

        private bool IsResolutionValid()
        {
            return Resolution switch
            {
                CopilotUserQuestionResolution.Pending => string.IsNullOrEmpty(Answer) && ResolvedAtUtc == null,
                CopilotUserQuestionResolution.Answered => TryNormalizeAnswer(Answer, out var normalized)
                    && string.Equals(Answer, normalized, StringComparison.Ordinal)
                    && ResolvedAtUtc >= RequestedAtUtc,
                CopilotUserQuestionResolution.Cancelled => string.IsNullOrEmpty(Answer)
                    && ResolvedAtUtc >= RequestedAtUtc,
                _ => false,
            };
        }
    }

    internal sealed class CopilotUserQuestionInput
    {
        public string Header { get; init; } = string.Empty;

        public string Question { get; init; } = string.Empty;

        public IReadOnlyList<CopilotUserQuestionInputOption> Options { get; init; } =
            Array.Empty<CopilotUserQuestionInputOption>();
    }

    internal sealed class CopilotUserQuestionInputOption
    {
        public string Label { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;
    }

    internal sealed class CopilotUserQuestionCoordinator
    {
        private readonly object _syncRoot = new();
        private PendingQuestion? _pending;

        public bool HasPendingQuestion
        {
            get
            {
                lock (_syncRoot)
                    return _pending != null;
            }
        }

        public async Task<CopilotUserQuestionSnapshot> AskAsync(
            CopilotAgentRequest request,
            CopilotUserQuestionInput input,
            Action<CopilotAgentEvent> emit,
            Func<CancellationToken, ValueTask<bool>> publishCheckpoint,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(emit);
            ArgumentNullException.ThrowIfNull(publishCheckpoint);
            cancellationToken.ThrowIfCancellationRequested();
            if (!CopilotUserQuestionSnapshot.TryCreate(
                    request.ConversationId,
                    request.TaskId,
                    input,
                    out var snapshot,
                    out var error))
            {
                throw new ArgumentException(error, nameof(input));
            }

            var pending = new PendingQuestion(snapshot);
            lock (_syncRoot)
            {
                if (_pending != null)
                    throw new InvalidOperationException("Another user question is already waiting for an answer.");
                _pending = pending;
            }

            var terminalResolutionRecorded = false;
            try
            {
                emit(CopilotAgentEvent.UserQuestionRequested(snapshot));
                if (!await publishCheckpoint(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "The structured question could not be checkpointed before waiting for user input.");
                }
                using var cancellationRegistration = cancellationToken.Register(
                    () => pending.Completion.TrySetCanceled(cancellationToken));
                var answer = await pending.Completion.Task.ConfigureAwait(false);
                var resolved = snapshot.Resolve(CopilotUserQuestionResolution.Answered, answer);
                terminalResolutionRecorded = true;
                emit(CopilotAgentEvent.UserQuestionResolved(resolved));
                if (!await publishCheckpoint(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "The structured question answer could not be checkpointed before resuming the Agent.");
                }
                return resolved;
            }
            catch
            {
                if (!terminalResolutionRecorded)
                {
                    terminalResolutionRecorded = true;
                    emit(CopilotAgentEvent.UserQuestionResolved(
                        snapshot.Resolve(CopilotUserQuestionResolution.Cancelled, string.Empty)));
                }
                throw;
            }
            finally
            {
                lock (_syncRoot)
                {
                    if (ReferenceEquals(_pending, pending))
                        _pending = null;
                }
            }
        }

        public bool TryAnswer(string taskId, string requestId, string answer)
        {
            if (!CopilotUserQuestionSnapshot.TryNormalizeAnswer(answer, out var normalized))
                return false;

            PendingQuestion? pending;
            lock (_syncRoot)
            {
                pending = _pending;
                if (pending == null
                    || !string.Equals(pending.Snapshot.TaskId, taskId?.Trim(), StringComparison.Ordinal)
                    || !string.Equals(pending.Snapshot.RequestId, requestId?.Trim(), StringComparison.Ordinal))
                {
                    return false;
                }
                _pending = null;
            }

            return pending.Completion.TrySetResult(normalized);
        }

        private sealed class PendingQuestion
        {
            public PendingQuestion(CopilotUserQuestionSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public CopilotUserQuestionSnapshot Snapshot { get; }

            public TaskCompletionSource<string> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
