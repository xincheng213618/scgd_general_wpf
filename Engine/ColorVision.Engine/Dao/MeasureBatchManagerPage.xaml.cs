#pragma warning disable CA1863,CS8625
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Themes;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public string TemplateName => TemplateFlow.Params.FirstOrDefault(item => item.Id == MeasureBatchModel.TId)?.Key
            ?? MeasureBatchModel.TId?.ToString(CultureInfo.CurrentCulture) ?? "—";
        public double DurationSeconds => MeasureBatchModel.TotalTime / 1000d;
        public string StatusText => EngineLocalization.Get(MeasureBatchModel.FlowStatus switch
        {
            FlowStatus.Ready => "就绪",
            FlowStatus.Runing => "运行中",
            FlowStatus.Paused => "已暂停",
            FlowStatus.Failed => "失败",
            FlowStatus.Canceled => "已取消",
            FlowStatus.OverTime => "超时",
            FlowStatus.Completed => "已完成",
            _ => MeasureBatchModel.FlowStatus.ToString()
        });

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
            var nodeAnalysisMenuItem = new MenuItem { Header = "流程执行分析" };
            nodeAnalysisMenuItem.Click += (s, e) =>
            {
                var window = new FlowExecutionAnalysisWindow(MeasureBatchModel) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Show();
            };
            ContextMenu.Items.Add(nodeAnalysisMenuItem);

            var postProcessManager = PostProcessManager.GetInstance();
            if (postProcessManager.Processes.Count > 0)
            {
                var processMenuItem = new MenuItem { Header = Properties.Resources.Flow_MeasureBatch_ProcessResult };
                
                foreach (var process in postProcessManager.Processes)
                {
                    var metadata = PostProcessMetadata.FromProcess(process);
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
            if (sender is MenuItem menuItem && menuItem.Tag is IPostProcessor process)
            {
                ExecuteProcess(process);
            }
        }

        private void ExecuteProcess(IPostProcessor process)
        {
            try
            {
                var context = new PostProcessContext
                {
                    Batch = MeasureBatchModel,
                    Config = PostProcessConfig.Instance,
                    FlowName = TemplateFlow.Params.FirstOrDefault(item => item.Id == MeasureBatchModel.TId)?.Key ?? string.Empty
                };

                bool success = process.Process(context);
                
                if (success)
                {
                    var metadata = PostProcessMetadata.FromProcess(process);
                    MessageBox.Show(string.Format(Properties.Resources.Flow_MeasureBatch_ProcessSuccess, metadata.DisplayName), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var metadata = PostProcessMetadata.FromProcess(process);
                    MessageBox.Show(string.Format(Properties.Resources.Flow_MeasureBatch_ProcessFailed, metadata.DisplayName), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.Flow_MeasureBatch_ProcessError, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [DisplayName("流程结果查询设置")]
    public class MeasureBatchManagerPageConfig : ViewModelBase, IConfig
    {
        public static MeasureBatchManagerPageConfig Instance => ConfigService.Instance.GetRequiredService<MeasureBatchManagerPageConfig>();

        [Browsable(false)]
        public RelayCommand EditCommand { get; }

        public MeasureBatchManagerPageConfig()
        {
            EditCommand = new RelayCommand(_ => new PropertyEditorWindow(this)
            {
                Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog());
        }

        [DisplayName("查询数量"), Category("查询"), Description("普通查询最多读取的批次数量，至少为 1。修改后重新查询生效。")]
        public int Count { get => _count; set { _count = Math.Max(value, 1); OnPropertyChanged(); } }
        private int _count = 50;

        [DisplayName("排列顺序"), Category("查询"), Description("按批次序号排列：Desc 为最新在前，Asc 为最早在前。")]
        public OrderByType OrderByType { get => _orderByType; set { _orderByType = value; OnPropertyChanged(); } }
        private OrderByType _orderByType = OrderByType.Desc;

        // Retain the serialized keys from ViewConfigBase; they do not control history queries.
        [Browsable(false)]
        public bool AutoRefreshView { get; set; } = true;
        [Browsable(false)]
        public bool InsertAtBeginning { get; set; } = true;
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
        }

        public async Task LoadAsync(string? batchCode = null)
        {
            int count = Config.Count;
            var order = Config.OrderByType;
            var batches = await Task.Run(() => QueryBatches(batchCode, count, order));
            ReplaceResults(batches);
        }

        public void Load() => ReplaceResults(QueryBatches(null, Config.Count, Config.OrderByType));

        private static List<MeasureBatchModel> QueryBatches(string? batchCode, int count, OrderByType order)
        {
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            var query = db.Queryable<MeasureBatchModel>();
            if (!string.IsNullOrWhiteSpace(batchCode)) query = query.Where(item => item.Code == batchCode);
            return query.OrderBy(item => item.Id, order).Take(count).ToList();
        }

        private void ReplaceResults(IEnumerable<MeasureBatchModel> batches)
        {
            ViewResults.Clear();
            foreach (var item in batches)
            {
                ViewResults.Add(new ViewBatchResult(item));
            }
        }

        public void GenericQuery()
        {
            var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            GenericQuery<MeasureBatchModel, ViewBatchResult> genericQuery = new GenericQuery<MeasureBatchModel, ViewBatchResult>(DB, ViewResults, t => new ViewBatchResult(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
            DB.Dispose();
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
        
        private IPostProcessor _selectedProcess;
        private INotifyPropertyChanged _currentProcessConfig;
        private PropertyChangedEventHandler _configChangedHandler;
        private CopilotDynamicContextSession? _copilotContextSession;
        private Window? _copilotHostWindow;
        private bool _copilotPublishQueued;
        private bool _isQuerying;
        private string _appliedBatchCode = string.Empty;
        private string _requestedBatchCode = string.Empty;
        private bool _lastQuerySucceeded = true;
        private string _loadedDataAsOf = string.Empty;

        public MeasureBatchManagerPage() { }
        public MeasureBatchManagerPage(Frame MainFrame)
        {
            Frame = MainFrame;
            InitializeComponent();
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResults.CollectionChanged -= ViewResults_CollectionChanged;
            ViewResults.CollectionChanged += ViewResults_CollectionChanged;
            this.DataContext = MeasureBatchManager;
            Window.GetWindow(this)?.ApplyCaption();
            
            // Initialize process ComboBox
            var postProcessManager = PostProcessManager.GetInstance();
            ProcessComboBox.ItemsSource = postProcessManager.Processes;
            if (postProcessManager.Processes.Count > 0)
            {
                ProcessComboBox.SelectedIndex = 0;
            }
            EnsureCopilotContextRegistered();
            await QueryAsync();
            PublishCopilotContext();
        }
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewResults.CollectionChanged -= ViewResults_CollectionChanged;
            ReleaseCopilotContext();
        }
        private void ViewResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_copilotPublishQueued)
                return;

            _copilotPublishQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _copilotPublishQueued = false;
                UpdateSummary();
                PublishCopilotContext();
            }));
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            listView1.ItemsSource = ViewResults;
            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilities);
        }
        private void KeyEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _ = QueryAsync();
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (QueryStatusText != null && !_isQuerying)
                QueryStatusText.Text = EngineLocalization.Get("条件已修改，按 Enter 或点击搜索应用查询。");
        }

        private async void Query_Click(object sender, RoutedEventArgs e) => await QueryAsync();

        private async void ResetQuery_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            await QueryAsync();
        }

        private async Task QueryAsync()
        {
            if (_isQuerying) return;
            _isQuerying = true;
            QueryControls.IsEnabled = false;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            QueryStatusText.Text = EngineLocalization.Get("正在查询批次记录…");
            string batchCode = SearchBox.Text.Trim();
            _requestedBatchCode = batchCode;
            try
            {
                await MeasureBatchManager.LoadAsync(batchCode);
                _appliedBatchCode = batchCode;
                _lastQuerySucceeded = true;
                _loadedDataAsOf = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                QueryStatusText.Text = EngineLocalization.Format($"已读取 {ViewResults.Count} 条，最多 {MeasureBatchManager.Config.Count} 条；概览仅统计本次查询结果。");
            }
            catch (Exception ex)
            {
                _lastQuerySucceeded = false;
                QueryStatusText.Text = EngineLocalization.Format($"查询失败，保留原有列表：{ex.Message}");
                log4net.LogManager.GetLogger(typeof(MeasureBatchManagerPage)).Warn("查询流程结果失败", ex);
            }
            finally
            {
                _isQuerying = false;
                QueryControls.IsEnabled = true;
                UpdateSummary();
                PublishCopilotContext();
            }
        }

        private void UpdateSummary()
        {
            LoadedCountText.Text = ViewResults.Count.ToString(CultureInfo.CurrentCulture);
            var completed = ViewResults.Where(item => item.MeasureBatchModel.FlowStatus == FlowStatus.Completed).ToArray();
            CompletedCountText.Text = completed.Length.ToString(CultureInfo.CurrentCulture);
            FailedCountText.Text = ViewResults.Count(item => item.MeasureBatchModel.FlowStatus is FlowStatus.Failed or FlowStatus.OverTime).ToString(CultureInfo.CurrentCulture);
            AverageTimeText.Text = completed.Length == 0 ? "—" : completed.Average(item => item.DurationSeconds).ToString("0.###", CultureInfo.CurrentCulture);
            EmptyStatePanel.Visibility = !_isQuerying && ViewResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateExecuteButtonState();
            UpdateSelectedBatchText();
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }
        
        private void UpdateSelectedBatchText()
        {
            if (listView1.SelectedItem is ViewBatchResult viewBatch)
            {
                SelectedBatchText.Text = $"#{viewBatch.MeasureBatchModel.Id} · {viewBatch.MeasureBatchModel.Code}";
                SelectedBatchText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            }
            else
            {
                SelectedBatchText.Text = Properties.Resources.Flow_MeasureBatch_NotSelected;
                SelectedBatchText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
            }
        }
        
        private void ProcessComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshProcessConfigPanel();
            UpdateExecuteButtonState();
        }
        
        private void RefreshProcessConfigPanel()
        {
            ProcessConfigPanel.Children.Clear();
            
            // Cleanup previous config handler
            if (_currentProcessConfig != null && _configChangedHandler != null)
            {
                _currentProcessConfig.PropertyChanged -= _configChangedHandler;
                _currentProcessConfig = null;
                _configChangedHandler = null;
            }
            
            if (ProcessComboBox.SelectedItem is IPostProcessor process)
            {
                _selectedProcess = process;
                
                var metadata = PostProcessMetadata.FromProcess(process);
                
                // Add process info section
                var infoBorder = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                
                infoBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                var infoStack = new StackPanel();
                infoBorder.Child = infoStack;
                
                infoStack.Children.Add(new TextBlock 
                { 
                    Text = Properties.Resources.Flow_MeasureBatch_ProcessInfo,
                    FontWeight = FontWeights.Bold, 
                    Margin = new Thickness(0, 0, 0, 8) 
                });
                
                AddLabeledText(infoStack, Properties.Resources.Flow_MeasureBatch_NameLabel, metadata.DisplayName);
                if (!string.IsNullOrEmpty(metadata.Description))
                {
                    AddLabeledText(infoStack, Properties.Resources.Flow_MeasureBatch_DescriptionLabel, metadata.Description);
                }
                if (!string.IsNullOrEmpty(metadata.Category))
                {
                    AddLabeledText(infoStack, Properties.Resources.Flow_MeasureBatch_ClassificationLabel, metadata.Category);
                }
                
                ProcessConfigPanel.Children.Add(infoBorder);
                
                // Add config section if available
                var config = process.GetConfig();
                if (config != null)
                {
                    var configBorder = new Border
                    {
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    configBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                    var configStack = new StackPanel();
                    configBorder.Child = configStack;
                    
                    configStack.Children.Add(new TextBlock 
                    { 
                        Text = Properties.Resources.Flow_MeasureBatch_ConfigOptions,
                        FontWeight = FontWeights.Bold, 
                        Margin = new Thickness(0, 0, 0, 8) 
                    });
                    
                    // Generate property editor controls
                    var configPanel = PropertyEditorHelper.GenPropertyEditorControl(config, showCategoryHeader: false);
                    configStack.Children.Add(configPanel);
                    
                    ProcessConfigPanel.Children.Add(configBorder);
                }
            }
            else
            {
                _selectedProcess = null;
                ProcessConfigPanel.Children.Add(new TextBlock 
                { 
                    Text = Properties.Resources.Flow_MeasureBatch_SelectProcessTypeToViewConfig,
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    Margin = new Thickness(0, 20, 0, 0) 
                });
            }
        }
        
        private static void AddLabeledText(StackPanel parent, string label, string value)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            dock.Children.Add(new TextBlock 
            { 
                Text = label, 
                Width = 50, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            dock.Children.Add(new TextBlock 
            { 
                Text = value ?? "", 
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            parent.Children.Add(dock);
        }
        
        private void UpdateExecuteButtonState()
        {
            ExecuteButton.IsEnabled = listView1.SelectedItem != null && _selectedProcess != null;
            OpenResultButton.IsEnabled = listView1.SelectedItem != null;
            OpenAnalysisButton.IsEnabled = listView1.SelectedItem != null;
        }
        
        private void ExecuteProcess_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedItem is ViewBatchResult viewBatch && _selectedProcess != null)
            {
                try
                {
                    var context = new PostProcessContext
                    {
                        Batch = viewBatch.MeasureBatchModel,
                        Config = PostProcessConfig.Instance,
                        FlowName = TemplateFlow.Params.FirstOrDefault(item => item.Id == viewBatch.MeasureBatchModel.TId)?.Key ?? string.Empty
                    };

                    bool success = _selectedProcess.Process(context);
                    
                    var metadata = PostProcessMetadata.FromProcess(_selectedProcess);
                    if (success)
                    {
                        MessageBox.Show(string.Format(Properties.Resources.Flow_MeasureBatch_ProcessSuccess, metadata.DisplayName), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(string.Format(Properties.Resources.Flow_MeasureBatch_ProcessFailed, metadata.DisplayName), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Properties.Resources.Flow_MeasureBatch_ProcessError, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilities { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                 GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilities);
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ViewBatchResult>(sender, GridViewColumnVisibilities, Properties.Resources.ResourceManager);

        }
        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(listView1, e.OriginalSource as DependencyObject) is ListViewItem)
                OpenResult_Click(sender, e);
        }

        private void OpenResult_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedItem is ViewBatchResult selected)
            {
                Frame.Navigate(new MeasureBatchPage(Frame, selected.MeasureBatchModel));
            }
        }

        private void OpenAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedItem is ViewBatchResult selected)
                new FlowExecutionAnalysisWindow(selected.MeasureBatchModel)
                {
                    Owner = Window.GetWindow(this), WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.Show();
        }
        private void Arch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ViewBatchResult viewBatchResult && viewBatchResult.MeasureBatchModel.Code !=null)
            {
                MqttRCService.GetInstance().Archived(viewBatchResult.MeasureBatchModel.Code);
                MessageBox.Show(Properties.Resources.Flow_MeasureBatch_ArchiveCommandSent);
                Frame.Refresh();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MqttRCService.GetInstance().ArchivedAll();
            MessageBox.Show(Properties.Resources.Flow_MeasureBatch_AllArchiveCommandSent);
        }

        private void AdvanceQuery_Click(object sender, RoutedEventArgs e)
        {
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            GenericQuery<MeasureBatchModel, ViewBatchResult> genericQuery = new GenericQuery<MeasureBatchModel, ViewBatchResult>(DB, ViewResults, t => new ViewBatchResult(t));
            var queryAttempted = false;
            var querySucceeded = false;
            genericQuery.PreQuery += (_, _) => queryAttempted = true;
            genericQuery.QueryCompleted += (_, _) => querySucceeded = true;
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
            DB.Dispose();
            if (queryAttempted)
            {
                _requestedBatchCode = "<advanced>";
                _lastQuerySucceeded = querySucceeded;
                if (querySucceeded)
                {
                    _appliedBatchCode = "<advanced>";
                    _loadedDataAsOf = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                }
            }
            UpdateSummary();
            if (querySucceeded)
                QueryStatusText.Text = EngineLocalization.Format($"高级查询：{ViewResults.Count} 条；概览仅统计本次查询结果。");
            else if (queryAttempted)
                QueryStatusText.Text = EngineLocalization.Get("高级查询失败或未完成，当前列表可能保留旧数据。");
            PublishCopilotContext();
        }

        private void EnsureCopilotContextRegistered()
        {
            if (_copilotContextSession != null)
                return;

            try
            {
                _copilotContextSession = CopilotMeasurementResultContextHub.Shared.Register(
                    CaptureCopilotMeasurementResultSnapshotAsync,
                    typeof(MeasureBatchManagerPage).Assembly.GetName().Version?.ToString());
                _copilotHostWindow = Window.GetWindow(this);
                if (_copilotHostWindow != null)
                {
                    _copilotHostWindow.Activated += CopilotHostWindow_Activated;
                    _copilotHostWindow.Closed += CopilotHostWindow_Closed;
                }
                _copilotContextSession.Activate();
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(MeasureBatchManagerPage)).Warn("注册检测批次历史 Copilot 上下文失败，结果页面将继续运行", ex);
            }
        }

        private void CopilotHostWindow_Activated(object? sender, EventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void CopilotHostWindow_Closed(object? sender, EventArgs e)
        {
            ReleaseCopilotContext();
        }

        private void ReleaseCopilotContext()
        {
            if (_copilotHostWindow != null)
            {
                _copilotHostWindow.Activated -= CopilotHostWindow_Activated;
                _copilotHostWindow.Closed -= CopilotHostWindow_Closed;
                _copilotHostWindow = null;
            }

            var wasCurrent = _copilotContextSession?.IsCurrent == true;
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrent)
                CopilotLiveContextRegistry.Clear(CopilotMeasurementResultAgentExtension.SourceId);
        }

        private async Task<CopilotMeasurementResultContextSnapshot?> CaptureCopilotMeasurementResultSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureCopilotMeasurementResultSnapshot();
                });
            }

            return CaptureCopilotMeasurementResultSnapshot();
        }

        private CopilotMeasurementResultContextSnapshot CaptureCopilotMeasurementResultSnapshot()
        {
            var batch = (listView1.SelectedItem as ViewBatchResult)?.MeasureBatchModel;
            var templateName = batch?.TId is int templateId
                ? TemplateFlow.Params.FirstOrDefault(item => item.Id == templateId)?.Key ?? string.Empty
                : string.Empty;
            return new CopilotMeasurementResultContextSnapshot
            {
                SourceId = CopilotMeasurementResultAgentExtension.SourceId,
                Surface = "Measurement result history",
                LoadedBatchCount = ViewResults.Count,
                IsFilterActive = !string.IsNullOrWhiteSpace(_appliedBatchCode),
                LastQuerySucceeded = _lastQuerySucceeded,
                IsLoadedDataStale = !_lastQuerySucceeded,
                RequestedFilterMatchesLoadedData = string.Equals(_requestedBatchCode, _appliedBatchCode, StringComparison.Ordinal),
                LoadedDataAsOf = _loadedDataAsOf,
                BatchId = batch?.Id,
                TemplateId = batch?.TId,
                TemplateName = templateName,
                BatchStatus = batch?.FlowStatus.ToString() ?? string.Empty,
                CreatedAt = batch?.CreateDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                TotalTimeMilliseconds = batch?.TotalTime ?? 0,
                ArchiveStatus = batch?.ArchiveStatus.ToString() ?? string.Empty,
                HasResultMessage = !string.IsNullOrWhiteSpace(batch?.Result),
            };
        }

        private void PublishCopilotContext()
        {
            if (_copilotContextSession?.IsCurrent != true || _copilotHostWindow?.IsActive != true)
                return;

            var snapshot = CaptureCopilotMeasurementResultSnapshot();
            var item = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(snapshot);
            CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                CopilotMeasurementResultAgentExtension.SourceId,
                item));
        }
    }
}
