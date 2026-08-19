using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnCheckpointLifecycleState(
        CopilotAgentSessionCheckpoint? LatestCheckpoint,
        bool Ready)
    {
        public static CopilotTurnCheckpointLifecycleState Empty => new(null, false);

        public CopilotTurnCheckpointLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            return agentEvent.Type switch
            {
                CopilotAgentEventType.CheckpointUpdated => ObserveUpdated(agentEvent),
                CopilotAgentEventType.CheckpointReady => ObserveReady(),
                CopilotAgentEventType.Completed => ObserveCompleted(),
                _ => this,
            };
        }

        public void ValidateCompletion(CopilotAgentRunResult agentRunResult)
        {
            ArgumentNullException.ThrowIfNull(agentRunResult);
            var finalCheckpoint = agentRunResult.SessionCheckpoint;
            if (finalCheckpoint == null)
                return;
            if (!IsValidCheckpoint(finalCheckpoint))
                throw new InvalidOperationException("Copilot Agent completed with an invalid session checkpoint.");
            if (LatestCheckpoint != null)
            {
                RequireMatchingIdentity(LatestCheckpoint, finalCheckpoint);
                RequireMonotonicJournal(LatestCheckpoint, finalCheckpoint, "final checkpoint");
                if (finalCheckpoint.UpdatedAtUtc < LatestCheckpoint.UpdatedAtUtc)
                    throw new InvalidOperationException("Copilot Agent final checkpoint moved backwards in time.");
            }
        }

        private CopilotTurnCheckpointLifecycleState ObserveUpdated(CopilotAgentEvent agentEvent)
        {
            var checkpoint = agentEvent.SessionCheckpoint;
            if (!IsValidCheckpoint(checkpoint))
                throw new InvalidOperationException("Copilot Agent emitted an invalid checkpoint update.");
            if (!IsValidTaskLedger(agentEvent.TaskLedger))
                throw new InvalidOperationException("Copilot Agent checkpoint update carried an invalid task ledger.");

            if (LatestCheckpoint != null)
            {
                RequireMatchingIdentity(LatestCheckpoint, checkpoint!);
                RequireMonotonicJournal(LatestCheckpoint, checkpoint!, "checkpoint update");
                if (checkpoint!.UpdatedAtUtc < LatestCheckpoint.UpdatedAtUtc)
                    throw new InvalidOperationException("Copilot Agent checkpoint update moved backwards in time.");
            }

            return this with { LatestCheckpoint = checkpoint };
        }

        private CopilotTurnCheckpointLifecycleState ObserveReady()
        {
            if (LatestCheckpoint == null)
                throw new InvalidOperationException("Copilot Agent marked its checkpoint ready before publishing one.");
            if (Ready)
                throw new InvalidOperationException("Copilot Agent marked its checkpoint ready more than once.");
            return this with { Ready = true };
        }

        private CopilotTurnCheckpointLifecycleState ObserveCompleted()
        {
            if (LatestCheckpoint != null && !Ready)
                throw new InvalidOperationException("Copilot Agent completed before its published checkpoint became ready.");
            return this;
        }

        private static bool IsValidCheckpoint(CopilotAgentSessionCheckpoint? checkpoint) =>
            checkpoint?.IsStructurallyValid() == true
            && checkpoint.UpdatedAtUtc != default
            && checkpoint.UpdatedAtUtc.Offset == TimeSpan.Zero;

        private static bool IsValidTaskLedger(CopilotAgentTaskLedgerSnapshot? taskLedger)
        {
            if (taskLedger == null
                || taskLedger.Mode is not ("plan" or "execute")
                || taskLedger.Items == null
                || taskLedger.Items.Count > CopilotAgentTaskLedgerSnapshot.MaxItems)
            {
                return false;
            }

            return taskLedger.Items.All(item => item != null
                    && item.Id >= 0
                    && !string.IsNullOrWhiteSpace(item.Title)
                    && string.Equals(item.Title, item.Title.Trim(), StringComparison.Ordinal)
                    && item.Title.Length <= CopilotAgentTaskItem.MaxTitleLength
                    && !item.Title.Contains('\0')
                    && item.Description != null
                    && string.Equals(item.Description, item.Description.Trim(), StringComparison.Ordinal)
                    && item.Description.Length <= CopilotAgentTaskItem.MaxDescriptionLength
                    && !item.Description.Contains('\0'))
                && taskLedger.Items.Select(item => item.Id).Distinct().Count() == taskLedger.Items.Count;
        }

        private static void RequireMatchingIdentity(
            CopilotAgentSessionCheckpoint expected,
            CopilotAgentSessionCheckpoint actual)
        {
            if (!string.Equals(expected.ProfileKey, actual.ProfileKey, StringComparison.Ordinal)
                || expected.CapabilityCatalogRevision != actual.CapabilityCatalogRevision
                || expected.ToolSurfaceVersion != actual.ToolSurfaceVersion
                || expected.EnvironmentVersion != actual.EnvironmentVersion
                || !string.Equals(expected.EnvironmentFingerprint, actual.EnvironmentFingerprint, StringComparison.OrdinalIgnoreCase)
                || expected.HookSurfaceVersion != actual.HookSurfaceVersion
                || !string.Equals(expected.HookSurfaceFingerprint, actual.HookSurfaceFingerprint, StringComparison.OrdinalIgnoreCase)
                || expected.ProjectInstructionSurfaceVersion != actual.ProjectInstructionSurfaceVersion
                || !string.Equals(expected.ProjectInstructionSurfaceFingerprint, actual.ProjectInstructionSurfaceFingerprint, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.TaskIntentText, actual.TaskIntentText, StringComparison.Ordinal)
                || !expected.AvailableToolNames.SequenceEqual(actual.AvailableToolNames, StringComparer.OrdinalIgnoreCase)
                || expected.Capabilities.Count != actual.Capabilities.Count
                || !expected.Capabilities.Zip(actual.Capabilities).All(pair =>
                    string.Equals(pair.First.Id, pair.Second.Id, StringComparison.OrdinalIgnoreCase)
                    && pair.First.Revision == pair.Second.Revision
                    && string.Equals(pair.First.Fingerprint, pair.Second.Fingerprint, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Copilot Agent checkpoint identity changed during the turn.");
            }
        }

        private static void RequireMonotonicJournal(
            CopilotAgentSessionCheckpoint expected,
            CopilotAgentSessionCheckpoint actual,
            string checkpointKind)
        {
            if (!CopilotAgentTaskEventJournal.IsSameOrForwardBoundedSuccessor(
                    actual.TaskEventJournal,
                    expected.TaskEventJournal))
            {
                throw new InvalidOperationException(
                    $"Copilot Agent {checkpointKind} journal did not advance monotonically during the turn.");
            }
        }
    }
}
