using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConversationRecord
    {
        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (UpdatedAt == default)
            {
                UpdatedAt = CreatedAt;
                changed = true;
            }
            else if (UpdatedAt < CreatedAt)
            {
                UpdatedAt = CreatedAt;
                changed = true;
            }

            if (RecencyAt == default || RecencyAt < CreatedAt)
            {
                RecencyAt = UpdatedAt;
                changed = true;
            }
            if (UpdatedAt < RecencyAt)
            {
                UpdatedAt = RecencyAt;
                changed = true;
            }

            if (_draftText == null)
            {
                DraftText = string.Empty;
                changed = true;
            }
            if (!Enum.IsDefined(DraftRequestMode))
            {
                DraftRequestMode = CopilotAgentMode.Auto;
                changed = true;
            }
            if (DraftWorkspaceReviewTarget != null
                && (DraftRequestMode != CopilotAgentMode.Review
                    || !DraftWorkspaceReviewTarget.IsStructurallyValid()))
            {
                DraftWorkspaceReviewTarget = null;
                changed = true;
            }
            if (DraftAgentSkillReference != null
                && (!DraftAgentSkillReference.IsStructurallyValid()
                    || !DraftAgentSkillReference.IsExplicitlyInvokedBy(DraftText)))
            {
                DraftAgentSkillReference = null;
                changed = true;
            }
            if (ComposerStash != null)
            {
                changed |= ComposerStash.EnsureValid();
                if (!ComposerStash.HasContent)
                {
                    ComposerStash = null;
                    changed = true;
                }
            }
            if (!Enum.IsDefined(ResponsePersonality))
            {
                ResponsePersonality = CopilotResponsePersonality.None;
                changed = true;
            }
            else if (!HasResponsePersonalityOverride
                && ResponsePersonality != CopilotResponsePersonality.None)
            {
                HasResponsePersonalityOverride = true;
                changed = true;
            }
            if (_legacyAccessModeLoaded)
            {
                _legacyAccessModeLoaded = false;
                changed = true;
            }
            changed |= _accessContext.Revoke();

            if (Messages == null)
            {
                Messages = new ObservableCollection<CopilotChatMessage>();
                changed = true;
            }
            if (Attachments == null)
            {
                Attachments = new ObservableCollection<CopilotAttachmentItem>();
                changed = true;
            }
            changed |= CopilotSteeringRecovery.NormalizePendingRecords(this);
            if (AdditionalReadRootPaths == null)
            {
                AdditionalReadRootPaths = new ObservableCollection<string>();
                changed = true;
            }
            var normalizedReadRoots = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                AdditionalReadRootPaths);
            if (!AdditionalReadRootPaths.SequenceEqual(
                    normalizedReadRoots,
                    StringComparer.OrdinalIgnoreCase))
            {
                AdditionalReadRootPaths.Clear();
                foreach (var path in normalizedReadRoots)
                    AdditionalReadRootPaths.Add(path);
                changed = true;
            }
            for (var index = Messages.Count - 1; index >= 0; index--)
            {
                if (Messages[index] != null)
                    continue;

                Messages.RemoveAt(index);
                changed = true;
            }
            for (var index = Attachments.Count - 1; index >= 0; index--)
            {
                if (Attachments[index] != null)
                    continue;

                Attachments.RemoveAt(index);
                changed = true;
            }
            if (AgentSessionCheckpoint != null && !AgentSessionCheckpoint.IsStructurallyValid())
            {
                AgentSessionCheckpoint = null;
                changed = true;
            }
            if (LatestAgentTaskEventJournal != null
                && (LatestAgentTaskEventJournal.Events?.Count is not > 0
                    || !LatestAgentTaskEventJournal.IsStructurallyValid()))
            {
                LatestAgentTaskEventJournal = null;
                changed = true;
            }
            if (LatestAgentTaskEventJournal == null
                && AgentSessionCheckpoint?.TaskEventJournal is { Events.Count: > 0 } checkpointJournal)
            {
                changed |= UpdateLatestAgentTaskEventJournal(checkpointJournal);
            }
            if (Compaction != null && !Compaction.IsStructurallyValid())
            {
                Compaction = null;
                changed = true;
            }
            changed |= EnsureAuxiliaryUsageValid();
            if (BranchOrigin != null && !BranchOrigin.IsStructurallyValid(Id))
            {
                BranchOrigin = null;
                changed = true;
            }
            if (Goal != null && !Goal.IsStructurallyValid())
            {
                Goal = null;
                changed = true;
            }
            if (IsGoalContinuationDeferred
                && (Goal?.IsActive != true || BranchOrigin?.IsStructurallyValid(Id) != true))
            {
                IsGoalContinuationDeferred = false;
                changed = true;
            }

            var lastUserRequestMode = CopilotAgentMode.Chat;
            var messageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var message in Messages)
            {
                changed |= message.EnsureValid();
                if (!messageIds.Add(message.Id))
                {
                    string replacementId;
                    do
                    {
                        replacementId = Guid.NewGuid().ToString("N");
                    }
                    while (!messageIds.Add(replacementId));

                    message.Id = replacementId;
                    changed = true;
                }
                if (message.IsUser)
                {
                    lastUserRequestMode = message.RequestMode;
                }
                else if (message.RequestMode != lastUserRequestMode)
                {
                    message.RequestMode = lastUserRequestMode;
                    changed = true;
                }
            }
            if (AgentActivity != null && !HasValidAgentActivitySource())
            {
                AgentActivity = null;
                changed = true;
            }
            var lastAssistantMessage = Messages.LastOrDefault(message =>
                !message.IsUser
                && !message.WasResponseInterrupted);
            if (lastAssistantMessage != null
                && !lastAssistantMessage.ReportedUsage.HasAny
                && LastUsage.HasAny)
            {
                changed |= lastAssistantMessage.SetReportedUsage(LastUsage);
            }

            foreach (var attachment in Attachments)
            {
                changed |= attachment.EnsureValid();
            }

            return changed;
        }

        internal bool ReplaceAdditionalReadRootPaths(IEnumerable<string>? paths)
        {
            var normalized = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(paths);
            AdditionalReadRootPaths ??= new ObservableCollection<string>();
            if (AdditionalReadRootPaths.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
                return false;

            AdditionalReadRootPaths.Clear();
            foreach (var path in normalized)
                AdditionalReadRootPaths.Add(path);
            OnPropertyChanged(nameof(AdditionalReadRootPaths));
            OnPropertyChanged(nameof(HasAdditionalReadRoots));
            return true;
        }

        internal bool UpdateLatestAgentTaskEventJournal(CopilotAgentTaskEventJournalSnapshot? journal)
        {
            if (journal?.Events?.Count is not > 0 || !journal.IsStructurallyValid())
                return false;

            var currentEvents = LatestAgentTaskEventJournal?.Events;
            var currentLast = currentEvents?.Count > 0 ? currentEvents[^1] : null;
            var candidateLast = journal.Events[^1];
            if (LatestAgentTaskEventJournal?.IsStructurallyValid() == true
                && currentLast != null
                && string.Equals(currentLast.RunId, candidateLast.RunId, StringComparison.Ordinal)
                && currentLast.Sequence >= candidateLast.Sequence)
            {
                return false;
            }

            LatestAgentTaskEventJournal = journal;
            return true;
        }

        internal bool SetAgentSessionCheckpoint(CopilotAgentSessionCheckpoint? checkpoint)
        {
            var changed = !ReferenceEquals(AgentSessionCheckpoint, checkpoint);
            AgentSessionCheckpoint = checkpoint;
            if (checkpoint != null)
                changed |= UpdateLatestAgentTaskEventJournal(checkpoint.TaskEventJournal);
            return changed;
        }

        internal bool CommitAgentRunState(
            CopilotAgentTaskEventJournalSnapshot? journal,
            CopilotAgentSessionCheckpoint? checkpoint)
        {
            var changed = UpdateLatestAgentTaskEventJournal(journal);
            changed |= SetAgentSessionCheckpoint(checkpoint);
            return changed;
        }
    }
}
