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
        private const int PageSize = 100;
        private string _filter = Properties.Resources.Sched_All;

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
            List<JobExecutionRecord> records;

            if (_jobName != null && _groupName != null)
            {
                records = dbManager.QueryRecords(_jobName, _groupName, _pageIndex, PageSize);
            }
            else
            {
                records = dbManager.QueryAllRecords(_pageIndex, PageSize);
            }

            // 应用筛选
            if (_filter == Properties.Resources.Sched_SuccessFilter)
                records = records.Where(r => r.Success).ToList();
            else if (_filter == Properties.Resources.Sched_FailFilter)
                records = records.Where(r => !r.Success).ToList();

            _currentRecords = records;
            ListViewHistory.ItemsSource = _currentRecords;
            TextBlockPage.Text = string.Format(Properties.Resources.Sched_PageInfo, _pageIndex);

            // 更新统计
            UpdateStats();
            PublishCopilotContext();
        }

        private void UpdateStats()
        {
            var dbManager = SchedulerDbManager.GetInstance();

            if (_jobName != null && _groupName != null)
            {
                var stats = dbManager.GetTaskStats(_jobName, _groupName);
                TextBlockTotal.Text = stats.RunCount.ToString();
                TextBlockSuccess.Text = stats.SuccessCount.ToString();
                TextBlockFailure.Text = stats.FailureCount.ToString();
                TextBlockAvgTime.Text = $"{stats.AvgMs}ms";
            }
            else
            {
                // 全部任务的统计用当前页数据简单汇总
                var records = ListViewHistory.ItemsSource as List<JobExecutionRecord>;
                if (records != null && records.Count > 0)
                {
                    TextBlockTotal.Text = records.Count.ToString();
                    TextBlockSuccess.Text = records.Count(r => r.Success).ToString();
                    TextBlockFailure.Text = records.Count(r => !r.Success).ToString();
                    TextBlockAvgTime.Text = $"{(long)records.Average(r => r.ExecutionTimeMs)}ms";
                }
            }
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
            _pageIndex++;
            LoadData();
        }

        private void ComboBoxFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (ComboBoxFilter.SelectedItem is ComboBoxItem item)
            {
                _filter = item.Content?.ToString() ?? Properties.Resources.Sched_All;
                _pageIndex = 1;
                LoadData();
            }
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
            if (_filter == Properties.Resources.Sched_SuccessFilter)
                return "Success only";
            if (_filter == Properties.Resources.Sched_FailFilter)
                return "Failure only";
            return "All results";
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
