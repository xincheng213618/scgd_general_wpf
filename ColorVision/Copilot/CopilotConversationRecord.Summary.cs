using Newtonsoft.Json;
using System;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConversationRecord
    {
        [JsonIgnore]
        public string UpdatedLabel
        {
            get
            {
                var recencyAt = RecencyAt == default ? UpdatedAt : RecencyAt;
                return recencyAt.Date == DateTime.Today ? recencyAt.ToString("HH:mm") : recencyAt.ToString("M/d");
            }
        }

        [JsonIgnore]
        public bool HasDraft => !string.IsNullOrWhiteSpace(DraftText);

        [JsonIgnore]
        public bool HasComposerStash => ComposerStash?.HasContent == true;

        [JsonIgnore]
        public string ConversationListPreviewText =>
            !string.IsNullOrWhiteSpace(SearchMatchPreviewText)
                ? SearchMatchPreviewText
                : HasDraft
                    ? $"草稿：{BuildPreview(DraftText, 42)}"
                    : HasComposerStash
                        ? BuildComposerStashPreview(ComposerStash!)
                        : PreviewText;

        [JsonIgnore]
        public string SearchMatchPreviewText
        {
            get => _searchMatchPreviewText;
            private set
            {
                if (SetProperty(ref _searchMatchPreviewText, value ?? string.Empty))
                    OnPropertyChanged(nameof(ConversationListPreviewText));
            }
        }
        private string _searchMatchPreviewText = string.Empty;

        internal void SetSearchMatchPreview(string? preview) =>
            SearchMatchPreviewText = preview;

        [JsonIgnore]
        public string PinLabel => IsPinned ? CopilotUiText.PinnedLabel : string.Empty;

        [JsonIgnore]
        public string PinMenuText => IsPinned ? CopilotUiText.UnpinMenuText : CopilotUiText.PinMenuText;

        [JsonIgnore]
        public bool HasBranchOrigin => BranchOrigin?.IsStructurallyValid(Id) == true;

        [JsonIgnore]
        public string BranchLabel => HasBranchOrigin ? "分支" : string.Empty;

        [JsonIgnore]
        public bool HasGoal => Goal?.IsStructurallyValid() == true;

        [JsonIgnore]
        public string GoalDisplayText => Goal == null
            ? string.Empty
            : $"{(IsGoalContinuationDeferred
                ? "目标待接管"
                : CopilotConversationGoalStateText.FormatDisplayLabel(Goal.State))} · {BuildPreview(Goal.Objective, 120)}";

        [JsonIgnore]
        public string GoalToolTip => Goal == null
            ? string.Empty
            : (IsGoalContinuationDeferred
                    ? "活动目标已从源会话带入分支；下一条显式 Agent 任务将接管目标生命周期，完成后恢复正常自动续作。"
                    : CopilotConversationGoalStateText.FormatDescription(Goal.State))
                + Environment.NewLine
                + Goal.Objective
                + Environment.NewLine
                + $"{Goal.TurnCount:N0} 轮 · {Goal.EvaluationCount:N0} 次独立评估 · "
                + (Goal.HasTokenBudget
                    ? $"{Goal.TokensUsed:N0} / {Goal.TokenBudget:N0} Token"
                    : $"{Goal.TokensUsed:N0} Token")
                + " · 累计 "
                + CopilotConversationGoalUsageText.FormatElapsed(Goal.TimeUsedSeconds)
                + (string.IsNullOrWhiteSpace(Goal.LastEvaluationReason)
                    ? string.Empty
                    : Environment.NewLine + "最近判断：" + Goal.LastEvaluationReason)
                + Environment.NewLine
                + "目标约束完成判定，但不授权写入、工具调用、审批复用或外部副作用。";

        [JsonIgnore]
        public string AgentRunStatusLabel
        {
            get => _agentRunStatusLabel;
            internal set
            {
                if (SetProperty(ref _agentRunStatusLabel, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasAgentRunStatus));
            }
        }
        private string _agentRunStatusLabel = string.Empty;

        [JsonIgnore]
        public bool HasAgentRunStatus => !string.IsNullOrWhiteSpace(AgentRunStatusLabel);

        public void RefreshSummary()
        {
            var firstUserMessage = Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var generatedTitle = firstUserMessage == null ? CopilotUiText.NewConversationTitle : BuildPreview(firstUserMessage.Content, 24);
            if (!HasCustomTitle || string.IsNullOrWhiteSpace(Title))
                Title = generatedTitle;

            var lastVisibleMessage = Messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content));
            if (lastVisibleMessage != null)
            {
                PreviewText = BuildPreview(lastVisibleMessage.Content, 42);
                return;
            }

            PreviewText = Attachments.Count > 0
                ? CopilotUiText.FormatAttachmentMountedCount(Attachments.Count)
                : CopilotUiText.EmptyConversationPreview;
        }

        public void SetCustomTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        internal static bool TryNormalizeCustomTitle(string? title, out string normalizedTitle)
        {
            normalizedTitle = NormalizeText(title);
            return normalizedTitle.Length is > 0 and <= MaximumTitleCharacters;
        }

        public void SetGeneratedTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public static CopilotConversationRecord CreateEmpty(string profileId, string profileDisplayName)
        {
            var now = DateTime.Now;
            return new CopilotConversationRecord
            {
                HasCustomTitle = false,
                ProfileId = profileId,
                ProfileDisplayName = profileDisplayName,
                Title = CopilotUiText.NewConversationTitle,
                PreviewText = CopilotUiText.EmptyConversationPreview,
                CreatedAt = now,
                UpdatedAt = now,
                RecencyAt = now,
            };
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string BuildComposerStashPreview(CopilotComposerStash stash)
        {
            var textPreview = BuildPreview(stash.Text, 34);
            if (!string.IsNullOrWhiteSpace(textPreview))
                return $"已暂存：{textPreview}";

            return $"已暂存：{stash.Attachments.Count:N0} 个附件";
        }
    }
}
