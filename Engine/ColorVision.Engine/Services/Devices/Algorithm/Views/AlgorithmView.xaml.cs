#pragma warning disable CA1822,CS8601,CS8604,CS8622,CS8625
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Results;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AlgorithmView));
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _messageSubscribed;
        private IDisposable? _localResultSubscription;
        private readonly ResultImagePlaceholderCache _resultImagePlaceholderCache = new();

        private DeviceAlgorithm? Device { get; }

        public ImageView ImageView { get; set; }

        public ListView ListView { get; set; }

        public TextBox SideTextBox { get; set; }

        public AlgorithmView() : this(null)
        {
        }

        public AlgorithmView(DeviceAlgorithm? device)
        {
            Device = device;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(AlgorithmResultDataSaver.SaveCommand, SaveSideDataCommand_Executed, SaveSideDataCommand_CanExecute));
        }

        public ViewAlgorithmConfig Config => ViewAlgorithmConfig.Instance;


        public ViewResultContext ViewResultContext { get; set; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (_isInitialized || _isDisposed)
                return;

            _isInitialized = true;
            if (Device != null)
            {
                Device.DService.MsgReturnReceived += DeviceService_OnMessageRecved;
                _messageSubscribed = true;
                _localResultSubscription = ResultMessageBus.Default.Subscribe(LocalResultPublished);
            }
            this.DataContext = Config;
            ImageView = new ImageView();
            ListView = listViewSide;
            SideTextBox = TextBoxside;
            Grid1.Children.Add(ImageView);
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            listView1.ItemsSource = ViewResults;
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

            ViewResultContext = new ViewResultContext();
            ViewResultContext.SideTextBox = SideTextBox;
            ViewResultContext.ImageView = ImageView;
            ViewResultContext.LeftGridViewColumnVisibilitys = LeftGridViewColumnVisibilitys;
            ViewResultContext.ListView = ListView;

        }

        private void Delete()
        {
            if (listView1.SelectedItems.Count == listView1.Items.Count)
                ViewResults.Clear();
            else
            {
                listView1.SelectedIndex = -1;
                foreach (var item in listView1.SelectedItems.Cast<ViewResultAlg>().ToList())
                    ViewResults.Remove(item);
            }
        }



        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
            }
        }

        public ObservableCollection<ViewResultAlg> ViewResults { get; } = new ObservableCollection<ViewResultAlg>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;

  
            if (listView1.SelectedIndex < 0 ||listView1.Items[listView1.SelectedIndex] is not ViewResultAlg result)
            {
                MessageBox.Show(Application.Current.MainWindow, Properties.Resources.SelectDataFirst, "ColorVision");
                return;
            }
            else
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                using StreamWriter file = new(dialog.FileName, true, Encoding.UTF8); 
                if (listView1.View is GridView gridView1)
                {
                    string headers = "";
                    foreach (var item in gridView1.Columns)
                    {
                        headers += item.Header.ToString() + ",";
                    }
                    file.WriteLine(headers);
                }
                string value = "";
                foreach (var item in ViewResults)
                {
                    value += item.Id + ","
                        + item.Batch + ","
                        + item.POITemplateName + ","
                        + item.FilePath + ","
                        + item.CreateTime + ","
                        + item.ResultType + ","
                        + item.TotalTime + ","
                        + item.ResultDesc + ","
                        + Environment.NewLine;
                }
                file.WriteLine(value);
                ImageSource bitmapSource = ImageView.ImageShow.Source;
                ImageUtils.SaveImageSourceToFile(bitmapSource, Path.Combine( Path.GetDirectoryName(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
            }
        }

        public void AddAlgResultMasterModel(AlgResultMasterModel result)
        {
            if (!_isDisposed && result != null)
            {
                ViewResultAlg ViewResultAlg = new ViewResultAlg(result);

                if (Config.InsertAtBeginning)
                    ViewResults.Insert(0, ViewResultAlg);
                else
                    ViewResults.Add(ViewResultAlg);

                if (Config.AutoRefreshView)
                    RefreshResultListView();
                if (Config.AutoSaveSideData)
                    SideSave(ViewResultAlg, Config.SaveSideDataDirPath);
            }
        }

        private void DeviceService_OnMessageRecved(MsgReturn message)
        {
            object? masterIdValue = message.Data?.MasterId;
            if (_isDisposed
                || Device == null
                || Device.IsDisposed
                || !string.Equals(message.DeviceCode, Device.Config.Code, StringComparison.Ordinal)
                || masterIdValue == null)
                return;

            int masterId = Convert.ToInt32(masterIdValue);
            if (masterId > 0)
                ShowPersistedResult(masterId);
        }

        private void LocalResultPublished(ResultMessage message)
        {
            if (_isDisposed
                || Device == null
                || Device.IsDisposed
                || message.Route != ResultRoutes.Algorithm
                || message.ResultKind != ResultKinds.Algorithm
                || !string.Equals(message.DeviceCode, Device.Config.Code, StringComparison.Ordinal))
                return;

            ShowPersistedResult(message.Data.MasterId);
        }

        private void ShowPersistedResult(int masterId)
        {
            if (_isDisposed || Device == null || Device.IsDisposed || masterId <= 0) return;
            AlgResultMasterModel? model = AlgResultMasterDao.Instance.GetById(masterId);
            if (model == null)
            {
                log.Debug($"GetAlgResult By Id is null: {masterId}");
                return;
            }

            log.Debug($"FileUrl：{model.ImgFile}");
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!_isDisposed && !Device.IsDisposed)
                    AddAlgResultMasterModel(model);
            });
        }

        public void RefreshResultListView()
        {
            if (_isDisposed)
                return;

            if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning? 0: listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            listViewSide.ItemsSource = null;
            SideTextBox.Visibility = Visibility.Collapsed;
            SideTextBox.Clear();

            if (listView1.SelectedItem is not ViewResultAlg result ||
                ResultHandleRegistry.GetInstance().ResultHandles.FirstOrDefault(item => item.CanHandle1(result)) is not { } resultHandle)
            {
                ImageView.Clear();
                return;
            }

            resultHandle.Load(ViewResultContext, result);
            PrepareResultImageSurface(result);
            resultHandle.Handle(ViewResultContext, result);
        }

        private void PrepareResultImageSurface(ViewResultAlg result)
        {
            ImageView.ImageShow.Clear();

            if (File.Exists(result.FilePath))
            {
                // The result handler may reuse the current bitmap when it opens a compatible CVRAW file.
                return;
            }

            if (!AlgorithmResultImageDimensions.TryRecoverFromMeasureResults(result, out int width, out int height))
            {
                ImageView.Clear();
                log.Warn($"算法结果图像不存在且没有可恢复尺寸，已清除旧底图：resultId={result.Id}, file={result.FilePath}");
                return;
            }

            ShowResultImagePlaceholder(width, height);
        }

        private void ShowResultImagePlaceholder(int width, int height)
        {
            var placeholder = _resultImagePlaceholderCache.GetOrCreate(width, height);
            if (_resultImagePlaceholderCache.IsCurrent(ImageView.ImageShow.Source, width, height))
                return;

            ImageView.Clear();
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, width, nameof(AlgorithmView), "历史算法结果坐标空间宽度");
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, height, nameof(AlgorithmView), "历史算法结果坐标空间高度");
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, width, nameof(AlgorithmView), "历史算法结果图像像素宽度");
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, height, nameof(AlgorithmView), "历史算法结果图像像素高度");
            ImageView.SetImageSource(placeholder, enableEditorImageServices: false, configureDefaultLayerController: false);
            ImageView.UpdateZoomAndScale();
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
            }
        }


        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            var listView = IsExchange ? listView1 : listViewSide;

            listView.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }
        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (ListRow2.ActualHeight > 32)
            {
                var listView = !IsExchange ? listView1 : listViewSide;
                listView.Height = ListRow2.ActualHeight - 32;
                ListRow2.Height = GridLength.Auto;
                ListRow1.Height = new GridLength(1, GridUnitType.Star);

            }
        }


        private void Button_Delete_Click(object sender, RoutedEventArgs e) => ViewResults.Clear();
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listViewSide.View is GridView gridView && LeftGridViewColumnVisibilitys.Count ==0)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns, LeftGridViewColumnVisibilitys);
        }
        bool IsExchange;
        private void Exchange_Click(object sender, RoutedEventArgs e)
        {
            IsExchange = !IsExchange;
            var listD = IsExchange ? listView1 : listViewSide;
            var listL = IsExchange ? listViewSide : listView1;
            if (listD.Parent is Grid parent1 && listL.Parent is Grid parent2 )
            {
                var tempCol = Grid.GetColumn(listD);
                var tempRow = Grid.GetRow(listD);   

                var tempCol1 = Grid.GetColumn(listL);
                var tempRow1 = Grid.GetRow(listL);

                parent1.Children.Remove(listD);
                parent2.Children.Remove(listL);

                parent1.Children.Add(listL);
                parent2.Children.Add(listD);

                Grid.SetColumn(listD, tempCol1);
                Grid.SetRow(listD, tempRow1);

                Grid.SetColumn(listL, tempCol);
                Grid.SetRow(listL, tempRow);


                listD.Width = listL.ActualWidth;
                listL.Height = listD.ActualHeight;
                listD.Height = double.NaN;
                listL.Width = double.NaN;
            }
        }

        public void SideSave(ViewResultAlg result,string selectedPath)
        {
            AlgorithmResultDataSaver.Save(ViewResultContext, result, selectedPath);
        }

        private void SideSave_Click(object sender, RoutedEventArgs e)
        {
            AlgorithmResultDataSaver.PromptAndSave(ViewResultContext, listView1.SelectedItems.Cast<ViewResultAlg>());
        }

        private void SaveSideDataCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _isInitialized && !_isDisposed && e.Parameter is ViewResultAlg result && AlgorithmResultDataSaver.CanSave(result);
            e.Handled = true;
        }

        private void SaveSideDataCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is ViewResultAlg result)
                AlgorithmResultDataSaver.PromptAndSave(ViewResultContext, new[] { result });
            e.Handled = true;
        }

        private void AlgorithmResult_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is ListViewItem { DataContext: ViewResultAlg result })
                AlgorithmResultDataSaver.EnsureContextMenu(result);
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ViewResultAlg>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_messageSubscribed && Device != null)
            {
                Device.DService.MsgReturnReceived -= DeviceService_OnMessageRecved;
                _messageSubscribed = false;
            }
            _localResultSubscription?.Dispose();
            _localResultSubscription = null;

            listView1.SelectionChanged -= listView1_SelectionChanged;
            listView1.PreviewKeyDown -= listView1_PreviewKeyDown;
            listView1.ItemsSource = null;
            listView1.CommandBindings.Clear();
            listViewSide.ItemsSource = null;
            ImageView.Dispose();
            Grid1.Children.Remove(ImageView);
            DataContext = null;

            GC.SuppressFinalize(this);
        }

        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            var query = db.Queryable<AlgResultMasterModel>();
            query = query.OrderBy(x => x.Id, Config.OrderByType);
            var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
            foreach (var item in dbList)
            {
                ViewResultAlg ViewResultAlg = new ViewResultAlg(item);
                ViewResults.Add(ViewResultAlg);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            GenericQuery<AlgResultMasterModel, ViewResultAlg> genericQuery = new GenericQuery<AlgResultMasterModel, ViewResultAlg>(Db, ViewResults, t => new ViewResultAlg(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
            Db.Dispose();
        }
    }
}
