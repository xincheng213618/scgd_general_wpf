#pragma warning disable CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using FileServerPlugin;
using log4net;
using MQTTMessageLib.Camera;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Services.Device.Camera
{
    /// <summary>
    /// CameraView.xaml 的交互逻辑
    /// </summary>
    public partial class CameraView : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(CameraView));
        public View View { get; set; }
        public event ImgCurSelectionChanged OnCurSelectionChanged;

        public ObservableCollection<CameraImgResult> Results { get; set; } = new ObservableCollection<CameraImgResult>();
        public Dictionary<string, CameraImgResult> resultDis { get; set; } = new Dictionary<string, CameraImgResult>();
        public CameraView()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            View = new View();
            View.ViewIndexChangedEvent += (s, e) =>
            {
                if (e == -2)
                {
                    MenuItem menuItem3 = new MenuItem { Header = "还原到主窗口中" };
                    menuItem3.Click += (s, e) =>
                    {
                        if (ViewGridManager.GetInstance().IsGridEmpty(View.PreViewIndex))
                        {
                            View.ViewIndex = View.PreViewIndex;
                        }
                        else
                        {
                            View.ViewIndex = -1;
                        }
                    };
                    this.ContextMenu = new ContextMenu();
                    this.ContextMenu.Items.Add(menuItem3);

                }
                else
                {
                    MenuItem menuItem = new MenuItem() { Header = "设为主窗口" };
                    menuItem.Click += (s, e) => { ViewGridManager.GetInstance().SetOneView(this); };
                    MenuItem menuItem1 = new MenuItem() { Header = "展示全部窗口" };
                    menuItem1.Click += (s, e) => { ViewGridManager.GetInstance().SetViewNum(-1); };
                    MenuItem menuItem2 = new MenuItem() { Header = "独立窗口中显示" };
                    menuItem2.Click += (s, e) => { View.ViewIndex = -2; };
                    MenuItem menuItem3 = new MenuItem() { Header = Properties.Resource.WindowHidden };
                    menuItem3.Click += (s, e) => { View.ViewIndex = -1; };
                    this.ContextMenu = new ContextMenu();
                    this.ContextMenu.Items.Add(menuItem);
                    this.ContextMenu.Items.Add(menuItem1);
                    this.ContextMenu.Items.Add(menuItem2);
                    this.ContextMenu.Items.Add(menuItem3);

                }
            };

            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "参数", "图像数据文件", "图像信息", "测量时间", "用时(时:分:秒)", "结果", "描述" };
            List<string> bdheaders = new List<string> { "Id", "SerialNumber", "ReqParams", "ImgFileName", "ImgFrameInfo", "RecvTime", "TotalTime", "Result", "ResultDesc" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            listView1.View = gridView;
            listView1.ItemsSource = Results;
        }

        private void Button_Click_ShowResultGrid(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView1.Visibility = visibility;
            }
        }

        private void Button_Click_ShowRightGrid(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_Export(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using StreamWriter file = new StreamWriter(dialog.FileName, true, Encoding.UTF8);
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
                foreach (var item in Results)
                {
                    value += item.Id + ","
                        + item.SerialNumber + ","
                        + item.ImgFileName + ","
                        + item.RecvTime + ","
                        + item.TotalTime + ","
                        + item.Result + ","
                        + item.ResultDesc + ","
                        + Environment.NewLine;
                }
                file.WriteLine(value);
            }
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            Results.Clear();
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CameraImgResult data = listView1.Items[listView1.SelectedIndex] as CameraImgResult;
            if (data != null)
            {
                OnCurSelectionChanged?.Invoke(data);
            }
        }

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ToolBar toolBar)
            {
                if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
                    overflowGrid.Visibility = Visibility.Collapsed;
                if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
                    mainPanelBorder.Margin = new Thickness(0);
            }
        }
        public void OpenImage(byte[] bytes)
        {
            img_view.OpenImage(bytes);
        }
        public void OpenImage(CVCIEFileInfo fileData)
        {
            img_view.OpenImage(fileData);
        }
        public void ShowResult(MeasureImgResultModel model)
        {
            string key = model.Id.ToString();
            string serialNumber = model.BatchCode ?? string.Empty;
            string imgFileName = model.RawFile ?? string.Empty;
            int resultCode = model.ResultCode;
            string resultDesc = model.ResultDesc;
            string reqParams = model.ReqParams;
            string imgFrameInfo = model.ImgFrameInfo;
            long totalTime = model.TotalTime;
            if (!resultDis.ContainsKey(key))
            {
                CameraImgResult result = new CameraImgResult(Results.Count + 1, serialNumber, imgFileName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), reqParams, imgFrameInfo, (CameraFileType)model.FileType, resultCode, resultDesc, totalTime);
                Results.Add(result);
                resultDis[key] = result;
                RefreshResultListView();
            }
        }

        private void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }
    }


    public delegate void ImgCurSelectionChanged(CameraImgResult data);
    public class CameraImgResult : ViewModelBase
    {
        private int _Id;
        private string _SerialNumber;
        private string _ImgFileName;
        private string _Params;
        private string _ImgFrameInfo;
        private CameraFileType _FileType;
        private string _RecvTime;
        private int _resultCode;
        private long _totalTime;
        private string _resultDesc;

        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; NotifyPropertyChanged(); } }
        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; NotifyPropertyChanged(); } }
        public CameraFileType FileType { get { return _FileType; } set { _FileType = value; NotifyPropertyChanged(); } }
        public string ReqParams { get { return _Params; } set { _Params = value; NotifyPropertyChanged(); } }
        public string ImgFrameInfo { get { return _ImgFrameInfo; } set { _ImgFrameInfo = value; NotifyPropertyChanged(); } }
        public string RecvTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }

        public string Result
        {
            get
            {
                return ResultCode == 0 ? "成功" : "失败";
            }
        }
        public string TotalTime
        {
            get
            {
                return string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString().TrimEnd('0'));
            }
        }
        public int ResultCode { get { return _resultCode; } set { _resultCode = value; NotifyPropertyChanged(); } }
        public string ResultDesc { get { return _resultDesc; } set { _resultDesc = value; NotifyPropertyChanged(); } }
        public CameraImgResult()
        {
        }

        public CameraImgResult(int id, string serialNumber, string imgFileName, string recvTime, string reqParams, string imgFrameInfo, CameraFileType fileType, int? resultCode, string resultDesc, long totalTime = 0) : this()
        {
            _Id = id;
            _SerialNumber = serialNumber;
            _ImgFileName = imgFileName;
            _Params = reqParams;
            _ImgFrameInfo = imgFrameInfo;
            _FileType = fileType;
            _RecvTime = recvTime;
            _resultCode = (int)resultCode;
            _totalTime = totalTime;
            _resultDesc = resultDesc;
        }
    }


}
