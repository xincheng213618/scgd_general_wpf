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

        public IReadOnlyList<CopilotReasoningOption> SelectedProfileReasoningOptions => CopilotReasoningCapabilities.GetOptions(SelectedProfile);

        public string SelectedProfileReasoningLabel => CopilotReasoningCapabilities.GetLabel(CopilotReasoningCapabilities.GetEffectiveMode(SelectedProfile));

        public string SelectedProfileReasoningToolTip => CopilotReasoningCapabilities.GetToolTip(SelectedProfile);

        public bool HasConfigurableReasoning => CopilotReasoningCapabilities.HasConfigurableReasoning(SelectedProfile);

        public void SetSelectedProfileReasoningMode(CopilotReasoningMode mode)
        {
            var profile = SelectedProfile;
            if (profile == null || !HasConfigurableReasoning)
                return;

            var normalized = CopilotReasoningCapabilities.Normalize(profile.VendorType, mode);
            if (profile.ReasoningMode == normalized)
                return;

            profile.ReasoningMode = normalized;
            PersistConfig();
            RefreshSelectedProfileReasoningState();
        }
        private string _inputText = string.Empty;
        private CopilotAgentSkillReference? _pendingAgentSkillReference;

        public string InputPlaceholder => IsPromptHistorySearchOpen
            ? $"搜索{PromptHistorySearchScopeLabel}的可见历史请求"
            : IsEditingMessage
            ? $"修改后按 {ComposerSubmitShortcutLabel} 重新发送"
            : IsViewingActiveRun
                ? IsAnsweringUserQuestion
                    ? $"输入问题答案并按 {ComposerSubmitShortcutLabel}；也可直接选择上方选项"
                    : ActiveHostedRun?.State switch
                    {
                        CopilotHostedRunState.PauseRequested => "任务正在暂停 · 当前输入会保留到任务结束",
                        CopilotHostedRunState.CancelRequested => "任务正在取消 · 当前输入会保留到任务结束",
                        _ when IsAgentRequestActive => $"{ComposerSubmitShortcutLabel} {DefaultFollowUpActionLabel} · Tab {AlternateFollowUpActionLabel} · Ctrl+Enter 立即接管 · @ 关联",
                        _ => "正在生成回复 · 可使用 /status",
                    }
                : ResolveComposerRequestMode() == CopilotAgentMode.Plan
                    ? "计划模式 · 输入任务；只读分析，不执行修改"
                : IsConversationEmpty ? "随心输入 · @ 关联 · / 或 $ 命令" : "要求后续变更 · @ 关联 · / 或 $ 命令";

        private string ComposerSubmitShortcutLabel => UseMultilineComposer ? "Shift+Enter" : "Enter";

        private string DefaultFollowUpActionLabel =>
            DefaultFollowUpBehavior == CopilotFollowUpBehavior.Queue ? "排队" : "调整";

        private string AlternateFollowUpActionLabel =>
            DefaultFollowUpBehavior == CopilotFollowUpBehavior.Queue ? "调整" : "排队";

        private string ResolveFollowUpShortcut(CopilotFollowUpBehavior behavior) =>
            behavior == DefaultFollowUpBehavior ? ComposerSubmitShortcutLabel : "Tab";

        public bool IsEditingMessage => !string.IsNullOrWhiteSpace(_editingConversationId)
            && !string.IsNullOrWhiteSpace(_editingUserMessageId);

        public bool CanOpenExpandedComposerEditor =>
            !IsPromptHistorySearchOpen && !IsComposerReferenceMentionActive;

        public string EditingMessageStatusText => "正在编辑上一条请求；发送后将替换原回复";

        public bool IsInputEmpty => string.IsNullOrWhiteSpace(InputText);

        public IReadOnlyList<CopilotLocalCommand> LocalCommandSuggestions
        {
            get
            {
                if (IsEditingMessage || IsPromptHistorySearchOpen)
                    return Array.Empty<CopilotLocalCommand>();

                var composerContext = ResolveLocalCommandComposerContext();
                if (!CopilotLocalCommandAvailabilityPolicy.CanShowSuggestions(composerContext))
                    return Array.Empty<CopilotLocalCommand>();

                var input = (InputText ?? string.Empty).TrimStart();
                if (input.Length == 0 || input[0] is not '/' and not '$')
                    return Array.Empty<CopilotLocalCommand>();
                return CopilotLocalCommandCatalog.Suggest(
                    input,
                    DiscoverComposerSkills(),
                    Profiles,
                    SelectedProfile,
                    composerContext,
                    SelectedConversation);
            }
        }

        public string LocalCommandSuggestionHeader => ResolveLocalCommandComposerContext()
            == CopilotLocalCommandComposerContext.ActiveRun
                ? "运行中可用命令或 Skill"
                : "/ 或 $ 命令";

        public bool HasLocalCommandSuggestions => LocalCommandSuggestions.Count > 0;

        public int SelectedLocalCommandSuggestionIndex
        {
            get => _selectedLocalCommandSuggestionIndex;
            set => SetProperty(ref _selectedLocalCommandSuggestionIndex, value);
        }

        public bool TryNavigateLocalCommandSuggestion(bool previous)
        {
            var suggestions = LocalCommandSuggestions;
            if (suggestions.Count == 0)
            {
                SelectedLocalCommandSuggestionIndex = -1;
                return false;
            }

            SelectedLocalCommandSuggestionIndex = CopilotSuggestionSelection.Move(
                SelectedLocalCommandSuggestionIndex,
                suggestions.Count,
                previous);
            return true;
        }

        public bool TryCompleteLocalCommand(CopilotLocalCommand? command = null)
        {
            var suggestions = LocalCommandSuggestions;
            command ??= GetSelectedLocalCommandSuggestion(suggestions);
            if (command == null)
                return false;

            InputText = command.CompletionText;
            SetPendingAgentSkillReference(command.AgentSkillReference);
            return true;
        }

        internal bool TryCompleteLocalCommandForSubmission()
        {
            var suggestions = LocalCommandSuggestions;
            var command = GetSelectedLocalCommandSuggestion(suggestions);
            if (command == null)
                return true;

            InputText = command.CompletionText;
            SetPendingAgentSkillReference(command.AgentSkillReference);
            return !command.RequiresMoreInputAfterCompletion;
        }

        private void RefreshPendingAgentSkillReference(string text)
        {
            if (_pendingAgentSkillReference != null
                && !_pendingAgentSkillReference.IsExplicitlyInvokedBy(text))
            {
                SetPendingAgentSkillReference(null);
            }
        }

        private void SetPendingAgentSkillReference(
            CopilotAgentSkillReference? reference,
            bool synchronizeDraft = true)
        {
            var normalized = reference?.IsStructurallyValid() == true
                ? reference.CreateSnapshot()
                : null;
            if (SkillReferencesEqual(_pendingAgentSkillReference, normalized))
                return;

            _pendingAgentSkillReference = normalized;
            if (!synchronizeDraft || SelectedConversation == null)
                return;

            SelectedConversation.DraftAgentSkillReference = normalized?.CreateSnapshot();
            _statePersistenceCoordinator.RequestSave();
        }

        private CopilotAgentSkillReference? ResolvePendingAgentSkillReference(string text)
        {
            return _pendingAgentSkillReference?.IsExplicitlyInvokedBy(text) == true
                ? _pendingAgentSkillReference.CreateSnapshot()
                : null;
        }

        private static bool SkillReferencesEqual(
            CopilotAgentSkillReference? left,
            CopilotAgentSkillReference? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.SkillFilePath, right.SkillFilePath, StringComparison.OrdinalIgnoreCase);
        }

        private CopilotLocalCommand? GetSelectedLocalCommandSuggestion(
            IReadOnlyList<CopilotLocalCommand> suggestions)
        {
            var selectedIndex = CopilotSuggestionSelection.Normalize(
                SelectedLocalCommandSuggestionIndex,
                suggestions.Count);
            if (selectedIndex < 0)
                return null;

            SelectedLocalCommandSuggestionIndex = selectedIndex;
            return suggestions[selectedIndex];
        }

        private void RefreshLocalCommandSuggestions()
        {
            var suggestions = LocalCommandSuggestions;
            var selectedIndex = CopilotSuggestionSelection.Reset(suggestions.Count);

            OnPropertyChanged(nameof(LocalCommandSuggestions));
            SelectedLocalCommandSuggestionIndex = selectedIndex;
            OnPropertyChanged(nameof(HasLocalCommandSuggestions));
        }

        private void AgentSkillCatalog_CatalogChanged(object? sender, EventArgs e)
        {
            if (Volatile.Read(ref _disposeState) == 1)
            {
                return;
            }

            var input = InputText ?? string.Empty;
            var refreshLocalCommands = input.TrimStart().StartsWith('$');
            var refreshReferences = _currentCodexConfigOptions.ConfiguredMentionsV2Enabled
                && CopilotComposerReferenceCatalog.TryParseMention(input, out _);
            if (!refreshLocalCommands && !refreshReferences)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                _ = dispatcher.BeginInvoke(() =>
                {
                    if (Volatile.Read(ref _disposeState) == 0)
                    {
                        if (refreshLocalCommands)
                            RefreshLocalCommandSuggestions();
                        if (refreshReferences)
                            RefreshComposerReferenceSuggestions();
                    }
                }, DispatcherPriority.Background);
                return;
            }

            if (refreshLocalCommands)
                RefreshLocalCommandSuggestions();
            if (refreshReferences)
                RefreshComposerReferenceSuggestions();
        }
    }
}
