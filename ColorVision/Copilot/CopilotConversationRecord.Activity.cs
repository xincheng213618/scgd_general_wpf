using System;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConversationRecord
    {
        public CopilotConversationActivity? AgentActivity
        {
            get => _agentActivity;
            set
            {
                if (SetProperty(ref _agentActivity, value))
                {
                    OnPropertyChanged(nameof(AgentRunStatusLabel));
                    OnPropertyChanged(nameof(HasAgentRunStatus));
                }
            }
        }
        private CopilotConversationActivity? _agentActivity;

        public bool ShouldSerializeAgentActivity() => AgentActivity?.IsStructurallyValid() == true;

        internal bool ReplaceAgentActivity(CopilotConversationActivity? activity)
        {
            if (ActivitiesMatch(AgentActivity, activity))
                return false;

            AgentActivity = activity;
            return true;
        }

        internal bool AcknowledgeAgentActivityByViewing()
        {
            if (AgentActivity?.IsAcknowledgedByViewing != true)
                return false;

            AgentActivity = null;
            return true;
        }

        internal bool ClearAgentActivityForMessage(string? messageId)
        {
            if (AgentActivity == null
                || !string.Equals(AgentActivity.SourceMessageId, messageId?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            AgentActivity = null;
            return true;
        }

        internal bool HasValidAgentActivitySource()
        {
            if (AgentActivity?.IsStructurallyValid() != true)
                return false;

            var latestAssistant = Messages.LastOrDefault(message => message != null && !message.IsUser);
            return latestAssistant != null
                && string.Equals(latestAssistant.Id, AgentActivity.SourceMessageId, StringComparison.Ordinal);
        }

        private static bool ActivitiesMatch(
            CopilotConversationActivity? first,
            CopilotConversationActivity? second)
        {
            if (ReferenceEquals(first, second))
                return true;
            if (first == null || second == null)
                return false;
            return first.SchemaVersion == second.SchemaVersion
                && first.State == second.State
                && string.Equals(first.SourceMessageId, second.SourceMessageId, StringComparison.Ordinal)
                && first.UpdatedAtUtc == second.UpdatedAtUtc;
        }
    }
}
