using ColorVision.Common.MVVM;
using System;

namespace ColorVision.Copilot
{
    public sealed class CopilotQueuedFollowUp : ViewModelBase
    {
        internal CopilotQueuedFollowUp(
            string runId,
            string conversationId,
            string conversationTitle,
            string prompt,
            CopilotAgentMode mode,
            CopilotProfileConfig profile,
            CopilotAgentHostContextSnapshot submissionContext,
            string? goalId = null,
            CopilotAgentSkillReference? agentSkillReference = null,
            CopilotTurnRuntimeConfigSnapshot? runtimeConfigSnapshot = null)
        {
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
            ConversationTitle = string.IsNullOrWhiteSpace(conversationTitle) ? "新会话" : conversationTitle.Trim();
            Prompt = (prompt ?? string.Empty).Trim();
            Mode = mode;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            SubmissionContext = submissionContext ?? throw new ArgumentNullException(nameof(submissionContext));
            GoalId = (goalId ?? string.Empty).Trim();
            AgentSkillReference = agentSkillReference?.IsStructurallyValid() == true
                && agentSkillReference.IsExplicitlyInvokedBy(Prompt)
                    ? agentSkillReference.CreateSnapshot()
                    : null;
            RuntimeConfigSnapshot = runtimeConfigSnapshot?.CreateSnapshot()
                ?? new CopilotTurnRuntimeConfigSnapshot(
                    new CopilotAgentDefaultsConfig(),
                    Array.Empty<CopilotMcpClientServerConfig>());
            QueuedAtUtc = DateTimeOffset.UtcNow;
        }

        public string RunId { get; }

        public string ConversationId { get; }

        public string ConversationTitle { get; }

        public string Prompt { get; }

        public string PromptPreview
        {
            get
            {
                var normalized = Prompt.Replace('\r', ' ').Replace('\n', ' ').Trim();
                return normalized.Length <= 160 ? normalized : normalized[..157] + "...";
            }
        }

        public CopilotAgentMode Mode { get; }

        public DateTimeOffset QueuedAtUtc { get; }

        public string GoalId { get; }

        public bool IsAutomaticGoalContinuation => GoalId.Length > 0;

        internal CopilotAgentSkillReference? AgentSkillReference { get; }

        public int QueuePosition
        {
            get => _queuePosition;
            private set
            {
                if (SetProperty(ref _queuePosition, value))
                    OnPropertyChanged(nameof(PositionLabel));
            }
        }
        private int _queuePosition;

        public string PositionLabel => QueuePosition > 0 ? $"#{QueuePosition}" : "#";

        public bool CanMoveUp
        {
            get => _canMoveUp;
            private set => SetProperty(ref _canMoveUp, value);
        }
        private bool _canMoveUp;

        public bool CanMoveDown
        {
            get => _canMoveDown;
            private set => SetProperty(ref _canMoveDown, value);
        }
        private bool _canMoveDown;

        internal CopilotProfileConfig Profile { get; }

        internal CopilotAgentHostContextSnapshot SubmissionContext { get; }

        internal CopilotTurnRuntimeConfigSnapshot RuntimeConfigSnapshot { get; }

        internal CopilotAgentHostContextSnapshot CreateExecutionContext(
            CopilotConversationHistorySnapshot conversationHistory) =>
            SubmissionContext.WithConversationHistory(conversationHistory);

        internal void UpdateQueuePosition(int position, int totalCount)
        {
            QueuePosition = position;
            CanMoveUp = position > 1;
            CanMoveDown = position > 0 && position < totalCount;
        }
    }
}
