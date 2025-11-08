using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Batch;
using ColorVision.Engine.Batch.IVL;
using ColorVision.Engine.Services.RC;
using ColorVision.Solution.Searches;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine
{


    public class ViewBatchResult : ViewModelBase
    {
        public MeasureBatchModel MeasureBatchModel { get; set; }

        public ContextMenu ContextMenu { get; set; }

        public RelayCommand ProcessCommand { get; set; }

        public ViewBatchResult()
        {

        }
        public ViewBatchResult(MeasureBatchModel batchResultMasterModel)
        {
            MeasureBatchModel = batchResultMasterModel;
            ContextMenu = new ContextMenu();
            PopulateContextMenu();
        }

        private void PopulateContextMenu()
        {
            var batchManager = ColorVision.Engine.Batch.BatchManager.GetInstance();
            if (batchManager.Processes.Count > 0)
            {
                var processMenuItem = new MenuItem { Header = "处理结果" };
                
                foreach (var process in batchManager.Processes)
                {
                    var metadata = ColorVision.Engine.Batch.BatchProcessMetadata.FromProcess(process);
                    var menuItem = new MenuItem 
                    { 
                        Header = metadata.DisplayName,
                        ToolTip = metadata.GetTooltipText(),
                        Tag = process
                    };
                    menuItem.Click += ProcessMenuItem_Click;
                    processMenuItem.Items.Add(menuItem);
                }
                
                ContextMenu.Items.Add(processMenuItem);
            }
        }

        private void ProcessMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ColorVision.Engine.Batch.IBatchProcess process)
            {
                ExecuteProcess(process);
            }
        }

        private void ExecuteProcess(ColorVision.Engine.Batch.IBatchProcess process)
        {
            try
            {
                var context = new ColorVision.Engine.Batch.IBatchContext
                {
                    Batch = MeasureBatchModel,
                    Config = ColorVision.Engine.Batch.BatchConfig.Instance
                };

                bool success = process.Process(context);
                
                if (success)
                {
                    var metadata = ColorVision.Engine.Batch.BatchProcessMetadata.FromProcess(process);
                    MessageBox.Show($"处理成功: {metadata.DisplayName}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var metadata = ColorVision.Engine.Batch.BatchProcessMetadata.FromProcess(process);
                    MessageBox.Show($"处理失败: {metadata.DisplayName}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理出错: {ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [DisplayName("流程批次配置")]
    public class MeasureBatchManagerPageConfig : ViewConfigBase, IConfig
    {
        public static MeasureBatchManagerPageConfig Instance => ConfigService.Instance.GetRequiredService<MeasureBatchManagerPageConfig>();
    }

    public class MeasureBatchManager
    {
        private static MeasureBatchManager _instance;
        private static readonly object _locker = new();
        public static MeasureBatchManager GetInstance() { lock (_locker) { return _instance ??= new MeasureBatchManager(); } }

        public MeasureBatchManagerPageConfig Config { get; set; }
        public ObservableCollection<ViewBatchResult> ViewResults { get; set; } = new ObservableCollection<ViewBatchResult>();
        public RelayCommand GenericQueryCommand { get; set; }

        public MeasureBatchManager()
        {
            Config = ConfigService.Instance.GetRequiredService<MeasureBatchManagerPageConfig>();
            GenericQueryCommand = new RelayCommand(a => GenericQuery());
            Load();
        }

        public void Load()
        {
            ViewResults.Clear();
            var BatchResultMasterModels = MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().OrderByDescending(x => x.Id).OrderBy(x => x.Id, Config.OrderByType).Take(Config.Count).ToList();
            foreach (var item in BatchResultMasterModels)
            {
                ViewResults.Add(new ViewBatchResult(item));
            }
        }

        public void GenericQuery()
        {
            GenericQuery<MeasureBatchModel, ViewBatchResult> genericQuery = new GenericQuery<MeasureBatchModel, ViewBatchResult>(MySqlControl.GetInstance().DB, ViewResults, t => new ViewBatchResult(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

    }


        /// <summary>
        /// MeasureBatchManagerPage.xaml 的交互逻辑
        /// </summary>
    public partial class MeasureBatchManagerPage : Page,IPage
    {
        public string PageTitle => nameof(MeasureBatchManagerPage);

        public MeasureBatchManager MeasureBatchManager { get; set; } = MeasureBatchManager.GetInstance();

        public ObservableCollection<ViewBatchResult> ViewResults => MeasureBatchManager.ViewResults;

        public Frame Frame { get; set; }

        public MeasureBatchManagerPage() { }
        public MeasureBatchManagerPage(Frame MainFrame)
        {
            Frame = MainFrame;
            InitializeComponent();
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MeasureBatchManager.Load();
            this.DataContext = MeasureBatchManager;

        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            listView1.ItemsSource = ViewResults;
            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilities);
        }
        private void KeyEnter(object sender, KeyEventArgs e)
        {

        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewResults.Clear();
            foreach (var item in MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().Where(x => x.Code == SearchBox.Text).ToList())
            {
                ViewResults.Add(new ViewBatchResult(item));
            }
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            foreach (var item in MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().Where(x => x.Code == SearchBox.Text).ToList())
            {
                ViewResults.Add(new ViewBatchResult(item));
            }
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilities { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                 GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilities);
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewBatchResult);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilities.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResults.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }

        }
        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                Frame.Navigate(new MeasureBatchPage(Frame, ViewResults[listView.SelectedIndex].MeasureBatchModel));
            }
        }
        private void Arch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ViewBatchResult viewBatchResult && viewBatchResult.MeasureBatchModel.Code !=null)
            {
                MqttRCService.GetInstance().Archived(viewBatchResult.MeasureBatchModel.Code);
                MessageBox.Show("归档指令已经发送");
                Frame.Refresh();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MqttRCService.GetInstance().ArchivedAll();
            MessageBox.Show("全部归档指令已经发送");
        }

        private void AdvanceQuery_Click(object sender, RoutedEventArgs e)
        {
            GenericQuery<MeasureBatchModel, ViewBatchResult> genericQuery = new GenericQuery<MeasureBatchModel, ViewBatchResult>(MySqlControl.GetInstance().DB, ViewResults, t => new ViewBatchResult(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }
    }
}
