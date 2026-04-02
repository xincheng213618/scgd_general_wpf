using ColorVision.Scheduler.Data;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Scheduler
{
    public partial class ExecutionHistoryWindow : Window
    {
        private readonly string? _jobName;
        private readonly string? _groupName;
        private int _pageIndex = 1;
        private const int PageSize = 100;
        private string _filter = "全部";

        /// <summary>
        /// 查看所有任务的执行历史
        /// </summary>
        public ExecutionHistoryWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            TextBlockTaskName.Text = "全部任务";
            LoadData();
        }

        /// <summary>
        /// 查看指定任务的执行历史
        /// </summary>
        public ExecutionHistoryWindow(string jobName, string groupName)
        {
            this.ApplyCaption();
            this.ApplyCaption();
            _jobName = jobName;
            _groupName = groupName;
            TextBlockTaskName.Text = $"{jobName} ({groupName})";
            LoadData();
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
            if (_filter == "成功")
                records = records.Where(r => r.Success).ToList();
            else if (_filter == "失败")
                records = records.Where(r => !r.Success).ToList();

            ListViewHistory.ItemsSource = records;
            TextBlockPage.Text = $"第 {_pageIndex} 页 (共 {records.Count} 条)";

            // 更新统计
            UpdateStats();
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
            var result = MessageBox.Show("确定要清理90天前的历史记录吗？", "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                int deleted = SchedulerDbManager.GetInstance().CleanupOldRecords(90);
                MessageBox.Show($"已清理 {deleted} 条历史记录", "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
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
                _filter = item.Content?.ToString() ?? "全部";
                _pageIndex = 1;
                LoadData();
            }
        }
    }
}
