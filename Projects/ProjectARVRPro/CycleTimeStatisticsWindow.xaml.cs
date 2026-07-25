using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Batch;
using ColorVision.UI;
using SqlSugar;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVRPro
{
    public partial class CycleTimeStatisticsWindow : Window
    {
        private readonly ViewResultManager _viewResultManager = ViewResultManager.GetInstance();
        private readonly ObservableCollection<CycleTimeGroup> _groups = [];
        private readonly ObservableCollection<ProjectARVRReuslt> _details = [];
        private int _detailLoadVersion;

        public CycleTimeStatisticsWindow()
        {
            InitializeComponent();
            GroupList.ItemsSource = _groups;
            DetailList.ItemsSource = _details;
            BuildDetailContextMenu();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            RefreshButton.IsEnabled = false;
            SummaryText.Text = "正在统计...";
            DetailHeader.Text = "组内明细";
            _details.Clear();

            try
            {
                IReadOnlyList<CycleTimeGroup> groups = await Task.Run(_viewResultManager.QueryCycleTimeGroups);
                _groups.Clear();
                foreach (CycleTimeGroup group in groups)
                {
                    _groups.Add(group);
                }

                SummaryText.Text = $"共 {_groups.Count} 次执行；相同 SN 重测会按流程顺序重新开始拆分";
                if (_groups.Count > 0)
                {
                    GroupList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                SummaryText.Text = "统计失败";
                MessageBox.Show(this, $"读取 CT 统计失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private async void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupList.SelectedItem is not CycleTimeGroup group)
            {
                _details.Clear();
                DetailHeader.Text = "组内明细";
                return;
            }

            int loadVersion = ++_detailLoadVersion;
            DetailHeader.Text = $"{group.SN} - {group.ExecutionText} - {group.ResultCount} 项 - CT {group.TotalRunTimeText}";
            try
            {
                IReadOnlyList<ProjectARVRReuslt> details = await Task.Run(() => _viewResultManager.QueryCycleTimeDetails(group));
                if (loadVersion != _detailLoadVersion || GroupList.SelectedItem != group)
                {
                    return;
                }

                _details.Clear();
                foreach (ProjectARVRReuslt detail in details)
                {
                    _details.Add(detail);
                }
            }
            catch (Exception ex)
            {
                if (loadVersion == _detailLoadVersion)
                {
                    _details.Clear();
                    MessageBox.Show(this, $"读取组内明细失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BuildDetailContextMenu()
        {
            DetailList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (_, e) => e.CanExecute = DetailList.SelectedItems.Count > 0));

            var openFolderCommand = new RelayCommand(
                _ => OpenFolderAndSelectFile(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && File.Exists(item.FileName));
            var batchHistoryCommand = new RelayCommand(
                _ => OpenBatchDataHistory(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && item.BatchId > 0);
            var viewTestResultCommand = new RelayCommand(
                _ => ViewTestResult(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrEmpty(item.ViewResultJson));

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.Copy, Header = "复制" });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Command = openFolderCommand, Header = "OpenFolderAndSelectFile" });
            contextMenu.Items.Add(new MenuItem { Command = batchHistoryCommand, Header = "流程结果查询" });
            contextMenu.Items.Add(new MenuItem { Command = viewTestResultCommand, Header = "查看测试结果" });
            contextMenu.Opened += (_, _) => CommandManager.InvalidateRequerySuggested();

            DetailList.PreviewMouseRightButtonDown += (_, e) =>
            {
                DependencyObject? element = DetailList.InputHitTest(e.GetPosition(DetailList)) as DependencyObject;
                while (element != null && element is not ListViewItem)
                {
                    element = VisualTreeHelper.GetParent(element);
                }

                if (element is ListViewItem targetItem)
                {
                    targetItem.IsSelected = true;
                }
            };

            DetailList.ContextMenu = contextMenu;
        }

        private void OpenFolderAndSelectFile()
        {
            if (DetailList.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrWhiteSpace(item.FileName))
            {
                PlatformHelper.OpenFolderAndSelectFile(item.FileName);
            }
        }

        private void OpenBatchDataHistory()
        {
            if (DetailList.SelectedItem is not ProjectARVRReuslt item)
            {
                return;
            }

            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true
            });
            MeasureBatchModel? batch = db.Queryable<MeasureBatchModel>().Where(model => model.Id == item.BatchId).First();
            if (batch == null)
            {
                MessageBox.Show(this, "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }

            var frame = new Frame();
            var window = new Window
            {
                Owner = this,
                Content = new MeasureBatchPage(frame, batch)
            };
            window.Show();
        }

        private void ViewTestResult()
        {
            if (DetailList.SelectedItem is not ProjectARVRReuslt item || string.IsNullOrEmpty(item.ViewResultJson))
            {
                return;
            }

            new TestResultViewWindow(item.ViewResultJson)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }
    }
}
