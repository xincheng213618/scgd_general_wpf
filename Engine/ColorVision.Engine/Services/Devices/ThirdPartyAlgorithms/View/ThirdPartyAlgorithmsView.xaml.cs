#pragma  warning disable CA1708,CS8602,CS8604,CS8629,CA1822
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.UI.Sorts;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views
{

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ThirdPartyAlgorithmsView : UserControl
    {
        private static readonly ILog logg = LogManager.GetLogger(typeof(ThirdPartyAlgorithmsView));
        public ThirdPartyAlgorithmsView()
        {
            InitializeComponent();
        }

        public static ViewThirdPartyAlgorithmsConfig Config => ViewThirdPartyAlgorithmsConfig.Instance;

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.ItemsSource = ViewResults;

        }


        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
            }
        }

        public ObservableCollection<ViewResultAlg> ViewResults { get; set; } = new ObservableCollection<ViewResultAlg>();

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

                switch (result.ResultType)
                {   
                    case ViewResultAlgType.POI_XYZ:
                        var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
                        PoiResultCIExyuvDatas.SaveCsv(dialog.FileName);
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

        public void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning? 0: listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedItem is not ViewResultAlg result)
            {
                ImageView.Clear();
                listViewSide.ItemsSource = null;
                return;
            }

            ImageView.ImageShow.Clear();
            ImageView.OpenImage(result.FilePath);

            if (listViewSide.View is GridView gridView)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
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
            ImageView.OpenImage(fileInfo.ToWriteableBitmap());
        }


        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
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
                dialog.Description = Properties.Resources.SelectSaveFolder;
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
                MessageBox.Show(Properties.Resources.SelectDataFirst);
            }
        }

        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            var query = Db.Queryable<AlgResultMasterModel>();
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

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }
    }
}
