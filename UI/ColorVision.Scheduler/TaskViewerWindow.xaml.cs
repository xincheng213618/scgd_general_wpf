using ColorVision.Themes;
using Microsoft.Win32;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// TaskViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TaskViewerWindow : Window
    {
        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; }
        private ICollectionView _taskInfosView;

        public static QuartzSchedulerManager QuartzSchedulerManager => QuartzSchedulerManager.GetInstance();

        public TaskViewerWindow()
        {
            InitializeComponent();
            this.DataContext = QuartzSchedulerManager;
            TaskInfos = QuartzSchedulerManager.GetInstance().TaskInfos;
            
            // 使用 CollectionView 以支持过滤
            _taskInfosView = CollectionViewSource.GetDefaultView(TaskInfos);
            _taskInfosView.Filter = FilterTasks;
            ListViewTask.ItemsSource = _taskInfosView;
            
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

        private async void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                // 深拷贝一份用于编辑
                var editInfo = JsonConvert.DeserializeObject<SchedulerInfo>(JsonConvert.SerializeObject(info, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                if (editInfo != null)
                {
                    var win = new CreateTask { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, SchedulerInfo = editInfo };
                    if (win.ShowDialog() == true)
                    {
                        // 编辑完成后更新
                        await QuartzSchedulerManager.UpdateJob(editInfo);
                    }
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
                try
                {
                    await QuartzSchedulerManager.StopJob(info.JobName, info.GroupName);
                    info.Status = SchedulerStatus.Paused;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"暂停任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuResume_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                try
                {
                    await QuartzSchedulerManager.ResumeJob(info.JobName, info.GroupName);
                    info.Status = SchedulerStatus.Ready;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"恢复任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                try
                {
                    var result = MessageBox.Show($"确定要删除任务 {info.JobName}({info.GroupName}) 吗？", 
                        "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        await QuartzSchedulerManager.RemoveJob(info.JobName, info.GroupName);
                        TaskInfos.Remove(info);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewTask.SelectedItem is SchedulerInfo info)
            {
                try
                {
                    var jobKey = new Quartz.JobKey(info.JobName, info.GroupName);
                    await QuartzSchedulerManager.Scheduler.TriggerJob(jobKey);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"触发任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 搜索和过滤功能
        private bool FilterTasks(object obj)
        {
            if (obj is not SchedulerInfo task)
                return false;


            return true;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _taskInfosView?.Refresh();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _taskInfosView?.Refresh();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _taskInfosView?.Refresh();
        }

        // 导出功能
        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV 文件|*.csv",
                    FileName = $"Tasks_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    // CSV 头部
                    sb.AppendLine("任务名称,分组名称,优先级,运行次数,成功次数,失败次数,状态,最后执行时间(ms),平均执行时间(ms),最大执行时间(ms),最小执行时间(ms),下次执行时间,上次执行时间,创建时间");

                    // 数据行
                    foreach (var task in TaskInfos)
                    {
                        sb.AppendLine($"\"{task.JobName}\",\"{task.GroupName}\",{task.Priority},{task.RunCount},{task.SuccessCount},{task.FailureCount},\"{task.Status}\",{task.LastExecutionTimeMs},{task.AverageExecutionTimeMs},{task.MaxExecutionTimeMs},{task.MinExecutionTimeMs},\"{task.NextFireTime}\",\"{task.PreviousFireTime}\",\"{task.CreateTime:yyyy-MM-dd HH:mm:ss}\"");
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"成功导出 {TaskInfos.Count} 个任务到:\n{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出CSV失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportJSON_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON 文件|*.json",
                    FileName = $"Tasks_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        TypeNameHandling = TypeNameHandling.Auto,
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    var json = JsonConvert.SerializeObject(TaskInfos, settings);
                    File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                    MessageBox.Show($"成功导出 {TaskInfos.Count} 个任务配置到:\n{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出JSON失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "文本报告|*.txt|Markdown 报告|*.md",
                    FileName = $"TaskReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
                    sb.AppendLine("║        ColorVision.Scheduler 任务执行统计报告                  ║");
                    sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
                    sb.AppendLine();
                    sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"任务总数: {TaskInfos.Count}");
                    sb.AppendLine();

                    // 总体统计
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    sb.AppendLine("总体统计");
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    var totalRuns = TaskInfos.Sum(t => t.RunCount);
                    var totalSuccess = TaskInfos.Sum(t => t.SuccessCount);
                    var totalFailure = TaskInfos.Sum(t => t.FailureCount);
                    var avgExecutionTime = TaskInfos.Where(t => t.AverageExecutionTimeMs > 0).Average(t => (double?)t.AverageExecutionTimeMs) ?? 0;

                    sb.AppendLine($"总执行次数: {totalRuns}");
                    sb.AppendLine($"成功次数: {totalSuccess} ({(totalRuns > 0 ? (totalSuccess * 100.0 / totalRuns).ToString("F2") : "0.00")}%)");
                    sb.AppendLine($"失败次数: {totalFailure} ({(totalRuns > 0 ? (totalFailure * 100.0 / totalRuns).ToString("F2") : "0.00")}%)");
                    sb.AppendLine($"平均执行时间: {avgExecutionTime:F2} ms");
                    sb.AppendLine();

                    // 按状态分组统计
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    sb.AppendLine("任务状态分布");
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    var statusGroups = TaskInfos.GroupBy(t => t.Status);
                    foreach (var group in statusGroups)
                    {
                        sb.AppendLine($"{group.Key}: {group.Count()} 个任务");
                    }
                    sb.AppendLine();

                    // 详细任务列表
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    sb.AppendLine("任务详细信息");
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    foreach (var task in TaskInfos.OrderByDescending(t => t.RunCount))
                    {
                        sb.AppendLine();
                        sb.AppendLine($"【{task.JobName}】({task.GroupName})");
                        sb.AppendLine($"  优先级: {task.Priority}");
                        sb.AppendLine($"  状态: {task.Status}");
                        sb.AppendLine($"  执行统计: 总计 {task.RunCount} 次 (成功 {task.SuccessCount}, 失败 {task.FailureCount})");
                        if (task.RunCount > 0)
                        {
                            sb.AppendLine($"  执行时间: 最后 {task.LastExecutionTimeMs}ms, 平均 {task.AverageExecutionTimeMs}ms, 最大 {task.MaxExecutionTimeMs}ms, 最小 {task.MinExecutionTimeMs}ms");
                        }
                        if (!string.IsNullOrEmpty(task.NextFireTime) && task.NextFireTime != "N/A")
                        {
                            sb.AppendLine($"  下次执行: {task.NextFireTime}");
                        }
                        if (!string.IsNullOrEmpty(task.PreviousFireTime))
                        {
                            sb.AppendLine($"  上次执行: {task.PreviousFireTime}");
                        }
                        sb.AppendLine($"  创建时间: {task.CreateTime:yyyy-MM-dd HH:mm:ss}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");
                    sb.AppendLine("报告结束");
                    sb.AppendLine("═══════════════════════════════════════════════════════════════");

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"成功生成执行统计报告:\n{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成报告失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 批量操作功能
        private async void BatchResume_Click(object sender, RoutedEventArgs e)
        {
            var selectedTasks = ListViewTask.SelectedItems.Cast<SchedulerInfo>().ToList();
            if (selectedTasks.Count == 0)
            {
                MessageBox.Show("请先选择要启动的任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var successCount = 0;
                var failedTasks = new List<string>();

                foreach (var task in selectedTasks)
                {
                    try
                    {
                        await QuartzSchedulerManager.ResumeJob(task.JobName, task.GroupName);
                        task.Status = SchedulerStatus.Ready;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failedTasks.Add($"{task.JobName}({task.GroupName}): {ex.Message}");
                    }
                }

                if (failedTasks.Count > 0)
                {
                    MessageBox.Show($"成功启动 {successCount} 个任务\n以下任务启动失败:\n{string.Join("\n", failedTasks)}", 
                        "部分成功", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"成功启动 {successCount} 个任务", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BatchPause_Click(object sender, RoutedEventArgs e)
        {
            var selectedTasks = ListViewTask.SelectedItems.Cast<SchedulerInfo>().ToList();
            if (selectedTasks.Count == 0)
            {
                MessageBox.Show("请先选择要暂停的任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var successCount = 0;
                var failedTasks = new List<string>();

                foreach (var task in selectedTasks)
                {
                    try
                    {
                        await QuartzSchedulerManager.StopJob(task.JobName, task.GroupName);
                        task.Status = SchedulerStatus.Paused;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failedTasks.Add($"{task.JobName}({task.GroupName}): {ex.Message}");
                    }
                }

                if (failedTasks.Count > 0)
                {
                    MessageBox.Show($"成功暂停 {successCount} 个任务\n以下任务暂停失败:\n{string.Join("\n", failedTasks)}", 
                        "部分成功", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"成功暂停 {successCount} 个任务", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量暂停失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BatchDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedTasks = ListViewTask.SelectedItems.Cast<SchedulerInfo>().ToList();
            if (selectedTasks.Count == 0)
            {
                MessageBox.Show("请先选择要删除的任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要删除选中的 {selectedTasks.Count} 个任务吗？\n此操作不可撤销！", 
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var successCount = 0;
                var failedTasks = new List<string>();

                foreach (var task in selectedTasks.ToList()) // ToList to avoid modification during iteration
                {
                    try
                    {
                        await QuartzSchedulerManager.RemoveJob(task.JobName, task.GroupName);
                        TaskInfos.Remove(task);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failedTasks.Add($"{task.JobName}({task.GroupName}): {ex.Message}");
                    }
                }

                if (failedTasks.Count > 0)
                {
                    MessageBox.Show($"成功删除 {successCount} 个任务\n以下任务删除失败:\n{string.Join("\n", failedTasks)}", 
                        "部分成功", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"成功删除 {successCount} 个任务", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
