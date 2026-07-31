using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotSideQuestionTurn(string Question, string Answer);

    internal sealed class CopilotSideConversationSession
    {
        internal const int MaximumTurns = 8;
        internal const int MaximumTranscriptCharacters = 32_000;

        private readonly List<CopilotSideQuestionTurn> _turns = [];

        public string ParentConversationId { get; }

        public CopilotConversationHistorySnapshot ParentHistory { get; }

        public int TurnCount => _turns.Count;

        public IReadOnlyList<CopilotSideQuestionTurn> Turns => _turns.ToArray();

        public CopilotSideConversationSession(
            string parentConversationId,
            CopilotConversationHistorySnapshot parentHistory)
        {
            ParentConversationId = string.IsNullOrWhiteSpace(parentConversationId)
                ? throw new ArgumentException("A parent conversation id is required.", nameof(parentConversationId))
                : parentConversationId.Trim();
            ParentHistory = parentHistory ?? throw new ArgumentNullException(nameof(parentHistory));
        }

        public bool MatchesParent(string? conversationId) =>
            string.Equals(ParentConversationId, conversationId, StringComparison.Ordinal);

        public IReadOnlyList<CopilotRequestMessage> CaptureTranscript()
        {
            return _turns
                .SelectMany(turn => new[]
                {
                    new CopilotRequestMessage("user", turn.Question),
                    new CopilotRequestMessage("assistant", turn.Answer),
                })
                .ToArray();
        }

        public void AppendTurn(string question, string answer)
        {
            var normalizedQuestion = (question ?? string.Empty).Trim();
            var normalizedAnswer = (answer ?? string.Empty).Trim();
            if (normalizedQuestion.Length == 0)
                throw new ArgumentException("A side question is required.", nameof(question));
            if (normalizedAnswer.Length == 0)
                throw new ArgumentException("A side-question answer is required.", nameof(answer));

            _turns.Add(new CopilotSideQuestionTurn(normalizedQuestion, normalizedAnswer));
            while (_turns.Count > MaximumTurns)
                _turns.RemoveAt(0);
            while (_turns.Count > 1 && CountTranscriptCharacters() > MaximumTranscriptCharacters)
                _turns.RemoveAt(0);
        }

        private int CountTranscriptCharacters()
        {
            var total = 0L;
            foreach (var turn in _turns)
            {
                total += turn.Question.Length;
                total += turn.Answer.Length;
                if (total > int.MaxValue)
                    return int.MaxValue;
            }

            return (int)total;
        }
    }
}
