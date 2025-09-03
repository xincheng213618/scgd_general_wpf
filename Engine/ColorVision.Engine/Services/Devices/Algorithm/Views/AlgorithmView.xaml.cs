#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using CVCommCore.CVAlgorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView,IDisposable, IViewImageA
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }
        public ImageView ImageView { get; set; }

        public ListView ListView { get; set; }

        public TextBox SideTextBox { get; set; }

        public AlgorithmView()
        {
            InitializeComponent();
        }

        private NetFileUtil netFileUtil;

        public ViewAlgorithmConfig Config => ViewAlgorithmConfig.Instance;

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            ImageView = new ImageView();
            ListView = listViewSide;
            SideTextBox = TextBoxside;
            Grid1.Children.Add(ImageView);
            View = new View();
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.ItemsSource = ViewResults;

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
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


        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OpenImage(arg.FileData);
                });
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

        public ObservableCollection<ViewResultAlg> ViewResults => Config.ViewResults;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;

  
            if (listView1.SelectedIndex < 0 ||listView1.Items[listView1.SelectedIndex] is not ViewResultAlg result)
            {
                MessageBox.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
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

        public void AlgResultMasterModelDataDraw(AlgResultMasterModel result)
        {
            if (result != null)
            {
                ViewResultAlg ViewResultAlg = new ViewResultAlg(result);

                var ResultHandle = DisplayAlgorithmManager.GetInstance().ResultHandles.FirstOrDefault(a => a.CanHandle1(ViewResultAlg));
                    ResultHandle?.Load(this,ViewResultAlg);

                ViewResults.AddUnique(ViewResultAlg, Config.InsertAtBeginning);
                if (Config.AutoRefreshView)
                    RefreshResultListView();
                if (Config.AutoSaveSideData)
                    SideSave(ViewResultAlg, Config.SaveSideDataDirPath);
            }
        }

        public void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning? 0: listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;
            if (ViewResults[listView1.SelectedIndex] is not ViewResultAlg result) return;
            var ResultHandle = DisplayAlgorithmManager.GetInstance().ResultHandles.FirstOrDefault(a => a.CanHandle1(result));
            if (ResultHandle != null)
            {
                ImageView.ImageShow.Clear();
                SideTextBox.Visibility = Visibility.Collapsed;
                SideTextBox.Clear();
                ResultHandle.Load(this, result);
                ResultHandle.Handle(this, result);
                return;
            }

            ImageView.ImageShow.Clear();
            if (File.Exists(result.FilePath))
                ImageView.OpenImage(result.FilePath);

            List<POIPoint> DrawPoiPoint = new();
            List<string> header = new();
            List<string> bdHeader = new();

            switch (result.ResultType)
            {
                case ViewResultAlgType.POI_XYZ_File:
                case ViewResultAlgType.POI_Y_File:
                    header = new List<string> { "file_name", "FileUrl", "FileType" };
                    bdHeader = new List<string> { "FileName", "FileUrl", "FileType", };
                    break;
                case ViewResultAlgType.POI:
                    if (result.ViewResults == null)
                    {
                        result.ViewResults = new ObservableCollection<IViewResult>();
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultData poiResult = new(item) { Id = id++ };
                            result.ViewResults.Add(poiResult);
                        }
                    }

                    header = new List<string> { "名称", "位置", "大小", "形状" };
                    bdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes"};

                    foreach (var item in result.ViewResults)
                    {
                        if (item is PoiResultData poiResultData)
                        {
                            DrawPoiPoint.Add(poiResultData.Point);
                        }
                    }
                    AddPOIPoint(DrawPoiPoint);
                    break;
                default:
                    break;
            }

            if (listViewSide.View is GridView gridView)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                listViewSide.ItemsSource = result.ViewResults;
            }
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

        internal void OpenImage(CVCIEFile fileInfo)
        {
            ImageView.OpenImage(fileInfo.FilePath);
        }


        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
        }

        public async void AddPOIPoint(List<POIPoint> PoiPoints)
        {
            ImageView.ImageShow.Clear();
            await Task.Delay(1000);
            for (int i = 0; i < PoiPoints.Count; i++)
            {
                if (i % 10000 == 0)
                    await Task.Delay(30);

                var item = PoiPoints[i];
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        CircleTextProperties circleTextProperties = new CircleTextProperties();
                        circleTextProperties.Center = new Point(item.PixelX, item.PixelY);
                        circleTextProperties.Radius = item.Radius;
                        circleTextProperties.Brush = Brushes.Transparent;
                        circleTextProperties.Pen = new Pen(Brushes.Red, 1);
                        circleTextProperties.Id = item.Id ?? -1;
                        circleTextProperties.Text = item.Name;

                        DVCircleText Circle = new DVCircleText(circleTextProperties);
                        Circle.Render();
                        ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                        rectangleTextProperties.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        rectangleTextProperties.Brush = Brushes.Transparent;
                        rectangleTextProperties.Pen = new Pen(Brushes.Red, 1);
                        rectangleTextProperties.Id = item.Id ?? -1;
                        rectangleTextProperties.Text = item.Name;

                        DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
                        Rectangle.Render();
                        ImageView.AddVisual(Rectangle);
                        break;
                    case POIPointTypes.SolidPoint:
                        CircleProperties circleProperties = new CircleProperties();
                        circleProperties.Center = new Point(item.PixelX, item.PixelY);
                        circleProperties.Radius = 10;
                        circleProperties.Brush = Brushes.Red;
                        circleProperties.Pen = new Pen(Brushes.Red, 1);
                        circleProperties.Id = item.Id ?? -1;

                        DVCircle Circle1 = new DVCircle(circleProperties);
                        Circle1.Render();
                        ImageView.AddVisual(Circle1);
                        break;
                    default:
                        break;
                }
            }
        }


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
            var ResultHandle = DisplayAlgorithmManager.GetInstance().ResultHandles.FirstOrDefault(a => a.CanHandle.Contains(result.ResultType));
            if (ResultHandle != null)
            {
                ResultHandle.SideSave(result,selectedPath);
                return;
            }
        }

        private void SideSave_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "请选择保存文件的文件夹";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                string selectedPath = dialog.SelectedPath;

                foreach (var selectedItem in listView1.SelectedItems)
                {
                    if (selectedItem is ViewResultAlg result)
                    {
                        SideSave(result, selectedPath);
                    }
                }
            }
            else
            {
                MessageBox.Show("您需要先选择数据");
            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewResultAlg);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    string displayName = attribute?.DisplayName ?? property.Name;
                    displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                    if (displayName == gridViewColumnHeader.Content.ToString())
                    {
                        var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                        if (item != null)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByProperty(property.Name, item.IsSortD);
                            break;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            ImageView?.Dispose();

            GC.SuppressFinalize(this);
        }

        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            var query = MySqlControl.GetInstance().DB.Queryable<AlgResultMasterModel>();
            query = query.OrderBy(x => x.Id, Config.OrderByType);
            var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
            foreach (var item in dbList)
            {
                ViewResultAlg ViewResultAlg = new ViewResultAlg(item);
                ViewResults.AddUnique(ViewResultAlg);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            GenericQuery<AlgResultMasterModel, ViewResultAlg> genericQuery = new GenericQuery<AlgResultMasterModel, ViewResultAlg>(MySqlControl.GetInstance().DB, ViewResults, t => new ViewResultAlg(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }
    }
}
