using ColorVision.Scheduler;
using ColorVision.Themes;
using Quartz;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Resources = ColorVision.Scheduler.Properties.Resources;

namespace ColorVision.UI.Tests;

// Injected services never load saved tasks, start Quartz, or touch devices.
public class SchedulerWindowTests
{
    [Fact]
    public void CardActions_OperateOnClickedTaskInsteadOfPreviousSelection()
    {
        OnUi(() =>
        {
            var service = new SchedulerWindowTestService();
            var first = service.AddTask("first");
            var second = service.AddTask("second");
            var window = new TaskViewerWindow(service);
            try
            {
                Show(window);
                window.ListViewTask.SelectedItem = first;
                var pause = Descendants<Button>(window).Single(button => ReferenceEquals(button.DataContext, second) && Equals(button.Content, Resources.Sched_PauseTask));
                pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(SchedulerStatus.Ready, first.Status);
                Assert.Equal(SchedulerStatus.Paused, second.Status);
                Assert.Same(second, window.ListViewTask.SelectedItem);
                Drain();
                Assert.Equal(Resources.Sched_ResumeTask, pause.Content);
                pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(SchedulerStatus.Ready, second.Status);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void CreateForm_PreservesUserNamesAcrossTaskTypesAndUpdatesPlanPreview()
    {
        OnUi(() =>
        {
            var window = new CreateTask(new SchedulerWindowTestService());
            try
            {
                Show(window);
                window.TaskComboBox.SelectedValue = typeof(SchedulerWindowTestService.FirstJob);
                window.TaskNameTextBox.Text = "operator name";
                window.SchedulerInfo.GroupName = "inspection";
                window.TaskComboBox.SelectedValue = typeof(SchedulerWindowTestService.SecondJob);
                Assert.Equal("operator name", window.SchedulerInfo.JobName);
                Assert.Equal("inspection", window.SchedulerInfo.GroupName);
                window.SchedulerInfo.RepeatMode = JobRepeatMode.Multiple;
                window.SchedulerInfo.RepeatCount = 2;
                window.SchedulerInfo.Interval = TimeSpan.FromMinutes(5);
                Drain();
                Assert.Contains("00:05:00", window.PlanSummaryText.Text);
                Assert.Contains("3", window.PlanSummaryText.Text);
                window.SchedulerInfo.Mode = JobExecutionMode.Calendar;
                Drain();
                Assert.Equal(Resources.Sched_EveryCalendarDay, window.PlanSummaryText.Text);
                Assert.False(window.RepeatModeComboBox.IsVisible);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("interval")]
    [InlineData("repeat")]
    [InlineData("timeout")]
    public void InvalidText_DoesNotSubmitPreviousParsedValue(string field)
    {
        OnUi(() =>
        {
            var service = new SchedulerWindowTestService();
            var window = new CreateTask(service);
            try
            {
                Show(window);
                window.TaskComboBox.SelectedValue = typeof(SchedulerWindowTestService.FirstJob);
                window.SchedulerInfo.RepeatMode = JobRepeatMode.Multiple;
                window.AdvancedSettings.IsExpanded = true;
                Drain();
                TextBox input = field switch
                {
                    "interval" => window.IntervalTextBox,
                    "repeat" => window.RepeatCountTextBox,
                    _ => window.TimeoutTextBox
                };
                input.Text = "invalid";
                Assert.False(window.SaveAsync().GetAwaiter().GetResult());
                Assert.Equal(0, service.CreateCalls);
                Assert.Equal(Visibility.Visible, window.ErrorMessage.Visibility);
                Assert.True(window.IsVisible);
                input.Text = field == "interval" ? "00:00:10" : "1";
                Assert.True(window.SaveAsync().GetAwaiter().GetResult());
                Assert.Equal(1, service.CreateCalls);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Editing_UsesOriginalIdentityAndKeepsFailureVisible()
    {
        OnUi(() =>
        {
            var service = new SchedulerWindowTestService();
            var saved = service.AddTask("saved name");
            service.SaveResult = SchedulerOperationResult.Failed(SchedulerOperationError.PersistenceFailure, "disk unavailable");
            var draft = new SchedulerInfo { JobName = saved.JobName, GroupName = saved.GroupName, JobType = saved.JobType };
            var window = new CreateTask(service) { SchedulerInfo = draft };
            try
            {
                Show(window);
                Assert.Equal(Resources.Sched_SaveChanges, window.SubmitButton.Content);
                window.TaskNameTextBox.Text = "changed name";
                Assert.False(window.SaveAsync().GetAwaiter().GetResult());
                Assert.Equal((saved.JobName, saved.GroupName), service.UpdatedIdentity);
                Assert.Equal("saved name", saved.JobName);
                Assert.Equal("disk unavailable", window.ErrorMessage.Text);
                Assert.True(window.SubmitButton.IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TaskConfiguration_SurvivesScheduleChangesAndCannotBeSkippedOnFactoryFailure()
    {
        OnUi(() =>
        {
            var service = new SchedulerWindowTestService();
            service.Jobs["Configured"] = typeof(ConfiguredJob);
            service.Jobs["Broken configuration"] = typeof(BrokenConfigurationJob);
            var window = new CreateTask(service);
            try
            {
                Show(window);
                window.TaskComboBox.SelectedValue = typeof(ConfiguredJob);
                var config = Assert.IsType<TestJobConfig>(window.SchedulerInfo.Config);
                config.Label = "operator value";
                Assert.NotEmpty(window.StackPanelConfig.Children.Cast<UIElement>());
                window.SchedulerInfo.Mode = JobExecutionMode.Cron;
                window.SchedulerInfo.CronExpression = "0 0 2 * * ?";
                Assert.Same(config, window.SchedulerInfo.Config);
                Assert.Equal("operator value", config.Label);
                window.SchedulerInfo.Config = null!;
                window.TaskComboBox.SelectedValue = typeof(BrokenConfigurationJob);
                Assert.False(window.SaveAsync().GetAwaiter().GetResult());
                Assert.Equal(0, service.CreateCalls);
                Assert.Contains("configuration failed", window.ErrorMessage.Text);
            }
            finally { window.Close(); }
        });
    }

    public sealed class TestJobConfig : JobConfigBase { public string Label { get; set; } = "initial value"; }
    public sealed class ConfiguredJob : IJob, IConfigurableJob
    {
        public Type ConfigType => typeof(TestJobConfig);
        public IJobConfig CreateDefaultConfig() => new TestJobConfig();
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
    public sealed class BrokenConfigurationJob : IJob, IConfigurableJob
    {
        public Type ConfigType => typeof(TestJobConfig);
        public IJobConfig CreateDefaultConfig() => throw new InvalidOperationException("configuration failed");
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }

    [Fact]
    public void ClosingWhileInitializationPending_DoesNotEnableClosedWindow()
    {
        OnUi(() =>
        {
            var initialization = new TaskCompletionSource();
            var window = new TaskViewerWindow(new SchedulerWindowTestService(), initialization.Task);
            Show(window);
            Assert.False(window.CreateButton.IsEnabled);
            window.Close();
            initialization.SetResult();
            Drain();
            Assert.False(window.CreateButton.IsEnabled);
        });
    }

    private static void OnUi(Action action) => WpfTestHost.Invoke(() =>
    {
        var previousTheme = ThemeManager.Current.CurrentUITheme;
        ThemeManager.Current.ApplyTheme(Application.Current, Theme.Light);
        try { action(); }
        finally { ThemeManager.Current.ApplyTheme(Application.Current, previousTheme); }
    });

    private static void Show(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Left = -10000;
        window.Show();
        Drain();
        window.UpdateLayout();
    }

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
}

internal sealed class SchedulerWindowTestService : ISchedulerService
{
    public ObservableCollection<SchedulerInfo> TaskInfos { get; } = new();
    public Dictionary<string, Type> Jobs { get; } = new() { ["First task"] = typeof(FirstJob), ["Second task"] = typeof(SecondJob) };
    public IScheduler Scheduler => null!;
    public TaskExecutionListener Listener => null!;
    internal int CreateCalls { get; private set; }
    internal (string, string)? UpdatedIdentity { get; private set; }
    internal SchedulerOperationResult SaveResult { get; set; } = SchedulerOperationResult.Completed();
    internal SchedulerInfo AddTask(string name)
    {
        var task = new SchedulerInfo { JobName = name, GroupName = "tests", JobType = typeof(FirstJob) };
        TaskInfos.Add(task);
        return task;
    }
    public Task PauseAll() { foreach (var task in TaskInfos) task.Status = SchedulerStatus.Paused; return Task.CompletedTask; }
    public Task ResumeAll() { foreach (var task in TaskInfos) task.Status = SchedulerStatus.Ready; return Task.CompletedTask; }
    public Task Start() => Task.CompletedTask;
    public Task Shutdown() => Task.CompletedTask;
    public Task StopJob(string name, string group) { TaskInfos.Single(task => task.JobName == name && task.GroupName == group).Status = SchedulerStatus.Paused; return Task.CompletedTask; }
    public Task ResumeJob(string name, string group) { TaskInfos.Single(task => task.JobName == name && task.GroupName == group).Status = SchedulerStatus.Ready; return Task.CompletedTask; }
    public Task RemoveJob(string name, string group) { TaskInfos.Remove(TaskInfos.Single(task => task.JobName == name && task.GroupName == group)); return Task.CompletedTask; }
    public Task<SchedulerOperationResult> CreateJob(SchedulerInfo task) { CreateCalls++; return Task.FromResult(SaveResult); }
    public Task<SchedulerOperationResult> UpdateJob(SchedulerInfo task) => UpdateJob(task, task.JobName, task.GroupName);
    public Task<SchedulerOperationResult> UpdateJob(SchedulerInfo task, string name, string group) { UpdatedIdentity = (name, group); return Task.FromResult(SaveResult); }
    public string GetNewJobName(string name) => name;
    public string GetNewGroupName(string name) => name;
    public void SaveTasks() { }
    public void LoadTasks() { }
    public sealed class FirstJob : IJob { public Task Execute(IJobExecutionContext context) => Task.CompletedTask; }
    public sealed class SecondJob : IJob { public Task Execute(IJobExecutionContext context) => Task.CompletedTask; }
}
