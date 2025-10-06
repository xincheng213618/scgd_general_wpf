using ColorVision.Themes;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

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

            // 添加右键菜单
            var menuEdit = new MenuItem { Header = "编辑任务" };
            menuEdit.Click += MenuEdit_Click;
            var menuView = new MenuItem { Header = "查看属性" };
            menuView.Click += MenuView_Click;
            var menuPause = new MenuItem { Header = "暂停任务" };
            menuPause.Click += MenuPause_Click;
            var menuResume = new MenuItem { Header = "继续任务" };
            menuResume.Click += MenuResume_Click;
            var menuDelete = new MenuItem { Header = "删除任务" };
            menuDelete.Click += MenuDelete_Click;
            var menuTrigger = new MenuItem { Header = "立即执行" };
            menuTrigger.Click += MenuTrigger_Click;
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(menuEdit);
            contextMenu.Items.Add(menuView);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuPause);
            contextMenu.Items.Add(menuResume);
            contextMenu.Items.Add(menuTrigger);
            contextMenu.Items.Add(menuDelete);
            ListViewTask.ContextMenu = contextMenu;
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

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                // 深拷贝一份用于编辑
                var editInfo = JsonConvert.DeserializeObject<SchedulerInfo>(JsonConvert.SerializeObject(info, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                var win = new CreateTask { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, SchedulerInfo = editInfo };
                if (win.ShowDialog() == true)
                {
                    // 编辑完成后更新
                    QuartzSchedulerManager.UpdateJob(editInfo);
                }
            }
        }

        private void MenuView_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                var win = new ColorVision.UI.PropertyEditorWindow(info, false) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                win.ShowDialog();
            }
        }

        private async void MenuPause_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                await QuartzSchedulerManager.StopJob(info.JobName, info.GroupName);
                info.Status = SchedulerStatus.Paused;
            }
        }

        private async void MenuResume_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                await QuartzSchedulerManager.ResumeJob(info.JobName, info.GroupName);
                info.Status = SchedulerStatus.Ready;
            }
        }

        private async void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                await QuartzSchedulerManager.RemoveJob(info.JobName, info.GroupName);
                TaskInfos.Remove(info);
            }
        }

        private async void MenuTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                var jobKey = new Quartz.JobKey(info.JobName, info.GroupName);
                await QuartzSchedulerManager.Scheduler.TriggerJob(jobKey);
            }
        }
    }
}
