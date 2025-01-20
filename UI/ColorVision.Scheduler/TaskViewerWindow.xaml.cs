using ColorVision.Themes;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// TaskViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TaskViewerWindow : Window
    {
        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; }

        public static QuartzSchedulerManager QuartzSchedulerManager => QuartzSchedulerManager.GetInstance();

        public TaskViewerWindow()
        {
            InitializeComponent();
            this.DataContext = QuartzSchedulerManager;
            TaskInfos = QuartzSchedulerManager.GetInstance().TaskInfos;
            ListViewTask.ItemsSource = TaskInfos;
            LoadTasks();
            // 订阅监听器事件
            var listener = QuartzSchedulerManager.GetInstance().Listener;
            if (listener != null)
            {
                listener.JobExecutedEvent += OnJobExecuted;
            }
            this.ApplyCaption();
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
            var scheduler = QuartzSchedulerManager.Scheduler;
            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

            foreach (var jobKey in jobKeys)
            {
                var triggers = await scheduler.GetTriggersOfJob(jobKey);

                foreach (var trigger in triggers)
                {
                    var existingTaskInfo = TaskInfos.FirstOrDefault(t => t.JobName == jobKey.Name && t.GroupName == jobKey.Group);
                    if (existingTaskInfo != null)
                    {
                        existingTaskInfo.NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                        existingTaskInfo.PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                    }
                    else
                    {
                        var taskInfo = new SchedulerInfo
                        {
                            JobName = jobKey.Name,
                            GroupName = jobKey.Group,
                            NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A",
                            PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A"
                        };
                        TaskInfos.Add(taskInfo);
                    }

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
                    NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A",
                    PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A"
                };

                var existingTaskInfo = TaskInfos.FirstOrDefault(t => t.JobName == updatedTaskInfo.JobName && t.GroupName == updatedTaskInfo.GroupName);
                if (existingTaskInfo != null)
                {
                    // 更新现有任务信息
                    existingTaskInfo.NextFireTime = updatedTaskInfo.NextFireTime;
                    existingTaskInfo.PreviousFireTime = updatedTaskInfo.PreviousFireTime;
                }
                else
                {
                    // 添加新任务信息
                    TaskInfos.Add(updatedTaskInfo);
                }
            }
        }


        private void CreateTaskButton_Click(object sender, RoutedEventArgs e)
        {
            CreateTask createTask = new CreateTask() { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation =WindowStartupLocation.CenterOwner };
            createTask.ShowDialog();
        }
    }
}
