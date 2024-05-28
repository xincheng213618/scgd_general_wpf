#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Scheduler
{

    public class AboutMsgExport :  IMenuItem
    {
        public string? OwnerGuid => "Help";
        public string? GuidId => "TaskViewerWindow";

        public int Order => 1000;

        public Visibility Visibility => Visibility.Visible;

        public string? Header => "TaskViewerWindow";

        public string? InputGestureText => "Ctrl + F1";

        public object? Icon => null;

        public RelayCommand Command => new(A => new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());

    }


    /// <summary>
    /// TaskViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TaskViewerWindow : Window
    {
        private ObservableCollection<SchedulerInfo> _taskInfos;

        public TaskViewerWindow()
        {
            InitializeComponent();
            _taskInfos = new ObservableCollection<SchedulerInfo>();
            ListViewTask.ItemsSource = _taskInfos;
            LoadTasks();
            TaskComboBox.ItemsSource = QuartzSchedulerManager.GetInstance().Jobs;
            // 订阅监听器事件
            var listener = QuartzSchedulerManager.GetInstance().Listener;
            if (listener != null)
            {
                listener.JobExecutedEvent += OnJobExecuted;
            }
        }

        private async void LoadTasks()
        {
            await GetScheduledTasks();
        }

        private async void OnJobExecuted(IJobExecutionContext context)
        {
            // 在主线程上更新 UI
            await Dispatcher.InvokeAsync(async () =>
            {
                await UpdateChangedTask(context);
            });
        }
        private async Task GetScheduledTasks()
        {
            var scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

            foreach (var jobKey in jobKeys)
            {
                var triggers = await scheduler.GetTriggersOfJob(jobKey);

                foreach (var trigger in triggers)
                {
                    var taskInfo = new SchedulerInfo
                    {
                        JobName = jobKey.Name,
                        GroupName = jobKey.Group,
                        NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString() ?? "N/A",
                        PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString() ?? "N/A"
                    };
                    _taskInfos.Add(taskInfo);
                }
            }
        }
        private async Task UpdateChangedTask(IJobExecutionContext context)
        {
            var scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            var jobKey = context.JobDetail.Key;
            var triggers = await scheduler.GetTriggersOfJob(jobKey);

            foreach (var trigger in triggers)
            {
                var updatedTaskInfo = new SchedulerInfo
                {
                    JobName = jobKey.Name,
                    GroupName = jobKey.Group,
                    NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString() ?? "N/A",
                    PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString() ?? "N/A"
                };

                var existingTaskInfo = _taskInfos.FirstOrDefault(t => t.JobName == updatedTaskInfo.JobName && t.GroupName == updatedTaskInfo.GroupName);

                if (existingTaskInfo != null)
                {
                    // 更新现有任务信息
                    existingTaskInfo.NextFireTime = updatedTaskInfo.NextFireTime;
                    existingTaskInfo.PreviousFireTime = updatedTaskInfo.PreviousFireTime;
                }
                else
                {
                    // 添加新任务信息
                    _taskInfos.Add(updatedTaskInfo);
                }
            }
        }


        private void CreateTaskButton_Click(object sender, RoutedEventArgs e)
        {
            var jobName = JobNameTextBox.Text;
            var groupName = GroupNameTextBox.Text;
            var cronExpression = CronExpressionTextBox.Text;

            if (string.IsNullOrWhiteSpace(jobName) || string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(cronExpression))
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }
            var taskInfo = QuartzSchedulerManager.GetInstance().CreateJob(jobName, groupName, cronExpression, TaskComboBox.Text);
            _taskInfos.Add(taskInfo.Result);
        }
    }
}
