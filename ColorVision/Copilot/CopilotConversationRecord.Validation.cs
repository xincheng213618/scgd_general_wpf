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
            if (LatestAgentTaskEventJournal != null
                && AgentSessionCheckpoint != null)
            {
                // Older snapshots could persist both a recovery checkpoint journal and
                // independent terminal evidence. Select the newer evidence once during
                // load, then migrate it into the checkpoint so only one journal owner
                // survives normalization.
                if (CopilotAgentTaskEventJournal.IsLegacyNewerEvidenceForNormalization(
                        LatestAgentTaskEventJournal,
                        AgentSessionCheckpoint.TaskEventJournal))
                {
                    AgentSessionCheckpoint =
                        AgentSessionCheckpoint.CopyWithTaskEventJournalForNormalization(
                            LatestAgentTaskEventJournal)
                        ?? AgentSessionCheckpoint;
                }
                LatestAgentTaskEventJournal = null;
                changed = true;
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

        private bool RetainAgentTaskEventJournal(CopilotAgentTaskEventJournalSnapshot? journal)
        {
            if (!CopilotAgentTaskEventJournal.TryCreateSnapshot(journal, out var snapshot)
                || snapshot.Events.Count == 0)
            {
                return false;
            }
            if (LatestAgentTaskEventJournal?.IsStructurallyValid() == true)
            {
                if (CopilotAgentTaskEventJournal.AreEquivalent(
                        snapshot,
                        LatestAgentTaskEventJournal)
                    || !CopilotAgentTaskEventJournal.IsSameOrForwardBoundedSuccessor(
                        snapshot,
                        LatestAgentTaskEventJournal))
                {
                    return false;
                }
            }

            LatestAgentTaskEventJournal = snapshot;
            return true;
        }

        internal CopilotAgentSessionCheckpoint? CreateAgentContinuationCheckpoint()
        {
            var checkpoint = AgentSessionCheckpoint;
            if (checkpoint?.IsStructurallyValid() != true)
                return null;

            var currentJournal = CurrentAgentTaskEventJournal;
            if (currentJournal?.IsStructurallyValid() != true
                || CopilotAgentTaskEventJournal.AreEquivalent(
                    checkpoint.TaskEventJournal,
                    currentJournal))
            {
                return checkpoint;
            }

            return checkpoint.CopyWithTaskEventJournal(currentJournal) ?? checkpoint;
        }

        internal bool SetAgentSessionCheckpoint(CopilotAgentSessionCheckpoint? checkpoint)
        {
            return TrySetAgentSessionCheckpoint(checkpoint, out var changed) && changed;
        }

        internal bool TrySetAgentSessionCheckpoint(
            CopilotAgentSessionCheckpoint? checkpoint,
            out bool changed)
        {
            changed = false;
            var previousCheckpointJournal = AgentSessionCheckpoint?.TaskEventJournal;
            CopilotAgentSessionCheckpoint? candidate = null;
            if (checkpoint != null
                && (!CopilotAgentSessionCheckpoint.TryCreateSnapshot(checkpoint, out candidate)
                    || !CanAcceptAgentSessionCheckpoint(candidate.TaskEventJournal)))
            {
                return false;
            }

            changed = !CopilotAgentSessionCheckpoint.AreEquivalent(
                AgentSessionCheckpoint,
                candidate);
            if (changed)
                AgentSessionCheckpoint = candidate;
            var acceptedCheckpoint = AgentSessionCheckpoint;
            if (acceptedCheckpoint != null)
            {
                // A live checkpoint owns its journal. Independent evidence is only
                // needed when a terminal result is ahead of that checkpoint, which is
                // committed through CommitAgentRunState instead of this method.
                if (LatestAgentTaskEventJournal != null
                    && (CopilotAgentTaskEventJournal.AreEquivalent(
                            acceptedCheckpoint.TaskEventJournal,
                            LatestAgentTaskEventJournal)
                        || CopilotAgentTaskEventJournal.IsSameOrForwardBoundedSuccessor(
                            acceptedCheckpoint.TaskEventJournal,
                            LatestAgentTaskEventJournal)))
                {
                    LatestAgentTaskEventJournal = null;
                    changed = true;
                }
            }
            else
            {
                // Retire the checkpoint without losing its last durable event evidence.
                changed |= RetainAgentTaskEventJournal(previousCheckpointJournal);
            }
            return true;
        }

        private bool CanAcceptAgentSessionCheckpoint(
            CopilotAgentTaskEventJournalSnapshot candidateJournal)
        {
            var currentJournal = CurrentAgentTaskEventJournal;
            if (currentJournal?.IsStructurallyValid() != true)
                return true;

            return CopilotAgentTaskEventJournal.IsSameOrForwardBoundedSuccessor(
                candidateJournal,
                currentJournal);
        }

        internal bool CommitAgentRunState(
            CopilotAgentTaskEventJournalSnapshot? journal,
            CopilotAgentSessionCheckpoint? checkpoint)
        {
            return TryCommitAgentRunState(journal, checkpoint, out var changed) && changed;
        }

        internal bool TryCommitAgentRunState(
            CopilotAgentTaskEventJournalSnapshot? journal,
            CopilotAgentSessionCheckpoint? checkpoint,
            out bool changed)
        {
            changed = false;
            CopilotAgentTaskEventJournalSnapshot? journalSnapshot = null;
            if (journal != null
                && !CopilotAgentTaskEventJournal.TryCreateSnapshot(journal, out journalSnapshot))
            {
                return false;
            }
            CopilotAgentSessionCheckpoint? checkpointSnapshot = null;
            if (checkpoint != null
                && !CopilotAgentSessionCheckpoint.TryCreateSnapshot(checkpoint, out checkpointSnapshot))
            {
                return false;
            }
            if (!CanAcceptAgentRunState(journalSnapshot, checkpointSnapshot))
            {
                return false;
            }

            // The terminal run result is the authoritative journal for this commit. A
            // checkpoint may intentionally trail it and may even belong to the previous
            // run after cancellation/recovery. Rebase that recovery projection onto the
            // terminal journal atomically so persistence never keeps two journal owners.
            var committedCheckpoint = checkpointSnapshot;
            if (journalSnapshot?.Events.Count > 0)
            {
                if (checkpointSnapshot != null
                    && !CopilotAgentTaskEventJournal.AreEquivalent(
                        checkpointSnapshot.TaskEventJournal,
                        journalSnapshot))
                {
                    committedCheckpoint = checkpointSnapshot.CopyWithTaskEventJournal(journalSnapshot)
                        ?? throw new InvalidOperationException(
                            "A valid Copilot Agent checkpoint could not be rebased onto the authoritative terminal journal.");
                }

                if (committedCheckpoint == null
                    && !CopilotAgentTaskEventJournal.AreEquivalent(
                        LatestAgentTaskEventJournal,
                        journalSnapshot))
                {
                    LatestAgentTaskEventJournal = journalSnapshot;
                    changed = true;
                }
            }

            if (CopilotAgentSessionCheckpoint.AreEquivalent(
                    AgentSessionCheckpoint,
                    committedCheckpoint))
            {
                committedCheckpoint = AgentSessionCheckpoint;
            }
            else
            {
                changed = true;
                AgentSessionCheckpoint = committedCheckpoint;
            }
            if (committedCheckpoint != null)
            {
                if (LatestAgentTaskEventJournal != null)
                {
                    LatestAgentTaskEventJournal = null;
                    changed = true;
                }
            }
            return true;
        }

        private bool CanAcceptAgentRunState(
            CopilotAgentTaskEventJournalSnapshot? journal,
            CopilotAgentSessionCheckpoint? checkpoint)
        {
            var currentJournal = CurrentAgentTaskEventJournal;
            if (currentJournal?.IsStructurallyValid() != true)
                return true;

            var candidateJournal = journal?.Events?.Count > 0
                ? journal
                : checkpoint?.TaskEventJournal;
            if (candidateJournal?.IsStructurallyValid() != true)
                return false;

            return CopilotAgentTaskEventJournal.AreEquivalent(
                    candidateJournal,
                    currentJournal)
                || CopilotAgentTaskEventJournal.IsSameOrForwardBoundedSuccessor(
                    candidateJournal,
                    currentJournal);
        }

        internal bool CompleteOpenAgentRun(
            CopilotAgentStopReason stopReason,
            CopilotAgentControlIntent controlIntent = CopilotAgentControlIntent.None)
        {
            var expectedStopReason = controlIntent switch
            {
                CopilotAgentControlIntent.None => CopilotAgentStopReason.Interrupted,
                CopilotAgentControlIntent.Pause => CopilotAgentStopReason.Paused,
                CopilotAgentControlIntent.Cancel => CopilotAgentStopReason.Cancelled,
                _ => throw new ArgumentOutOfRangeException(nameof(controlIntent)),
            };
            if (stopReason != expectedStopReason)
            {
                throw new ArgumentException(
                    $"Stop reason {stopReason} does not match control intent {controlIntent}.",
                    nameof(stopReason));
            }

            var checkpoint = AgentSessionCheckpoint;
            // Independent journal evidence exists only when a terminal result is
            // ahead of the checkpoint, so the aggregate's resolved current journal
            // must win over a lagging checkpoint during late completion callbacks.
            var sourceJournal = CurrentAgentTaskEventJournal;
            var terminalJournal = CopilotAgentTaskEventJournal.CloseLatestOpenRun(
                sourceJournal,
                stopReason,
                controlIntent);
            if (terminalJournal == null)
            {
                return stopReason == CopilotAgentStopReason.Cancelled
                    && SetAgentSessionCheckpoint(null);
            }

            if (stopReason == CopilotAgentStopReason.Cancelled)
                return CommitAgentRunState(terminalJournal, checkpoint: null);

            var terminalCheckpoint = checkpoint?.CopyWithTaskEventJournal(terminalJournal);
            return CommitAgentRunState(
                terminalJournal,
                terminalCheckpoint ?? checkpoint);
        }
    }
}
