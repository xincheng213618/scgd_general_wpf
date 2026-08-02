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
        internal bool TryStopCurrentReplyFromKeyboard()
        {
            if (!IsViewingActiveRun
                || ActiveHostedRunInteraction.PrimaryAction == CopilotHostedRunPrimaryAction.None)
            {
                return false;
            }

            return StopCurrentReply();
        }

        internal void ShowConversationRewindPointsFromKeyboard()
        {
            if (!CanShowConversationRewindShortcut)
                return;

            var command = CopilotLocalCommandCatalog.FindExact("/rewind");
            if (command != null)
                RewindConversation(command, string.Empty);
        }

        private void StopTaskFromCommand(CopilotLocalCommand command)
        {
            var activeRun = ActiveHostedRun;
            if (!IsViewingActiveRun || activeRun == null)
            {
                ShowLocalCommandResult(command, "当前会话没有正在运行的任务。");
                return;
            }

            var previousState = activeRun.State;
            if (!StopCurrentReply())
            {
                ShowLocalCommandResult(
                    command,
                    previousState == CopilotHostedRunState.CancelRequested
                        ? "当前任务已在等待取消完成。"
                        : "当前任务暂时无法停止；请查看 /tasks 或使用任务控制按钮。");
                return;
            }

            ShowLocalCommandResult(
                command,
                previousState == CopilotHostedRunState.PauseRequested
                    ? "已把当前 Agent 的暂停请求升级为取消；本轮不会继续执行。"
                    : activeRun.IsAgent
                        ? "已请求安全暂停当前 Agent；若没有可恢复 checkpoint，将取消当前轮次。"
                        : "已请求取消当前聊天响应。");
        }

        private bool StopCurrentReply()
        {
            var selectedRun = SelectedHostedRun;
            if (selectedRun?.State == CopilotHostedRunState.Queued)
                return _taskHost.RequestCancel(selectedRun.Id);

            var activeRun = ActiveHostedRun;
            if (!IsViewingActiveRun || activeRun == null)
                return false;

            if (activeRun.State == CopilotHostedRunState.CancelRequested)
                return false;
            if (activeRun.State == CopilotHostedRunState.PauseRequested)
                return _taskHost.RequestCancel(activeRun.Id);

            // Match Codex's single-stop interaction: keep recovery state when a
            // safe checkpoint exists, otherwise cancel the in-flight turn.
            if (activeRun.IsAgent && _taskHost.RequestPause(activeRun.Id))
                return true;

            return _taskHost.RequestCancel(activeRun.Id);
        }

        private void OpenSettings(CopilotSettingsPage initialPage = CopilotSettingsPage.Models)
        {
            if (IsBusy)
                return;

            var window = new CopilotSettingsWindow(initialPage)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            var result = window.ShowDialog();
            if (result != true && !window.HasAppliedChanges)
                return;

            ReloadStateFromConfig(window.ActiveProfileId);
        }

        private void OpenSettingsFromCommand(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotSettingsCommand.TryResolvePage(arguments, out var page))
            {
                ShowLocalCommandResult(command, CopilotSettingsCommand.Usage);
                return;
            }

            DismissLocalCommandResult();
            OpenSettings(page);
        }

        private void ReloadStateFromConfig(string? preferredProfileId)
        {
            var preferredConversationId = SelectedConversation?.Id ?? _state.ActiveConversationId;

            if (_config.EnsureInitialized())
                PersistConfig();

            var requestedProfile = CopilotChatStateProfileReconciler.Apply(_state, _config, preferredProfileId);

            OnPropertyChanged(nameof(Profiles));
            OnPropertyChanged(nameof(Conversations));
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(CanSelectProfile));
            RefreshLocalCommandSuggestions();
            RefreshMcpStatus();

            var conversation = Conversations.FirstOrDefault(item => item.Id == preferredConversationId)
                ?? Conversations.FirstOrDefault();

            SelectConversation(conversation, persist: false, preferredProfileId: requestedProfile?.Id);
            PersistState(immediate: true);
            RefreshComposerTokenEstimate();
        }

        private CopilotConversationHistoryLimits ResolveConversationHistoryLimits(CopilotProfileConfig? profile)
        {
            return CopilotConversationRequestBuilder.ResolveHistoryLimits(
                _config.AgentDefaults.ContextWindowTokens,
                profile?.MaxTokens ?? CopilotProfileConfig.DefaultMaxTokens);
        }
    }
}
