#pragma  warning disable CA1708,CS8602,CS8604
using ColorVision.MVVM;
using HandyControl.Tools.Extension;
using log4net;
using log4net.Repository.Hierarchy;
using MQTTMessageLib.Algorithm;
using Org.BouncyCastle.Utilities;
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

namespace ColorVision.Services.Algorithm
{
    public class PoiResult : ViewModelBase
    {
        private int _Id;
        private string _SerialNumber;
        private string _ImgFileName;
        private string _POITemplateName;
        private string _RecvTime;
        private AlgorithmResultType _ResultType;
        private ObservableCollection<PoiResultData> _PoiData;

        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; NotifyPropertyChanged(); } }
        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; NotifyPropertyChanged(); } }
        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        public string RecvTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }

        public string ResultTypeDis { get {
                string result = "";
                switch (_ResultType)
                {
                    case AlgorithmResultType.POI_XY_UV:
                        result = "色度";
                        break;
                    case AlgorithmResultType.POI_Y:
                        result = "亮度";
                        break;
                    default:
                        break;
                }
                return result; } }
        public AlgorithmResultType ResultType
        {
            get { return _ResultType; }
            set { _ResultType = value; }
        }
        public ObservableCollection<PoiResultData> PoiData { get { return _PoiData; } set { _PoiData = value; NotifyPropertyChanged(); } }

        public PoiResult()
        {
            this._PoiData = new ObservableCollection<PoiResultData>();
        }

        public PoiResult(int id, string serialNumber, string imgFileName, string pOITemplateName, string recvTime, AlgorithmResultType resultType) : this()
        {
            _Id = id;
            _SerialNumber = serialNumber;
            _ImgFileName = imgFileName;
            _POITemplateName = pOITemplateName;
            _RecvTime = recvTime;
            _ResultType = resultType;
        }
    }

    public class PoiResultData:ViewModelBase
    {
        public POIPoint Point { get { return POIPoint; } set { POIPoint = value; NotifyPropertyChanged(); } }

        public string Name { get { return POIPoint.Name; } }
        public string PixelPos { get { return string.Format("{0},{1}", POIPoint.PixelX, POIPoint.PixelY); } }
        public string PixelSize { get { return string.Format("{0},{1}", POIPoint.Width, POIPoint.Height); } }

        public string Shapes { get { return string.Format("{0}", POIPoint.PointType == 0 ? "圆形" : "矩形"); } }

        protected POIPoint POIPoint { get; set; }
    }
    public class PoiResultCIExyuvData : PoiResultData
    {
        public double CCT { get { return _CCT; } set { _CCT = value; NotifyPropertyChanged(); } }
        public double Wave { get { return _Wave; } set { _Wave = value; NotifyPropertyChanged(); } }
        public double X { get { return _X; } set { _X = value; NotifyPropertyChanged(); } }
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        public double Z { get { return _Z; } set { _Z = value; NotifyPropertyChanged(); } }
        public double u { get { return _u; } set { _u = value; NotifyPropertyChanged(); } }
        public double v { get { return _v; } set { _v = value; NotifyPropertyChanged(); } }
        public double x { get { return _x; } set { _x = value; NotifyPropertyChanged(); } }
        public double y { get { return _y; } set { _y = value; NotifyPropertyChanged(); } }

        private double _y;
        private double _x;
        private double _u;
        private double _v;
        private double _X;
        private double _Y;
        private double _Z;
        private double _Wave;
        private double _CCT;

        public PoiResultCIExyuvData(POIPoint point, POIDataCIExyuv data)
        {
            this.Point = point;
            this.u = data.u;
            this.v = data.v;
            this.x = data.x;
            this.y = data.y;
            this.X = data.X;
            this.Y = data.Y;
            this.Z = data.Z;
            this.CCT = data.CCT;
            this.Wave = data.Wave;
        }
    }

    public class PoiResultCIEYData : PoiResultData
    {
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }

        private double _Y;

        public PoiResultCIEYData(POIPoint point, POIDataCIEY data)
        {
            this.Point = point;
            this.Y = data.Y;
        }
    }

    public delegate void CurSelectionChanged(PoiResult data);

    /// <summary>
    /// SpectrumView.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }
        public event CurSelectionChanged OnCurSelectionChanged;
        public AlgorithmView()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new TextBox() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            this.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

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
            List<string> headers = new List<string> { "序号", "批次号", "模板", "图像数据文件", "测量时间", "类型" };
            List<string> bdheaders = new List<string> { "Id", "SerialNumber", "POITemplateName", "ImgFileName", "RecvTime", "ResultTypeDis" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            listView1.View = gridView;
            //色度
            List<string> cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };
            List<string> cieHeader = new List<string> { "名称", "位置", "大小", "形状", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };

            GridView gridView2 = new GridView();
            for (int i = 0; i < cieHeader.Count; i++)
            {
                gridView2.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }
            listView2.View = gridView2;
            listView2.ItemsSource = PoiResultDatas;
            //亮度
            List<string> bdheadersY = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "Y" };
            List<string> headersY = new List<string> { "名称", "位置", "大小", "形状", "Y" };
            GridView gridViewY = new GridView();
            for (int i = 0; i < headersY.Count; i++)
            {
                gridViewY.Columns.Add(new GridViewColumn() { Header = headersY[i], DisplayMemberBinding = new Binding(bdheadersY[i]) });
            }
            listViewY.View = gridViewY;
            listViewY.ItemsSource = PoiYResultDatas;
            //
            listView1.ItemsSource = PoiResults;
        }

        public ObservableCollection<PoiResultData> PoiResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<PoiResultData> PoiYResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<PoiResult> PoiResults { get; set; } = new ObservableCollection<PoiResult>();
        public Dictionary<string,PoiResult> resultDis { get; set; } = new Dictionary<string, PoiResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show("您需要先选择数据", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                foreach (var item in ListContents[listView1.SelectedIndex])
                {
                    value += item + ",";
                }
                file.WriteLine(value);
            }
        }

        //TODO: 需要新增亮度listview
        public void PoiDataDraw(string serialNumber, string templateName, string POIImgFileName, List<POIResultCIEY> poiResultData)
        {
            if (!resultDis.ContainsKey(serialNumber))
            {
                PoiResult result = new PoiResult(PoiResults.Count + 1, serialNumber, POIImgFileName, templateName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), AlgorithmResultType.POI_Y);
                PoiResults.Add(result);
                resultDis[serialNumber] = result;
                foreach (var item in poiResultData)
                {
                    try
                    {
                        PoiResultCIEYData poiResult = new PoiResultCIEYData(item.Point, item.Data);
                        result.PoiData.Add(poiResult);
                    }
                    catch
                    {

                    }
                }
            }
            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
        }
        public void PoiDataDraw(string serialNumber, string templateName, string POIImgFileName, List<POIResultCIExyuv> poiResultData)
        {
            if (!resultDis.ContainsKey(serialNumber))
            {
                PoiResult result = new PoiResult(PoiResults.Count + 1, serialNumber, POIImgFileName, templateName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), AlgorithmResultType.POI_XY_UV);
                PoiResults.Add(result);
                resultDis[serialNumber] = result;
                foreach (var item in poiResultData)
                {
                    try
                    {
                        PoiResultCIExyuvData poiResult = new PoiResultCIExyuvData(item.Point, item.Data);
                        result.PoiData.Add(poiResult);
                    }
                    catch
                    {

                    }
                }
            }
            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
        }
        private List<List<string>> ListContents { get; set; } = new List<List<string>>() { };

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PoiResult data = listView1.Items[listView1.SelectedIndex] as PoiResult;
            if(data != null)
            {
                OnCurSelectionChanged?.Invoke(data);
                PoiResultDatas.Clear();
                PoiYResultDatas.Clear();
                img_view.ResetPOIPoint();
                if (data.ResultType == AlgorithmResultType.POI_XY_UV)
                {
                    listViewY.Hide();
                    listView2.Show();
                    foreach (var item in data.PoiData)
                    {
                        PoiResultDatas.Add(item);
                    }
                    img_view.AddPOIPoint(PoiResultDatas);
                }
                else if (data.ResultType == AlgorithmResultType.POI_Y)
                {
                    listView2.Hide();
                    listViewY.Show();
                    foreach (var item in data.PoiData)
                    {
                        PoiYResultDatas.Add(item);
                    }
                    img_view.AddPOIPoint(PoiYResultDatas);
                }
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {

        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {

        }

        public void OpenImage(byte[] bytes)
        {
            logger.Info("OpenImage .....");
            img_view.OpenImage(bytes);
            logger.Info("OpenImage end");
        }

        private void listViewY_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        internal void OpenImage(CVCIEFileInfo fileInfo)
        {
            logger.Info("OpenImage CVCIEFileInfo .....");
            img_view.OpenImage(fileInfo);
            logger.Info("OpenImage end");
        }
    }
}
