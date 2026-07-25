#pragma warning disable CA1863
using ColorVision.Scheduler.Data;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Scheduler
{
    public partial class ExecutionHistoryWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ExecutionHistoryWindow));
        private readonly string? _jobName;
        private readonly string? _groupName;
        private IReadOnlyList<JobExecutionRecord> _currentRecords = Array.Empty<JobExecutionRecord>();
        private CopilotDynamicContextSession? _copilotContextSession;
        private bool _isClosed;
        private int _pageIndex = 1;
        private int _pageCount;
        private const int PageSize = 100;
        private JobExecutionResultFilter _resultFilter = JobExecutionResultFilter.All;

        /// <summary>
        /// 查看所有任务的执行历史
        /// </summary>
        public ExecutionHistoryWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            TextBlockTaskName.Text = Properties.Resources.Sched_AllTasks;
            LoadData();
            Activated += ExecutionHistoryWindow_Activated;
            EnsureCopilotContextRegistered();
        }

        /// <summary>
        /// 查看指定任务的执行历史
        /// </summary>
        public ExecutionHistoryWindow(string jobName, string groupName)
        {
            InitializeComponent();
            this.ApplyCaption();
            _jobName = jobName;
            _groupName = groupName;
            TextBlockTaskName.Text = $"{jobName} ({groupName})";
            LoadData();
            Activated += ExecutionHistoryWindow_Activated;
            EnsureCopilotContextRegistered();
        }

        private void LoadData()
        {
            var dbManager = SchedulerDbManager.GetInstance();
            var result = dbManager.QueryExecutionHistory(new JobExecutionHistoryRequest(
                _jobName,
                _groupName,
                _resultFilter,
                _pageIndex,
                PageSize));

            if (!result.QuerySucceeded)
            {
                _currentRecords = Array.Empty<JobExecutionRecord>();
                ListViewHistory.ItemsSource = _currentRecords;
                _pageCount = 0;
                UpdateStats(null);
                UpdatePager();
                MessageBox.Show(
                    result.ErrorMessage ?? Properties.Resources.Sched_Error,
                    Properties.Resources.Sched_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                PublishCopilotContext();
                return;
            }

            _pageIndex = result.PageIndex;
            _pageCount = result.PageCount;
            _currentRecords = result.Records;
            ListViewHistory.ItemsSource = _currentRecords;
            UpdateStats(result);
            UpdatePager();
            PublishCopilotContext();
        }

        private void UpdateStats(JobExecutionHistoryPage? result)
        {
            TextBlockTotal.Text = (result?.TotalCount ?? 0).ToString();
            TextBlockSuccess.Text = (result?.SuccessCount ?? 0).ToString();
            TextBlockFailure.Text = (result?.FailureCount ?? 0).ToString();
            TextBlockAvgTime.Text = $"{result?.AverageExecutionTimeMs ?? 0}ms";
        }

        private void UpdatePager()
        {
            int displayPageCount = Math.Max(1, _pageCount);
            TextBlockPage.Text = string.Format(
                Properties.Resources.Sched_PageInfo,
                $"{_pageIndex}/{displayPageCount}");
            ButtonPreviousPage.IsEnabled = _pageCount > 0 && _pageIndex > 1;
            ButtonNextPage.IsEnabled = _pageCount > 0 && _pageIndex < _pageCount;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void Cleanup_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(Properties.Resources.Sched_ConfirmClear90, Properties.Resources.Sched_ConfirmClearTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                int deleted = SchedulerDbManager.GetInstance().CleanupOldRecords(90);
                MessageBox.Show(string.Format(Properties.Resources.Sched_Cleared, deleted), Properties.Resources.Sched_ClearDone, MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pageIndex > 1)
            {
                _pageIndex--;
                LoadData();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pageCount > 0 && _pageIndex < _pageCount)
            {
                _pageIndex++;
                LoadData();
            }
        }

        private void ComboBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            _resultFilter = ComboBoxFilter.SelectedIndex switch
            {
                1 => JobExecutionResultFilter.Succeeded,
                2 => JobExecutionResultFilter.Failed,
                _ => JobExecutionResultFilter.All,
            };
            _pageIndex = 1;
            LoadData();
        }

        private void ListViewHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void ExecutionHistoryWindow_Activated(object? sender, EventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void EnsureCopilotContextRegistered()
        {
            if (_copilotContextSession != null)
                return;

            try
            {
                QuartzSchedulerManager.GetInstance();
                _copilotContextSession = CopilotSchedulerContextHub.Shared.Register(
                    CaptureCopilotSchedulerSnapshotAsync,
                    typeof(ExecutionHistoryWindow).Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                Log.Warn("Could not register the execution-history Copilot context; the history window will continue to operate.", ex);
            }
        }

        private async Task<CopilotSchedulerContextSnapshot?> CaptureCopilotSchedulerSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isClosed)
                return null;
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return _isClosed ? null : CaptureCopilotSchedulerSnapshot();
                });
            }

            return CaptureCopilotSchedulerSnapshot();
        }

        private CopilotSchedulerContextSnapshot CaptureCopilotSchedulerSnapshot()
        {
            var manager = QuartzSchedulerManager.GetInstance();
            var selectedTask = _jobName != null && _groupName != null
                ? manager.TaskInfos.FirstOrDefault(task => task.JobName == _jobName && task.GroupName == _groupName)
                : null;
            return manager.CaptureCopilotSchedulerSnapshot(
                surface: "Scheduled task execution history",
                selectedTask: selectedTask,
                selectedTaskCount: selectedTask == null ? 0 : 1,
                historyRecords: _currentRecords,
                historyPageIndex: _pageIndex,
                historyFilter: GetCopilotHistoryFilter(),
                historyScope: _jobName == null ? "All scheduled tasks" : "Selected scheduled task",
                historyTaskName: _jobName ?? string.Empty,
                historyGroupName: _groupName ?? string.Empty,
                selectedHistoryRecord: ListViewHistory.SelectedItem as JobExecutionRecord);
        }

        private string GetCopilotHistoryFilter()
        {
            return _resultFilter switch
            {
                JobExecutionResultFilter.Succeeded => "Success only",
                JobExecutionResultFilter.Failed => "Failure only",
                _ => "All results",
            };
        }

        private void PublishCopilotContext()
        {
            if (_isClosed || _copilotContextSession?.IsCurrent != true || !IsActive)
                return;

            try
            {
                var snapshot = CaptureCopilotSchedulerSnapshot();
                var item = CopilotBusinessContextBuilder.BuildSchedulerContextItem(snapshot);
                CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                    CopilotSchedulerAgentExtension.SourceId,
                    item));
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not publish the active scheduler-history context to Copilot: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            Activated -= ExecutionHistoryWindow_Activated;
            var wasCurrent = _copilotContextSession?.IsCurrent == true;
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrent)
                CopilotLiveContextRegistry.Clear(CopilotSchedulerAgentExtension.SourceId);
            base.OnClosed(e);
        }
    }
}
