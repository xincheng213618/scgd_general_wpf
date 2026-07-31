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
            _stateSaveScheduler.RequestSave(immediate);
            OnPropertyChanged(nameof(HasAttachments));
        }

        private async Task PersistStateAndFlushAsync()
        {
            PersistState(immediate: true);
            try
            {
                await _stateSaveScheduler.FlushAsync();
            }
            catch (Exception)
            {
                // The scheduler has already published the persistence failure. Keep the completed
                // Agent turn usable in memory; a later state change or flush will retry the snapshot.
            }
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
                await _stateSaveScheduler.FlushAsync();
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

        private async Task SaveStateSnapshotAsync(CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            CopilotChatStateSnapshot snapshot;
            if (dispatcher == null
                || dispatcher.CheckAccess()
                || _stateStore is not IIncrementalCopilotChatStateStore incrementalStateStore)
            {
                snapshot = _stateStore.CaptureSnapshot(_state);
            }
            else
            {
                var beginCaptureOperation = dispatcher.InvokeAsync(
                    () => incrementalStateStore.BeginSnapshot(_state),
                    DispatcherPriority.Background,
                    cancellationToken);
                var capture = await beginCaptureOperation.Task.ConfigureAwait(false);
                while (!capture.IsComplete)
                {
                    var captureSliceOperation = dispatcher.InvokeAsync(
                        () => CaptureStateSnapshotSlice(capture),
                        DispatcherPriority.Background,
                        cancellationToken);
                    await captureSliceOperation.Task.ConfigureAwait(false);
                }

                snapshot = capture.Complete();
            }

            var serializedState = await Task.Run(() => _stateStore.Serialize(snapshot), cancellationToken).ConfigureAwait(false);
            await _stateStore.SaveSerializedAsync(serializedState, cancellationToken).ConfigureAwait(false);
        }

        private static void CaptureStateSnapshotSlice(CopilotChatStateSnapshotCapture capture)
        {
            var startedAt = Stopwatch.GetTimestamp();
            do
            {
                capture.CaptureNextChunk();
            }
            while (!capture.IsComplete && Stopwatch.GetElapsedTime(startedAt) < StateSnapshotUiSliceBudget);
        }

        private void Application_Exit(object? sender, ExitEventArgs e)
        {
            RestoreQueuedFollowUpsToDrafts();
            CopilotSteeringRecovery.RestorePendingToDrafts(_state);
            var scheduledRuns = _taskHost.ScheduledRuns;
            _taskHost.Shutdown();
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
            FinalizeUnstartedRunsForShutdown(scheduledRuns);
            _stateSaveScheduler.Dispose();
            PublishSelectedTaskEventJournal();
            try
            {
                if (_stateStore is not CopilotChatStateStore stateStore || !stateStore.IsStatePersistenceBlocked)
                    _stateStore.Save(_state);
            }
            catch (Exception exception)
            {
                ReportStatePersistenceError(exception);
            }
            finally
            {
                Dispose();
            }
        }

        private void RestoreQueuedFollowUpsToDrafts()
        {
            _state.QueuedFollowUpRecoveries ??= new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>();
            var persistedRunIds = _state.QueuedFollowUpRecoveries
                .Where(record => record != null)
                .Select(record => record.RunId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var queuedFollowUp in QueuedFollowUps.OrderBy(item => item.QueuePosition))
            {
                if (!persistedRunIds.Add(queuedFollowUp.RunId))
                    continue;

                AddQueuedFollowUpRecovery(queuedFollowUp);
            }
            CopilotQueuedFollowUpRecovery.RestoreToDrafts(_state);
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

            CancelAllConversationTitleGenerations();
            CancelAllAuxiliaryOperations();
            if (Application.Current != null)
                Application.Current.Exit -= Application_Exit;
            WorkspaceManager.ContentIdSelected -= WorkspaceManager_ContentIdSelected;
            CopilotLiveContextRegistry.CurrentChanged -= CopilotLiveContextRegistry_CurrentChanged;
            CopilotMcpConfirmationStore.Instance.ActionsChanged -= ConfirmationStore_ActionsChanged;
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged -= ConfirmationStore_ActionStatusChanged;
            WeakEventManager<CopilotAgentTaskHost, CopilotAgentTaskHostChangedEventArgs>.RemoveHandler(_taskHost, nameof(CopilotAgentTaskHost.Changed), TaskHost_Changed);
            CopilotBackgroundShellCommandRegistry.Shared.CommandCompleted -= BackgroundShellCommandRegistry_CommandCompleted;
            CopilotBackgroundShellCommandRegistry.Shared.OutputMonitorEvent -= BackgroundShellCommandRegistry_OutputMonitorEvent;

            Conversations.CollectionChanged -= Conversations_CollectionChanged;
            if (_selectedConversation != null)
            {
                _selectedConversation.Attachments.CollectionChanged -= Attachments_CollectionChanged;
                _selectedConversation.Messages.CollectionChanged -= Messages_CollectionChanged;
            }
            if (_selectedProfile != null)
                _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;

            _conversationSearchDebounceTimer.Stop();
            _conversationSearchDebounceTimer.Tick -= ConversationSearchDebounceTimer_Tick;
            _pendingActionExpiryTimer.Stop();
            _pendingActionFeedbackCts?.RequestCancellation();
            _pendingActionFeedbackCts = null;
            _compactConversationCts?.RequestCancellation();
            CancelComposerReferenceRefresh(resetSession: true);
            _stateSaveScheduler.Dispose();
            GC.SuppressFinalize(this);
        }


    }
}
