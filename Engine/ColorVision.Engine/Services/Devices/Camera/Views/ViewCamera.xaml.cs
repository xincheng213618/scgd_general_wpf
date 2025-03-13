using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Messages;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using log4net;
using MQTTMessageLib.Camera;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Templates.Flow;
using System.Linq;
using CVCommCore;
using ColorVision.ImageEditor;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{

    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCamera : UserControl, IView
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public View View { get; set; }

        public DeviceCamera Device { get; set; }

        public static ViewCameraConfig Config => ViewCameraConfig.Instance;
        public static ObservableCollection<ViewResultCamera> ViewResults => Config.ViewResults;

        public ViewCamera(DeviceCamera device)
        {
            Device = device;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(EngineCommands.TakePhotoCommand, (s,e) => device.DisplayCameraControlLazy.Value.GetData_Click(s,e), (s, e) => e.CanExecute = Device.Config.DeviceStatus == DeviceStatusType.Opened));
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this ;
            View = new View();

            ImageView.SetConfig(new ImageViewConfig());

            ImageView.ImageViewModel.ToolBarScaleRuler.ScalRuler.ActualLength = Device.Config.ScaleFactor;
            ImageView.ImageViewModel.ToolBarScaleRuler.ScalRuler.PhysicalUnit = Device.Config.ScaleFactorUnit;
            ImageView.ImageViewModel.ToolBarScaleRuler.ScalRuler.PropertyChanged += (s, e) =>
            {
                if (s is DrawingVisualScaleHost host)
                {
                    if (e.PropertyName == "ActualLength")
                    {
                        Device.Config.ScaleFactor = host.ActualLength;
                        Device.SaveConfig();
                    }
                    else if (e.PropertyName == "PhysicalUnit")
                    {
                        Device.Config.ScaleFactorUnit = host.PhysicalUnit;
                        Device.SaveConfig();
                    }
                }
            };

            listView1.ItemsSource = ViewResults;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            Device.DService.MsgReturnReceived += DeviceService_OnMessageRecved;

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
        }

        private void Delete()
        {
            if (listView1.SelectedItems.Count == listView1.Items.Count)
                ViewResults.Clear();
            else
            {
                listView1.SelectedIndex = -1;
                foreach (var item in listView1.SelectedItems.Cast<ViewResultCamera>().ToList())
                    ViewResults.Remove(item);
            }
        }

        private void DeviceService_OnMessageRecved(MsgReturn arg)
        {
            if (arg.DeviceCode != Device.Config.Code) return;

            switch (arg.EventName)
            {
                case MQTTCameraEventEnum.Event_GetData:
                    int masterId = Convert.ToInt32(arg.Data.MasterId);
                    List<MeasureImgResultModel> resultMaster = null;
                    if (masterId > 0)
                    {
                        resultMaster = new List<MeasureImgResultModel>();
                        MeasureImgResultModel model = MeasureImgResultDao.Instance.GetById(masterId);
                        if (model != null)
                            resultMaster.Add(model);
                    }
                    if (resultMaster != null)
                    {
                        foreach (MeasureImgResultModel result in resultMaster)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                ShowResult(result);
                            });
                        }
                    }
                    break;
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0&& listView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        string Name = item.ColumnName.ToString();
                        if (Name == Properties.Resources.SerialNumber1)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByID(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.CreateTime)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByCreateTime(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.BatchNumber)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByBatch(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.File)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByFilePath(item.IsSortD);
                        }
                    }
                }
            }
        }

        private void Button_Click_Export(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox1.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            //dialog.Filter = "files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            dialog.FileName = dialog.FileName + ".csv";
            CsvWriter.WriteToCsv(ViewResults[listView1.SelectedIndex], dialog.FileName);
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                var data = ViewResults[listView1.SelectedIndex];
                if (string.IsNullOrWhiteSpace(data.FileUrl)) return;

                if (data.FileUrl.Equals(ImageView.Config.FilePath, StringComparison.Ordinal)) return;

                if (File.Exists(data.FileUrl))
                {
                    Task.Run(async() =>
                    {
                        try
                        {
                            var fileInfo = new FileInfo(data.FileUrl);
                            log.Warn($"fileInfo.Length{fileInfo.Length}");
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                log.Warn("文件可以读取，没有被占用。");
                            }
                            if (fileInfo.Length > 0)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ImageView.OpenImage(data.FileUrl);
                                });
                            }
                        }
                        catch
                        {
                            log.Warn("文件还在写入");
                            await Task.Delay(Config.ViewImageReadDelay);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImageView.OpenImage(data.FileUrl);
                            });
                        }
                    });
                }
          
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

        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCamera result = new(model);

            ViewResults.AddUnique(result, Config.InsertAtBeginning);
            if (Config.AutoRefreshView && (!FlowConfig.Instance.FlowRun || FlowConfig.Instance.AutoRefreshView))
            {
                if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning ? 0 : listView1.Items.Count - 1;
                listView1.ScrollIntoView(listView1.SelectedItem);
            }
        }
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.Instance.GetAll(Config.SearchLimit);
            if (!Config.InsertAtBeginning)
                algResults.Reverse();
            foreach (var item in algResults)
            {
                ViewResultCamera algorithmResult = new(item);
                ViewResults.AddUnique(algorithmResult);
            }
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            AdvanceSearch advanceSearch = new AdvanceSearch(MeasureImgResultDao.Instance) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            advanceSearch.Closed +=(s,e) =>
            {
                if (!Config.InsertAtBeginning)
                    advanceSearch.SearchResults.Reverse();
                ViewResults.Clear();

                foreach (var item in advanceSearch.SearchResults)
                {
                    ViewResultCamera algorithmResult = new ViewResultCamera(item);
                    ViewResults.AddUnique(algorithmResult);
                }
            };
            advanceSearch.Show();
        }



        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = MainGridRow2.ActualHeight - 32;
            MainGridRow1.Height = new GridLength(1, GridUnitType.Star);
            MainGridRow2.Height = GridLength.Auto;
        }
    }
}
