#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Copilot.Mcp;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private void RefreshComposerTokenEstimate()
        {
            RefreshConversationContextState();
            RefreshProviderRateLimitStatus();

            string summary;
            string details;

            if (IsBusy)
            {
                summary = "Waiting for token usage from the API...";
                details = BuildPendingComposerTokenDetails();
            }
            else if (SelectedConversation?.LastUsage.HasAny == true)
            {
                summary = BuildActualUsageSummary(SelectedConversation.LastUsage);
                details = BuildActualUsageDetails(SelectedConversation, SelectedConversation.LastUsage);
            }
            else if (SelectedProfile == null)
            {
                summary = "No model selected";
                details = "Select or configure a model before sending. This panel shows only token usage returned by the API.";
            }
            else if (SelectedConversation?.Messages.Count > 0)
            {
                summary = "The last request did not return token usage";
                details = BuildUnavailableUsageDetails(SelectedConversation);
            }
            else
            {
                summary = "Token usage appears after sending";
                details = BuildIdleComposerTokenDetails();
            }

            ComposerTokenSummary = summary;
            ComposerTokenDetails = details;
            OnPropertyChanged(nameof(PrimaryActionToolTip));
        }

        private void RefreshProviderRateLimitStatus()
        {
            var presentation = CopilotProviderRateLimitStatusPresenter.Create(
                CopilotProviderRateLimitTracker.GetSnapshot(SelectedProfile?.Id));
            IsProviderRateLimitStatusVisible = presentation.IsVisible;
            ProviderRateLimitStatusLabel = presentation.Label;
            ProviderRateLimitStatusToolTip = presentation.ToolTip;
            IsProviderRateLimitUnderPressure = presentation.IsUnderPressure;
        }

        private void RefreshConversationContextState()
        {
            var limits = ResolveConversationHistoryLimits(SelectedProfile);
            var selection = CopilotConversationRequestBuilder.CaptureHistorySelection(
                SelectedConversation,
                limits);
            IsConversationContextReduced = selection.WasReduced;
            ConversationContextCompactionToolTip = selection.WasReduced
                ? $"当前模型窗口只会发送 {selection.Messages.Length:N0}/{selection.SourceMessageCount:N0} 条历史消息、"
                    + $"{selection.RetainedCharacters:N0}/{selection.SourceCharacters:N0} 个字符。点击生成延续摘要；完整聊天记录不会删除。"
                : string.Empty;

            var usage = CopilotConversationAutoCompactionPolicy.Measure(
                SelectedConversation,
                limits,
                InputText);
            var presentation = CopilotConversationContextUsagePresenter.Create(
                usage,
                _config.AgentDefaults.AutoCompactConversationHistory,
                _config.AgentDefaults.AutoCompactThresholdPercent,
                _config.AgentDefaults.AutoCompactInstructions.Length);
            ConversationContextUsageLabel = presentation.Label;
            ConversationContextUsageToolTip = presentation.ToolTip;
            IsConversationContextUnderPressure = presentation.IsUnderPressure;
        }

        private void InvalidateChatAttachmentTokenEstimate()
        {
            RefreshComposerTokenEstimate();
        }

        private string BuildActualUsageSummary(CopilotTokenUsage usage)
        {
            var cacheSummary = usage.CachedInputTokens.HasValue
                ? $" · cached {CopilotTokenUsage.FormatCount(usage.EffectiveCachedInputTokens)} ({usage.CachedInputPercentage:0.#}%)"
                : string.Empty;
            return $"Last request: input {CopilotTokenUsage.FormatCount(usage.InputTokens)} · output {CopilotTokenUsage.FormatCount(usage.OutputTokens)} · total {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}{cacheSummary}";
        }

        private string BuildActualUsageDetails(CopilotConversationRecord conversation, CopilotTokenUsage usage)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Model: {ResolveUsageModelLabel(conversation)}");
            builder.AppendLine($"Input tokens: {CopilotTokenUsage.FormatCount(usage.InputTokens)}");
            builder.AppendLine($"Output tokens: {CopilotTokenUsage.FormatCount(usage.OutputTokens)}");
            builder.AppendLine($"Total tokens: {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}");
            builder.AppendLine(usage.CachedInputTokens.HasValue
                ? $"Cached input tokens: {CopilotTokenUsage.FormatCount(usage.EffectiveCachedInputTokens)} ({usage.CachedInputPercentage:0.#}% of input)"
                : "Cached input tokens: unavailable from this provider");
            builder.AppendLine();
            builder.Append("Note: cached input is a subset of input tokens and is not added to the total again.");

            return builder.ToString().TrimEnd();
        }

        private string BuildPendingComposerTokenDetails()
        {
            var builder = new StringBuilder();
            AppendComposerRequestPreview(builder);
            builder.AppendLine();
            builder.Append("Note: only API-returned usage is shown. It will refresh when this request completes.");
            return builder.ToString();
        }

        private string BuildUnavailableUsageDetails(CopilotConversationRecord conversation)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Model: {ResolveUsageModelLabel(conversation)}");
            builder.AppendLine();
            builder.Append("Note: local estimates are disabled. The last request did not return a usage field, so input and output token counts are unavailable.");
            return builder.ToString();
        }

        private string BuildIdleComposerTokenDetails()
        {
            var builder = new StringBuilder();
            AppendComposerRequestPreview(builder);
            builder.AppendLine();
            builder.Append("Note: if the API returns usage after sending, this panel will show real input, output, and total token counts.");
            return builder.ToString();
        }

        private string BuildComposerRequestPreview()
        {
            var builder = new StringBuilder();
            AppendComposerRequestPreview(builder);
            return builder.ToString().TrimEnd();
        }

        private void AppendComposerRequestPreview(StringBuilder builder)
        {
            builder.AppendLine($"Model: {SelectedProfile?.DisplayLabel ?? "No model selected"}");
            builder.AppendLine($"Prompt: {BuildPromptSummary()}");
            builder.AppendLine($"Persistent goal: {BuildConversationGoalSummary()}");
            builder.AppendLine($"Conversation context: {BuildConversationContextSummary()}");
            builder.AppendLine($"Attachments: {BuildAttachmentSummary()}");
            builder.AppendLine($"Window context: {BuildWindowContextSummary()}");

            if (IsControlModeVisible)
                builder.AppendLine($"Control: {McpStatusLabel}");
        }

        private string BuildPromptSummary()
        {
            var text = (InputText ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text)
                ? "Empty"
                : $"{text.Length} characters";
        }

        private string BuildConversationContextSummary()
        {
            var selection = CopilotConversationRequestBuilder.CaptureHistorySelection(SelectedConversation, ResolveConversationHistoryLimits(SelectedProfile));
            if (selection.SourceMessageCount == 0)
                return "None";

            var retained = $"{selection.Messages.Length} message(s), {selection.RetainedCharacters:N0} characters";
            return selection.WasReduced
                ? $"{retained} retained from {selection.SourceMessageCount} message(s), {selection.SourceCharacters:N0} characters"
                : retained;
        }

        private string BuildConversationGoalSummary()
        {
            var goal = SelectedConversation?.Goal;
            if (goal?.IsStructurallyValid() != true)
                return "None";

            var state = CopilotConversationGoalStateText.FormatEnglish(goal.State);
            var tokenProgress = goal.HasTokenBudget
                ? $"{goal.TokensUsed:N0} / {goal.TokenBudget:N0} tokens"
                : $"{goal.TokensUsed:N0} tokens";
            return $"{state}, {goal.Objective.Length:N0} characters, {goal.TurnCount:N0} turn(s), {tokenProgress}"
                + (goal.IsActive ? " (completion constraint only)" : string.Empty);
        }

        private string BuildAttachmentSummary()
        {
            if (Attachments.Count == 0)
                return "None";

            var fileCount = Attachments.Count(item => item.Type == CopilotAttachmentType.File);
            var imageCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Image);
            var webCount = Attachments.Count(item => item.Type == CopilotAttachmentType.WebPage);
            var contextCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Context);
            var parts = new List<string>();

            AddAttachmentCount(parts, fileCount, "file");
            AddAttachmentCount(parts, imageCount, "image");
            AddAttachmentCount(parts, webCount, "web");
            AddAttachmentCount(parts, contextCount, "context");

            return $"{Attachments.Count} total ({string.Join(", ", parts)})";
        }

        private string BuildWindowContextSummary()
        {
            if (_currentLiveContext == null)
                return "None available";

            return IsCurrentLiveContextAttached
                ? "Attached snapshot plus live summary"
                : "Live summary available for this request";
        }

        private static void AddAttachmentCount(List<string> parts, int count, string label)
        {
            if (count <= 0)
                return;

            parts.Add(count == 1 ? $"1 {label}" : $"{count} {label}s");
        }

        private string ResolveUsageModelLabel(CopilotConversationRecord conversation)
        {
            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;

            if (!string.IsNullOrWhiteSpace(SelectedProfile?.DisplayLabel))
                return SelectedProfile.DisplayLabel;

            return "Unnamed model";
        }

        private CopilotProfileConfig? ResolveProfile(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            foreach (var profile in Profiles)
            {
                if (string.Equals(profile.Id, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }
    }
}
