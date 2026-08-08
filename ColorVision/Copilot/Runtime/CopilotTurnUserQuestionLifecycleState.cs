using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnUserQuestionLifecycleState
    {
        private readonly CopilotUserQuestionSnapshot? _pending;
        private readonly IReadOnlySet<string> _resolvedRequestIds;

        private CopilotTurnUserQuestionLifecycleState(
            CopilotUserQuestionSnapshot? pending,
            IReadOnlySet<string> resolvedRequestIds)
        {
            _pending = pending;
            _resolvedRequestIds = resolvedRequestIds;
        }

        public static CopilotTurnUserQuestionLifecycleState Empty { get; } = new(
            pending: null,
            new HashSet<string>(StringComparer.Ordinal));

        public CopilotTurnUserQuestionLifecycleState Observe(
            CopilotAgentEvent agentEvent,
            string turnId)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            turnId = CopilotTurnStartedEvent.NormalizeTurnId(turnId);
            return agentEvent.Type switch
            {
                CopilotAgentEventType.UserQuestionRequested => ObserveRequested(agentEvent, turnId),
                CopilotAgentEventType.UserQuestionResolved => ObserveResolved(agentEvent, turnId),
                CopilotAgentEventType.Completed => ObserveAgentCompleted(),
                _ => this,
            };
        }

        private CopilotTurnUserQuestionLifecycleState ObserveRequested(
            CopilotAgentEvent agentEvent,
            string turnId)
        {
            var question = agentEvent.UserQuestion;
            if (question?.IsPending != true || !question.IsStructurallyValid())
                throw new InvalidOperationException("Copilot Agent emitted an invalid user question request.");
            RequireMatchingTurn(question, turnId);
            if (_pending != null)
                throw new InvalidOperationException("Copilot Agent requested another user question before resolving the active request.");
            if (_resolvedRequestIds.Contains(question.RequestId))
                throw new InvalidOperationException("Copilot Agent reused a resolved user question request ID.");

            return new CopilotTurnUserQuestionLifecycleState(
                CreateSnapshot(question),
                _resolvedRequestIds);
        }

        private CopilotTurnUserQuestionLifecycleState ObserveResolved(
            CopilotAgentEvent agentEvent,
            string turnId)
        {
            var question = agentEvent.UserQuestion;
            if (question == null || question.IsPending || !question.IsStructurallyValid())
                throw new InvalidOperationException("Copilot Agent emitted an invalid user question resolution.");
            RequireMatchingTurn(question, turnId);
            if (_pending == null)
                throw new InvalidOperationException("Copilot Agent resolved a user question before requesting it.");
            if (!HasSameRequest(_pending, question))
                throw new InvalidOperationException("Copilot Agent resolved a different user question than the active request.");
            if (_resolvedRequestIds.Contains(question.RequestId))
                throw new InvalidOperationException("Copilot Agent resolved the same user question more than once.");

            var resolvedRequestIds = new HashSet<string>(_resolvedRequestIds, StringComparer.Ordinal)
            {
                question.RequestId,
            };
            return new CopilotTurnUserQuestionLifecycleState(
                pending: null,
                resolvedRequestIds);
        }

        private CopilotTurnUserQuestionLifecycleState ObserveAgentCompleted()
        {
            if (_pending != null)
                throw new InvalidOperationException("Copilot Agent completed while a user question request was still pending.");
            return this;
        }

        private static void RequireMatchingTurn(
            CopilotUserQuestionSnapshot question,
            string turnId)
        {
            if (!string.Equals(question.TaskId, turnId, StringComparison.Ordinal))
                throw new InvalidOperationException("Copilot Agent user question referenced a different turn ID.");
        }

        private static bool HasSameRequest(
            CopilotUserQuestionSnapshot pending,
            CopilotUserQuestionSnapshot resolved)
        {
            return string.Equals(pending.RequestId, resolved.RequestId, StringComparison.Ordinal)
                && string.Equals(pending.ConversationId, resolved.ConversationId, StringComparison.Ordinal)
                && string.Equals(pending.TaskId, resolved.TaskId, StringComparison.Ordinal)
                && string.Equals(pending.Header, resolved.Header, StringComparison.Ordinal)
                && string.Equals(pending.Question, resolved.Question, StringComparison.Ordinal)
                && pending.RequestedAtUtc == resolved.RequestedAtUtc
                && pending.Options.Count == resolved.Options.Count
                && pending.Options.Zip(resolved.Options).All(pair =>
                    string.Equals(pair.First.RequestId, pair.Second.RequestId, StringComparison.Ordinal)
                    && string.Equals(pair.First.TaskId, pair.Second.TaskId, StringComparison.Ordinal)
                    && string.Equals(pair.First.Label, pair.Second.Label, StringComparison.Ordinal)
                    && string.Equals(pair.First.Description, pair.Second.Description, StringComparison.Ordinal));
        }

        private static CopilotUserQuestionSnapshot CreateSnapshot(CopilotUserQuestionSnapshot source)
        {
            return new CopilotUserQuestionSnapshot
            {
                RequestId = source.RequestId,
                ConversationId = source.ConversationId,
                TaskId = source.TaskId,
                Header = source.Header,
                Question = source.Question,
                Options = source.Options.Select(option => new CopilotUserQuestionOption
                {
                    RequestId = option.RequestId,
                    TaskId = option.TaskId,
                    Label = option.Label,
                    Description = option.Description,
                }).ToArray(),
                Resolution = source.Resolution,
                Answer = source.Answer,
                RequestedAtUtc = source.RequestedAtUtc,
                ResolvedAtUtc = source.ResolvedAtUtc,
            };
        }
    }
}
