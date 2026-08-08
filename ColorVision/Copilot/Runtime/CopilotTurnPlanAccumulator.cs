using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotTurnPlanItemStatus
    {
        Pending,
        InProgress,
        Completed,
    }

    internal sealed record CopilotTurnPlanItemSnapshot(
        int Id,
        string Step,
        string Description,
        CopilotTurnPlanItemStatus Status)
    {
        public bool IsStructurallyValid() =>
            Id >= 0
            && !string.IsNullOrWhiteSpace(Step)
            && string.Equals(Step, Step.Trim(), StringComparison.Ordinal)
            && Step.Length <= CopilotAgentTaskItem.MaxTitleLength
            && !Step.Contains('\0')
            && Description != null
            && string.Equals(Description, Description.Trim(), StringComparison.Ordinal)
            && Description.Length <= CopilotAgentTaskItem.MaxDescriptionLength
            && !Description.Contains('\0')
            && Enum.IsDefined(Status);
    }

    internal sealed class CopilotTurnPlanSnapshot
    {
        public const int MaxExplanationLength = 1_000;

        public CopilotTurnPlanSnapshot(
            string mode,
            bool resumedFromCheckpoint,
            IEnumerable<CopilotTurnPlanItemSnapshot> items,
            string explanation = "")
        {
            Mode = mode ?? string.Empty;
            ResumedFromCheckpoint = resumedFromCheckpoint;
            Items = Array.AsReadOnly((items ?? throw new ArgumentNullException(nameof(items))).ToArray());
            Explanation = explanation ?? string.Empty;
        }

        public string Mode { get; }

        public bool ResumedFromCheckpoint { get; }

        public IReadOnlyList<CopilotTurnPlanItemSnapshot> Items { get; }

        public string Explanation { get; }

        public bool IsStructurallyValid()
        {
            return Mode is "plan" or "execute"
                && Items != null
                && Items.Count <= CopilotAgentTaskLedgerSnapshot.MaxItems
                && Items.All(item => item?.IsStructurallyValid() == true)
                && Items.Select(item => item.Id).Distinct().Count() == Items.Count
                && Explanation != null
                && Explanation.Length <= MaxExplanationLength
                && !Explanation.Contains('\0')
                && string.Equals(Explanation, Explanation.Trim(), StringComparison.Ordinal);
        }

        public CopilotAgentTaskLedgerSnapshot ToTaskLedgerSnapshot() => new()
        {
            Mode = Mode,
            ResumedFromCheckpoint = ResumedFromCheckpoint,
            Items = Items.Select(item => new CopilotAgentTaskItem
            {
                Id = item.Id,
                Title = item.Step,
                Description = item.Description,
                IsComplete = item.Status == CopilotTurnPlanItemStatus.Completed,
            }).ToArray(),
        };

        public static CopilotTurnPlanSnapshot FromTaskLedger(
            CopilotAgentTaskLedgerSnapshot taskLedger,
            string explanation = "")
        {
            ArgumentNullException.ThrowIfNull(taskLedger);
            var normalized = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = taskLedger.Mode,
                ResumedFromCheckpoint = taskLedger.ResumedFromCheckpoint,
                Items = (taskLedger.Items ?? Array.Empty<CopilotAgentTaskItem>())
                    .Where(item => item != null)
                    .Select(item => new CopilotAgentTaskItem
                    {
                        Id = item.Id,
                        Title = item.Title,
                        Description = item.Description,
                        IsComplete = item.IsComplete,
                    })
                    .ToArray(),
            };
            normalized.EnsureValid();
            var boundedExplanation = (explanation ?? string.Empty).Replace('\0', ' ').Trim();
            if (boundedExplanation.Length > MaxExplanationLength)
                boundedExplanation = boundedExplanation[..MaxExplanationLength].TrimEnd();

            return new CopilotTurnPlanSnapshot(
                normalized.Mode,
                normalized.ResumedFromCheckpoint,
                normalized.Items.Select(item => new CopilotTurnPlanItemSnapshot(
                    item.Id,
                    item.Title,
                    item.Description,
                    item.IsComplete
                        ? CopilotTurnPlanItemStatus.Completed
                        : CopilotTurnPlanItemStatus.Pending)),
                boundedExplanation);
        }

        public static bool AreEquivalent(CopilotTurnPlanSnapshot? left, CopilotTurnPlanSnapshot? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null
                || !string.Equals(left.Mode, right.Mode, StringComparison.Ordinal)
                || left.ResumedFromCheckpoint != right.ResumedFromCheckpoint
                || !string.Equals(left.Explanation, right.Explanation, StringComparison.Ordinal)
                || left.Items.Count != right.Items.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Items.Count; index++)
            {
                if (left.Items[index] != right.Items[index])
                    return false;
            }
            return true;
        }
    }

    internal sealed class CopilotTurnPlanAccumulator
    {
        private CopilotTurnPlanSnapshot? _current;

        public bool Observe(CopilotAgentEvent agentEvent, out CopilotTurnPlanSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (agentEvent.Type != CopilotAgentEventType.CheckpointUpdated || agentEvent.TaskLedger == null)
            {
                snapshot = null!;
                return false;
            }

            return Observe(agentEvent.TaskLedger, out snapshot);
        }

        public bool Observe(CopilotAgentTaskLedgerSnapshot taskLedger, out CopilotTurnPlanSnapshot snapshot)
        {
            snapshot = CopilotTurnPlanSnapshot.FromTaskLedger(taskLedger);
            if (CopilotTurnPlanSnapshot.AreEquivalent(_current, snapshot))
                return false;

            _current = snapshot;
            return true;
        }
    }
}
