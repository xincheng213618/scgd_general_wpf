#pragma warning disable CS8604,CS8629,CS8602
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using log4net;
using MQTTMessageLib.Calibration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;


namespace ColorVision.Engine.Services.Devices.Calibration.Views
{
    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCalibration : UserControl, IView
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewCalibration));

        public View View { get; set; }

        public  MQTTCalibration DeviceService => Device.DService;
        public DeviceCalibration Device { get; set; }

        public ViewCalibration(DeviceCalibration device)
        {
            Device = device;
            InitializeComponent();
        }
        public static ViewCalibrationConfig Config => ViewCalibrationConfig.Instance;
        public static ObservableCollection<ViewResultCamera> ViewResults => Config.ViewResults;

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;
            View = new View();
            ImageView.SetConfig(Config.ImageViewConfig);
            listView1.ItemsSource = ViewResults;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            DeviceService.MsgReturnReceived += DeviceService_OnMessageRecved;

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
                foreach (var item in listView1.SelectedItems.Cast<ViewResultCamera>().ToList())
                    ViewResults.Remove(item);
            }
        }
        private void DeviceService_OnMessageRecved(MsgReturn arg)
        {
            switch (arg.EventName)
            {
                case MQTTCalibrationEventEnum.Event_GetData:
                    if (arg.Data == null) return;
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
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ShowResult(result);
                            });
                        }
                    }
                    break;
            }

        }


        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCamera result = new(model);
            ViewResults.AddUnique(result);
            if (Config.AutoRefreshView)
            {
                if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
                listView1.ScrollIntoView(listView1.SelectedItem);
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0&& listView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }

        private void Button_Click_ShowResultGrid(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView1.Visibility = visibility;
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
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            CsvWriter.WriteToCsv(ViewResults[listView1.SelectedIndex], dialog.FileName);
            ImageSource bitmapSource = ImageView.ImageShow.Source;
            ImageUtils.SaveImageSourceToFile(bitmapSource, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
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
                    Task.Run(async () =>
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

        public void OpenImage(CVCIEFile fileData)
        {
            ImageView.OpenImage(fileData.ToWriteableBitmap());
        }


        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.Instance.GetAllDevice(Device.Code, Config.SearchLimit);
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
            AdvanceSearchConfig.Instance.Limit = Config.SearchLimit;
            AdvanceSearch advanceSearch = new AdvanceSearch(MeasureImgResultDao.Instance) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            advanceSearch.Closed += (s, e) =>
            {
                if (Config.InsertAtBeginning)
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

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewResult)
            {
                ViewResults.Remove(viewResult);
                ImageView.Clear();
            }
        }


        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewResultCamera);

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
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
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

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = MainGridRow2.ActualHeight - 32;
            MainGridRow1.Height = new GridLength(1, GridUnitType.Star);
            MainGridRow2.Height = GridLength.Auto;
        }
    }
}
