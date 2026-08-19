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
        private void PersistState(bool immediate = false)
        {
            if (_stateStore is CopilotChatStateStore stateStore && stateStore.IsStatePersistenceBlocked)
                return;

            PublishSelectedTaskEventJournal();
            _statePersistenceCoordinator.RequestSave(immediate);
            OnPropertyChanged(nameof(HasAttachments));
        }

        private async Task PersistStateAndFlushAsync()
        {
            PersistState(immediate: true);
            try
            {
                await _statePersistenceCoordinator.FlushAsync();
            }
            catch (Exception)
            {
                // The scheduler has already published the persistence failure. Keep the completed
                // Agent turn usable in memory; a later state change or flush will retry the snapshot.
            }
        }

        private async Task FlushStatePersistenceBarrierAsync()
        {
            if (_stateStore is CopilotChatStateStore stateStore
                && stateStore.IsStatePersistenceBlocked)
            {
                throw new InvalidOperationException(
                    "Copilot state persistence is blocked by a newer state schema.");
            }

            PublishSelectedTaskEventJournal();
            _statePersistenceCoordinator.RequestSave(immediate: true);
            await _statePersistenceCoordinator.FlushAsync();
        }

        private bool CanRetryStatePersistence() => HasStatePersistenceNotice
            && !_isRetryingStatePersistence
            && Volatile.Read(ref _disposeState) == 0;

        private async Task RetryStatePersistenceAsync()
        {
            if (!CanRetryStatePersistence())
                return;

            _isRetryingStatePersistence = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                PersistState(immediate: true);
                await _statePersistenceCoordinator.FlushAsync();
                if (Volatile.Read(ref _disposeState) == 1)
                    return;

                UpdateStatePersistenceNotice(string.Empty, string.Empty);
                LocalCommandResultTitle = "会话已保存";
                LocalCommandResultText = "当前 Copilot 会话状态已经重新写入磁盘。";
            }
            finally
            {
                _isRetryingStatePersistence = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void Application_Exit(object? sender, ExitEventArgs e)
        {
            try
            {
                CopilotMcpServer.Instance.ShutdownAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Copilot MCP server shutdown failed: {CopilotAgentTraceEntry.Sanitize(exception.Message)}");
            }
            PreserveQueuedFollowUpsForRestart();
            CopilotSteeringRecovery.RestorePendingToDrafts(_state);
            var scheduledRuns = _taskHost.ScheduledRuns;
            _taskHost.Shutdown();
            try
            {
                EndOpenSessionsForShutdownAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Copilot SessionEnd hook shutdown failed open: {CopilotAgentTraceEntry.Sanitize(exception.Message)}");
            }
            try
            {
                CopilotToolExecutionHookBackgroundScheduler.Shared.ShutdownAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Copilot async tool hook shutdown failed: {CopilotAgentTraceEntry.Sanitize(exception.Message)}");
            }
            CopilotBackgroundShellCommandRegistry.Shared.CommandCompleted -= BackgroundShellCommandRegistry_CommandCompleted;
            CopilotBackgroundShellCommandRegistry.Shared.OutputMonitorEvent -= BackgroundShellCommandRegistry_OutputMonitorEvent;
            try
            {
                CopilotBackgroundShellCommandRegistry.Shared.ShutdownAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Copilot background process shutdown failed: {exception}");
            }
            CopilotShellCommandOutputArchiveRegistry.Shared.Dispose();
            CopilotToolOutputArchiveRegistry.Shared.Dispose();
            FinalizeUnstartedRunsForShutdown(scheduledRuns);
            try
            {
                PublishSelectedTaskEventJournal();
                _statePersistenceCoordinator.SaveSynchronouslyAndStop();
            }
            finally
            {
                Dispose();
            }
        }

        private void PreserveQueuedFollowUpsForRestart()
        {
            _followUpQueue.BeginShutdown();
        }

        private void FinalizeUnstartedRunsForShutdown(IReadOnlyList<CopilotHostedAgentRun> scheduledRuns)
        {
            foreach (var run in scheduledRuns.Where(run => !run.HasStarted))
            {
                var conversation = Conversations.FirstOrDefault(candidate => string.Equals(candidate.Id, run.ConversationId, StringComparison.Ordinal));
                var assistantMessage = conversation?.Messages.LastOrDefault(message => !message.IsUser
                    && (message.IsResponsePending || message.IsThinkingInProgress));
                if (conversation == null || assistantMessage == null)
                    continue;

                CopilotHostedTurnCompletion.CompleteBeforeStartCancellation(assistantMessage);
                UpdateConversationMetadata(conversation, touch: true);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) == 1)
                return;

            _conversationTitleCoordinator.Dispose();
            _followUpQueue.Changed -= FollowUpQueue_Changed;
            CancelAllAuxiliaryOperations();
            if (Application.Current != null)
                Application.Current.Exit -= Application_Exit;
            WorkspaceManager.ContentIdSelected -= WorkspaceManager_ContentIdSelected;
            CopilotLiveContextRegistry.CurrentChanged -= CopilotLiveContextRegistry_CurrentChanged;
            _approvalCoordinator.PendingActionsInvalidated -= ApprovalCoordinator_PendingActionsInvalidated;
            _approvalCoordinator.ActionTransitioned -= ApprovalCoordinator_ActionTransitioned;
            _approvalCoordinator.Dispose();
            CopilotAgentSkillCatalog.CatalogChanged -= AgentSkillCatalog_CatalogChanged;
            WeakEventManager<CopilotAgentTaskHost, CopilotAgentTaskHostChangedEventArgs>.RemoveHandler(_taskHost, nameof(CopilotAgentTaskHost.Changed), TaskHost_Changed);
            CopilotBackgroundShellCommandRegistry.Shared.CommandCompleted -= BackgroundShellCommandRegistry_CommandCompleted;
            CopilotBackgroundShellCommandRegistry.Shared.OutputMonitorEvent -= BackgroundShellCommandRegistry_OutputMonitorEvent;

            Conversations.CollectionChanged -= Conversations_CollectionChanged;
            if (SelectedConversation != null)
            {
                SelectedConversation.Attachments.CollectionChanged -= Attachments_CollectionChanged;
                SelectedConversation.Messages.CollectionChanged -= Messages_CollectionChanged;
            }
            if (SelectedProfile != null)
                SelectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;

            _conversationSearchDebounceTimer.Stop();
            _conversationSearchDebounceTimer.Tick -= ConversationSearchDebounceTimer_Tick;
            _pendingActionExpiryTimer.Stop();
            _pendingActionFeedbackCts?.RequestCancellation();
            _pendingActionFeedbackCts = null;
            _compactConversationCts?.RequestCancellation();
            CancelComposerReferenceRefresh(resetSession: true);
            _statePersistenceCoordinator.Dispose();
            GC.SuppressFinalize(this);
        }


    }
}
