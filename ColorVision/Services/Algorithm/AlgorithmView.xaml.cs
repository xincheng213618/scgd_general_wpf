#pragma  warning disable CA1708,CS8602,CS8604
using ColorVision.MySql.DAO;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Device.Algorithm
{
    public class PoiResult : INotifyPropertyChanged
    {
        private int _Id;
        private string _SerialNumber;
        private string _RecvTime;
        private POIResultType _ResultType;
        private ObservableCollection<PoiResultData> _PoiData;

        public int Id { get { return _Id; } set { _Id = value; OnPropertyChanged(new PropertyChangedEventArgs("Id")); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; OnPropertyChanged(new PropertyChangedEventArgs("SerialNumber")); } }
        public string RecvTime { get { return _RecvTime; } set { _RecvTime = value; OnPropertyChanged(new PropertyChangedEventArgs("RecvTime")); } }

        public POIResultType ResultType { get; set; }
        public ObservableCollection<PoiResultData> PoiData { get { return _PoiData; } set { _PoiData = value; OnPropertyChanged(new PropertyChangedEventArgs("PoiData")); } }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, e);
            }
        }

        public PoiResult()
        {
            this._PoiData = new ObservableCollection<PoiResultData>();
        }
    }

    public class PoiResultData
    {
        public POIPoint Point { get { return _point; } set { _point = value; OnPropertyChanged(new PropertyChangedEventArgs("Point")); } }

        public string PixelPos { get { return string.Format("{0},{1}", _point.PixelX, _point.PixelY); } }
        public string PixelSize { get { return string.Format("{0},{1}", _point.Width, _point.Height); } }

        public string Shapes { get { return string.Format("{0}", _point.PointType == 0 ? "圆形" : "矩形"); } }

        protected POIPoint _point;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, e);
            }
        }
    }
    public class PoiResultCIExyuvData : PoiResultData, INotifyPropertyChanged
    {
        public double CCT { get { return _CCT; } set { _CCT = value; OnPropertyChanged(new PropertyChangedEventArgs("CCT")); } }
        public double Wave { get { return _Wave; } set { _Wave = value; OnPropertyChanged(new PropertyChangedEventArgs("Wave")); } }
        public double X { get { return _X; } set { _X = value; OnPropertyChanged(new PropertyChangedEventArgs("X")); } }
        public double Y { get { return _Y; } set { _Y = value; OnPropertyChanged(new PropertyChangedEventArgs("Y")); } }
        public double Z { get { return _Z; } set { _Z = value; OnPropertyChanged(new PropertyChangedEventArgs("Z")); } }
        public double u { get { return _u; } set { _u = value; OnPropertyChanged(new PropertyChangedEventArgs("u")); } }
        public double v { get { return _v; } set { _v = value; OnPropertyChanged(new PropertyChangedEventArgs("v")); } }
        public double x { get { return _x; } set { _x = value; OnPropertyChanged(new PropertyChangedEventArgs("x")); } }
        public double y { get { return _y; } set { _y = value; OnPropertyChanged(new PropertyChangedEventArgs("y")); } }

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

    public class PoiResultCIEYData : PoiResultData, INotifyPropertyChanged
    {
        public double Y { get { return _Y; } set { _Y = value; OnPropertyChanged(new PropertyChangedEventArgs("Y")); } }

        private double _Y;

        public PoiResultCIEYData(POIPoint point, POIDataCIEY data)
        {
            this.Point = point;
            this.Y = data.Y;
        }
    }

    /// <summary>
    /// SpectrumView.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        public View View { get; set; }
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
            List<string> headers = new List<string> { "序号","批次号", "测量时间" };
            List<string> bdheaders = new List<string> { "Id", "SerialNumber", "RecvTime" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            listView1.View = gridView;
            List<string> headers2 = new List<string> { "PixelPos", "PixelSize", "Shapes", "CCT","Wave","X","Y","Z","u","v","x","y" };

            GridView gridView2 = new GridView();
            for (int i = 0; i < headers2.Count; i++)
            {
                gridView2.Columns.Add(new GridViewColumn() { Header = headers2[i], DisplayMemberBinding = new Binding(headers2[i]) });
            }
            listView2.View = gridView2;
            listView2.ItemsSource = PoiResultDatas;
            listView1.ItemsSource = PoiResults;
        }

        public ObservableCollection<PoiResultData> PoiResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<PoiResult> PoiResults { get; set; } = new ObservableCollection<PoiResult>();
        public Dictionary<string,PoiResult> resultDis { get; set; } = new Dictionary<string, PoiResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show("您需要先选择数据");
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

        public void PoiDataDraw(List<PoiResultModel> poiResultModels)
        {
            foreach (var item in poiResultModels)
            {
                try
                {
                    PoiResultCIExyuvData poiResult = JsonConvert.DeserializeObject<PoiResultCIExyuvData>(item.Value.ToString());
                    PoiResultDatas.Add(poiResult);
                }
                catch
                {

                }

            }

        }
        //TODO: 需要新增亮度listview
        public void PoiDataDraw(string serialNumber, List<POIResultCIEY> poiResultData)
        {
            if (!resultDis.ContainsKey(serialNumber))
            {
                PoiResult result = new PoiResult();
                result.Id = PoiResults.Count + 1;
                result.SerialNumber = serialNumber;
                result.RecvTime = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                result.ResultType = POIResultType.Y;
                //PoiResults.Add(result);
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
        public void PoiDataDraw(string serialNumber, List<POIResultCIExyuv> poiResultData)
        {
            if (!resultDis.ContainsKey(serialNumber))
            {
                PoiResult result = new PoiResult();
                result.Id = PoiResults.Count + 1;
                result.SerialNumber = serialNumber;
                result.RecvTime = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                result.ResultType = POIResultType.XY_UV;
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
                PoiResultDatas.Clear();
                //if(data.ResultType == POIResultType.XY_UV)
                {
                    foreach (var item in data.PoiData)
                    {
                        PoiResultDatas.Add(item);
                    }
                }
                img_view.ResetPOIPoint();
                img_view.AddPOIPoint(PoiResultDatas);
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
            img_view.OpenImage(bytes);
        }
    }
}
