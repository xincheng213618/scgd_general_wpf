using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl.Matchers;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Scheduler
{
    public partial class TaskViewerWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TaskViewerWindow));
        private readonly ISchedulerService _schedulerService;
        private readonly Task _initializationTask;
        private readonly HashSet<SchedulerInfo> _trackedTasks = new();
        private TaskExecutionListener? _listener;
        private CopilotDynamicContextSession? _copilotContextSession;
        private int _copilotPublishQueued;
        private bool _isClosed;
        private bool _isOperating;
        private bool _loaded;

        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; }
        public static QuartzSchedulerManager QuartzSchedulerManager => QuartzSchedulerManager.GetInstance();

        public TaskViewerWindow() : this(QuartzSchedulerManager, QuartzSchedulerManager.InitializationTask) { }

        internal TaskViewerWindow(ISchedulerService schedulerService, Task? initializationTask = null)
        {
            _schedulerService = schedulerService;
            _initializationTask = initializationTask ?? Task.CompletedTask;
            TaskInfos = schedulerService.TaskInfos;
            InitializeComponent();
            MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 48);
            DataContext = schedulerService;
            ListViewTask.ItemsSource = TaskInfos;
            ListViewTask.ContextMenu = CreateTaskMenu(null);
            CreateButton.IsEnabled = false;
            TaskInfos.CollectionChanged += TaskInfos_CollectionChanged;
            RefreshTaskSubscriptions();
            UpdateOverview();
            Activated += TaskViewerWindow_Activated;
            EnsureCopilotContextRegistered();
            this.ApplyCaption();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                await _initializationTask;
                if (_isClosed)
                    return;
                _listener = _schedulerService.Listener;
                if (_listener != null)
                    _listener.JobExecutedEvent += OnJobExecuted;
                if (_schedulerService.Scheduler != null)
                    await GetScheduledTasks();
                CreateButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowOperationError(ex);
            }
        }

        private void UpdateOverview()
        {
            if (_isClosed)
                return;
            OverviewText.Text = string.Format(CultureInfo.CurrentCulture, Properties.Resources.Sched_Overview,
                TaskInfos.Count, TaskInfos.Count(task => task.Status == SchedulerStatus.Running),
                TaskInfos.Count(task => task.Status == SchedulerStatus.Paused));
        }

        private void OnJobExecuted(IJobExecutionContext context) => _ = RefreshExecutedJobAsync(context);

        private async Task RefreshExecutedJobAsync(IJobExecutionContext context)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_isClosed)
                        return;
                    var task = TaskInfos.FirstOrDefault(task => task.JobName == context.JobDetail.Key.Name && task.GroupName == context.JobDetail.Key.Group);
                    if (task != null)
                    {
                        // A one-shot trigger may already be gone; the execution context is authoritative.
                        task.NextFireTime = context.NextFireTimeUtc?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                        task.PreviousFireTime = context.FireTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
                    }
                    QueueCopilotContextPublish();
                });
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to refresh task state after executing {context.JobDetail.Key}.", ex);
            }
        }

        private void ListViewTask_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private async Task GetScheduledTasks()
        {
            var scheduler = _schedulerService.Scheduler;
            foreach (var key in await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()))
            {
                var triggers = await scheduler.GetTriggersOfJob(key);
                if (_isClosed)
                    return;
                foreach (var trigger in triggers)
                {
                    var task = TaskInfos.FirstOrDefault(task => task.JobName == key.Name && task.GroupName == key.Group);
                    if (task == null)
                    {
                        task = new SchedulerInfo { JobName = key.Name, GroupName = key.Group };
                        TaskInfos.Add(task);
                    }
                    task.NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                    task.PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                }
            }
        }

        private SchedulerInfo? ResolveTask(object sender)
        {
            var task = (sender as FrameworkElement)?.DataContext as SchedulerInfo ?? ListViewTask.SelectedItem as SchedulerInfo;
            if (task != null)
                ListViewTask.SelectedItem = task;
            return task;
        }

        private ContextMenu CreateTaskMenu(SchedulerInfo? task)
        {
            var menu = new ContextMenu { DataContext = task };
            void Add(string header, RoutedEventHandler handler)
            {
                var item = new MenuItem { Header = header };
                item.Click += handler;
                menu.Items.Add(item);
            }
            Add(Properties.Resources.Sched_EditTask, MenuEdit_Click);
            Add(Properties.Resources.Sched_ViewProps, MenuView_Click);
            menu.Items.Add(new Separator());
            Add(Properties.Resources.Sched_PauseTask, MenuPause_Click);
            Add(Properties.Resources.Sched_ResumeTask, MenuResume_Click);
            Add(Properties.Resources.Sched_RunNow, MenuTrigger_Click);
            menu.Items.Add(new Separator());
            Add(Properties.Resources.Sched_ExecHistoryMenu, MenuHistory_Click);
            Add(Properties.Resources.Sched_DeleteTask, MenuDelete_Click);
            return menu;
        }

        private void ListViewTask_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(ListViewTask, source) is ListViewItem item)
                ListViewTask.SelectedItem = item.DataContext;
        }

        private void TaskMore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && ResolveTask(sender) is SchedulerInfo task)
            {
                button.ContextMenu = CreateTaskMenu(task);
                OpenButtonMenu_Click(button, e);
            }
        }

        private void OpenButtonMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void CreateTaskButton_Click(object sender, RoutedEventArgs e) =>
            new CreateTask(_schedulerService, _initializationTask) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is not SchedulerInfo info)
                return;
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            var draft = JsonConvert.DeserializeObject<SchedulerInfo>(JsonConvert.SerializeObject(info, settings), settings);
            if (draft != null)
                new CreateTask(_schedulerService, _initializationTask) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, SchedulerInfo = draft }.ShowDialog();
        }

        private void MenuView_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info)
                new PropertyEditorWindow(info, PropertyEditorEditMode.Transactional) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private async Task RunOperationAsync(Func<Task> operation)
        {
            if (_isOperating)
                return;
            _isOperating = true;
            CreateButton.IsEnabled = false;
            OperationError.Visibility = Visibility.Collapsed;
            try { await operation(); }
            catch (Exception ex) { ShowOperationError(ex); }
            finally
            {
                _isOperating = false;
                if (!_isClosed)
                    CreateButton.IsEnabled = true;
            }
        }

        private void ShowOperationError(Exception ex)
        {
            Log.Error("Scheduler window operation failed.", ex);
            if (_isClosed)
                return;
            OperationError.Text = ex.Message;
            OperationError.Visibility = Visibility.Visible;
        }

        private async void PauseAll_Click(object sender, RoutedEventArgs e) => await RunOperationAsync(_schedulerService.PauseAll);
        private async void ResumeAll_Click(object sender, RoutedEventArgs e) => await RunOperationAsync(_schedulerService.ResumeAll);

        private async void TogglePause_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info)
                await RunOperationAsync(() => info.Status == SchedulerStatus.Paused
                    ? _schedulerService.ResumeJob(info.JobName, info.GroupName)
                    : _schedulerService.StopJob(info.JobName, info.GroupName));
        }

        private async void MenuPause_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info)
                await RunOperationAsync(() => _schedulerService.StopJob(info.JobName, info.GroupName));
        }

        private async void MenuResume_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info)
                await RunOperationAsync(() => _schedulerService.ResumeJob(info.JobName, info.GroupName));
        }

        private async void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info &&
                MessageBox.Show(this, $"确定要删除任务 {info.JobName}({info.GroupName}) 吗？", Properties.Resources.Sched_ConfirmDelete,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await RunOperationAsync(() => _schedulerService.RemoveJob(info.JobName, info.GroupName));
        }

        private async void MenuTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info)
                await RunOperationAsync(() => _schedulerService.Scheduler.TriggerJob(new JobKey(info.JobName, info.GroupName)));
        }

        private void ViewAllHistory_Click(object sender, RoutedEventArgs e) =>
            new ExecutionHistoryWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();

        private void MenuHistory_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveTask(sender) is SchedulerInfo info)
                new ExecutionHistoryWindow(info.JobName, info.GroupName) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private void ExportCSV_Click(object sender, RoutedEventArgs e) =>
            ExportTasks("CSV 文件|*.csv", $"Tasks_{DateTime.Now:yyyyMMdd_HHmmss}.csv", () => SchedulerTaskExporter.Csv(TaskInfos));

        private void ExportJSON_Click(object sender, RoutedEventArgs e) =>
            ExportTasks("JSON 文件|*.json", $"Tasks_{DateTime.Now:yyyyMMdd_HHmmss}.json", () => SchedulerTaskExporter.Json(TaskInfos));

        private void ExportReport_Click(object sender, RoutedEventArgs e) =>
            ExportTasks("文本报告|*.txt|Markdown 报告|*.md", $"TaskReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt", () => SchedulerTaskExporter.Report(TaskInfos));

        private void ExportTasks(string filter, string fileName, Func<string> format)
        {
            try
            {
                var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
                if (dialog.ShowDialog(this) != true)
                    return;
                File.WriteAllText(dialog.FileName, format(), Encoding.UTF8);
                MessageBox.Show(this, dialog.FileName, Properties.Resources.Sched_ExportSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { ShowOperationError(ex); }
        }

        private void TaskViewerWindow_Activated(object? sender, EventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void TaskInfos_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshTaskSubscriptions();
            UpdateOverview();
            QueueCopilotContextPublish();
        }

        private void RefreshTaskSubscriptions()
        {
            var currentTasks = TaskInfos.ToHashSet();
            foreach (var task in _trackedTasks.Where(task => !currentTasks.Contains(task)).ToArray())
            {
                task.PropertyChanged -= SchedulerInfo_PropertyChanged;
                _trackedTasks.Remove(task);
            }

            foreach (var task in currentTasks.Where(task => _trackedTasks.Add(task)))
                task.PropertyChanged += SchedulerInfo_PropertyChanged;
        }

        private void SchedulerInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SchedulerInfo.Status))
                Dispatcher.InvokeAsync(UpdateOverview);
            QueueCopilotContextPublish();
        }

        private void EnsureCopilotContextRegistered()
        {
            if (_copilotContextSession != null || _schedulerService is not global::ColorVision.Scheduler.QuartzSchedulerManager)
                return;

            try
            {
                _copilotContextSession = CopilotSchedulerContextHub.Shared.Register(
                    CaptureCopilotSchedulerSnapshotAsync,
                    typeof(TaskViewerWindow).Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                Log.Warn("Could not register the Task Viewer Copilot context; the scheduler window will continue to operate.", ex);
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
            return ((QuartzSchedulerManager)_schedulerService).CaptureCopilotSchedulerSnapshot(
                surface: "Scheduled task viewer",
                selectedTask: ListViewTask.SelectedItem as SchedulerInfo,
                selectedTaskCount: ListViewTask.SelectedItems.Count);
        }

        private void QueueCopilotContextPublish()
        {
            if (_isClosed || Interlocked.Exchange(ref _copilotPublishQueued, 1) != 0)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref _copilotPublishQueued, 0);
                PublishCopilotContext();
            }));
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
                Log.Debug($"Could not publish the active Task Viewer context to Copilot: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            Activated -= TaskViewerWindow_Activated;
            TaskInfos.CollectionChanged -= TaskInfos_CollectionChanged;
            if (_listener != null)
                _listener.JobExecutedEvent -= OnJobExecuted;
            foreach (var task in _trackedTasks)
                task.PropertyChanged -= SchedulerInfo_PropertyChanged;
            _trackedTasks.Clear();

            var wasCurrent = _copilotContextSession?.IsCurrent == true;
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrent)
                CopilotLiveContextRegistry.Clear(CopilotSchedulerAgentExtension.SourceId);

            base.OnClosed(e);
        }
    }
}
