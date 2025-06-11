#pragma  warning disable CA1708,CS8602,CS8604,CS8629,CA1822
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.Engine.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Net;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using CVCommCore.CVAlgorithm;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Abstractions;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views
{

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ThirdPartyAlgorithmsView : UserControl,IView
    {
        private static readonly ILog logg = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }

        public ThirdPartyAlgorithmsView()
        {
            InitializeComponent();
        }

        private NetFileUtil netFileUtil;

        public static ViewThirdPartyAlgorithmsConfig Config => ViewThirdPartyAlgorithmsConfig.Instance;
        public ObservableCollection<IResultHandleBase> ResultHandles { get; set; } = new ObservableCollection<IResultHandleBase>();


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IResultHandleBase).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IResultHandleBase  algorithmResultRender)
                    {
                        ResultHandles.Add(algorithmResultRender);
                    }
                }
            }
            this.DataContext = this;

            View = new View();
            ImageView.SetConfig(Config.ImageViewConfig);
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.ItemsSource = ViewResults;
            var keyValuePairs =
            TextBoxType.ItemsSource = Enum.GetValues(typeof(AlgorithmResultType))
                .Cast<AlgorithmResultType>()
                .Select(e1 => new KeyValuePair<AlgorithmResultType, string>(e1, e1.ToString()))
                .ToList();

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
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

        public ObservableCollection<AlgorithmResult> ViewResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;

  
            if (listView1.SelectedIndex < 0 ||listView1.Items[listView1.SelectedIndex] is not AlgorithmResult result)
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

                switch (result.ResultType)
                {   
                    case AlgorithmResultType.POI_XYZ:
                        var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
                        PoiResultCIExyuvData.SaveCsv(PoiResultCIExyuvDatas, dialog.FileName);
                        ImageUtils.SaveImageSourceToFile(ImageView.ImageShow.Source, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
                        return;
                    default:
                        break;
                }

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
                        + item.Result + ","
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
                AlgorithmResult algorithmResult = new AlgorithmResult(result);
                ViewResults.AddUnique(algorithmResult, Config.InsertAtBeginning);
                if (Config.AutoRefreshView)
                    RefreshResultListView();
                if (Config.AutoSaveSideData)
                    SideSave(algorithmResult, Config.SaveSideDataDirPath);
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

            //var ResultHandle = ResultHandles.FirstOrDefault(a => a.CanHandle.Contains(ViewResults[listView1.SelectedIndex].ResultType));
            //if (ResultHandle != null)
            //{
            //    ResultHandle.Handle(this,ViewResults[listView1.SelectedIndex]);
            //    return;
            //}

            if (ViewResults[listView1.SelectedIndex] is AlgorithmResult result)
            {
                ImageView.ImageShow.Clear();
                if (File.Exists(result.FilePath))
                    ImageView.OpenImage(result.FilePath);

                List<POIPoint> DrawPoiPoint = new();
                List<string> header = new();
                List<string> bdHeader = new();

                switch (result.ResultType)
                {
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
        }

        public void AddPoint(List<Point> points)
        {
            int id = 0;
            foreach (var item in points)
            {
                id++;
                DVCircleText Circle = new();
                Circle.Attribute.Center = item;
                Circle.Attribute.Radius = 20 / ImageView.Zoombox1.ContentMatrix.M11;
                Circle.Attribute.Brush = Brushes.Transparent;
                Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                Circle.Attribute.Id = id;
                Circle.Render();
                ImageView.AddVisual(Circle);
            }
        }

        public void AddRect(Rect rect)
        {
            DVRectangleText Rectangle = new();
            Rectangle.Attribute.Rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            Rectangle.Attribute.Brush = Brushes.Transparent;
            Rectangle.Attribute.Pen = new Pen(Brushes.Red, rect.Width / 30.0);
            Rectangle.Render();
            ImageView.AddVisual(Rectangle);
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
            ImageView.OpenImage(fileInfo.ToWriteableBitmap());
        }


        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
        }

        private void Search_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxType.SelectedIndex = -1;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text)&& string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxType.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && SearchTimeSart.SelectedDateTime ==DateTime.MinValue)
            {
                ViewResults.Clear();
                List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.GetAll(Config.SearchLimit);
                if (!Config.InsertAtBeginning)
                    algResults.Reverse();
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new(item);
                    ViewResults.AddUnique(algorithmResult);
                }
                SerchPopup.IsOpen = false;
                return;
            }
            else
            {
                string altype = string.Empty;
                if (TextBoxType.SelectedValue is AlgorithmResultType algorithmResultType)
                    altype = ((int)algorithmResultType).ToString();
                ViewResults.Clear();
                List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, altype.ToString(), TextBoxFile.Text ,SearchTimeSart.SelectedDateTime,SearchTimeEnd.SelectedDateTime,Config.SearchLimit);
                if (Config.InsertAtBeginning)
                    algResults.Reverse();
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new(item);
                    ViewResults.AddUnique(algorithmResult);
                }
            }
            SerchPopup.IsOpen = false;
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
                        DVCircleText Circle = new();
                        Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle.Attribute.Radius = item.Radius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle.Attribute.Id = item.Id ?? -1;
                        Circle.Attribute.Text = item.Name;
                        Circle.Render();
                        ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText Rectangle = new();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Rectangle.Attribute.Id = item.Id ?? -1;
                        Rectangle.Attribute.Text = item.Name;
                        Rectangle.Render();
                        ImageView.AddVisual(Rectangle);
                        break;
                    case POIPointTypes.SolidPoint:
                        DVCircle Circle1 = new();
                        Circle1.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle1.Attribute.Radius = 10;
                        Circle1.Attribute.Brush = Brushes.Red;
                        Circle1.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle1.Attribute.Id = item.Id ?? -1;
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

        public void SideSave(AlgorithmResult result,string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            try
            {

            }
            catch (Exception ex)
            {
                logg.Error(ex);
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
                    if (selectedItem is AlgorithmResult result)
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

        private void ButtonChart_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                if (listView1.Items[listView1.SelectedIndex] is AlgorithmResult result)
                {
                    if (result.ResultType == AlgorithmResultType.POI_XYZ)
                    {
                        var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
                        if (PoiResultCIExyuvDatas.Count !=0)
                        {
                            WindowChart windowChart = new(PoiResultCIExyuvDatas);
                            windowChart.Show();
                        }
                        else
                        {
                            MessageBox.Show("结果为空");
                        }
                    }
                    else
                    {
                        MessageBox.Show("暂不支持其他");
                    }
                }
            }
            else
            {
                MessageBox.Show("没有选择条目");
            }
        }


    }
}
