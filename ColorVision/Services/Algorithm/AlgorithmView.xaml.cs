#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Draw;
using ColorVision.Extension;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Util;
using FileServerPlugin;
using HandyControl.Tools.Extension;
using log4net;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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
    public class MTFResultData : PoiResultData
    {
        public double Articulation { get { return _Articulation; } set { _Articulation = value; NotifyPropertyChanged(); } }

        private double _Articulation;

        public MTFResultData(POIPoint point, double articulation)
        {
            this.Point = point;
            this.Articulation = articulation;
        }

        public MTFResultData(AlgResultMTFModel detail)
        {
            this.Point = new POIPoint((int)detail.PoiId, -1, detail.PoiName, (POIPointTypes)detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
            var temp = JsonConvert.DeserializeObject<MQTTMessageLib.Algorithm.MTFResultData>(detail.Value);
            this.Articulation = temp.Articulation;
        }
    }

    public class FOVResultData : ViewModelBase
    {
        public FovPattern Pattern { get; set; }

        public FovType Type { get; set; }

        public double Degrees { get; set; }

        public FOVResultData(FovPattern pattern, FovType type, double degrees)
        {
            Pattern = pattern;
            Type = type;
            Degrees = degrees;
        }

        public FOVResultData(AlgResultFOVModel algResultFOVModel)
        {
            Pattern = (FovPattern)algResultFOVModel.Pattern;
            Type = (FovType)algResultFOVModel.Type;
            Degrees = (double)algResultFOVModel.Degrees;
        }
    }

    public class SFRResultData : ViewModelBase
    {
        public SFRResultData(float pdfrequency, float pdomainSamplingData)
        {
            this.pdfrequency = pdfrequency;
            this.pdomainSamplingData = pdomainSamplingData;
        }

        public float pdfrequency { get; set; }

        public float pdomainSamplingData { get; set; }
    }

    public class DistortionResultData : ViewModelBase
    {
        public DistortionResultData(DistortionType disType, DisSlopeType slopeType, DisLayoutType layoutType, DisCornerType cornerType, double maxRatio, PointFloat[] finalPoint)
        {
            DisType = disType;
            SlopeType = slopeType;
            LayoutType = layoutType;
            CornerType = cornerType;
            MaxRatio = maxRatio;
            FinalPoints = finalPoint;
        }

        public DistortionResultData(AlgResultDistortionModel algResultDistortionModel)
        {
            DisType = (DistortionType)algResultDistortionModel.Type;
            SlopeType = (DisSlopeType)algResultDistortionModel.SlopeType;
            LayoutType = (DisLayoutType)algResultDistortionModel.LayoutType;
            CornerType = (DisCornerType)algResultDistortionModel.CornerType;
            MaxRatio = (double)algResultDistortionModel.MaxRatio;
            FinalPoints = JsonConvert.DeserializeObject<PointFloat[]>(algResultDistortionModel.FinalPoints);
        }

        public DistortionType DisType { get; set; }
        public string DisTypeDesc
        {
            get
            {
                string result = DisType.ToString();
                switch (DisType)
                {
                    case DistortionType.OpticsDist:
                        result = DisType.ToString();
                        break;
                    case DistortionType.TVDistH:
                        result = DisType.ToString();
                        break;
                    case DistortionType.TVDistV:
                        result = DisType.ToString();
                        break;
                    default:
                        result = DisType.ToString();
                        break;
                }
                return result;
            }
        }
        public DisSlopeType SlopeType { get; set; }

        public string SlopeTypeDesc
        {
            get
            {
                string result = SlopeType.ToString();
                switch (SlopeType)
                {
                    case DisSlopeType.CenterPoint:
                        result = "中心九点";
                        break;
                    case DisSlopeType.lb_Variance:
                        result = "方差去除";
                        break;
                    default:
                        result = SlopeType.ToString();
                        break;
                }
                return result;
            }
        }
        public DisLayoutType LayoutType { get; set; }

        public string LayoutTypeDesc
        {
            get
            {
                string result = LayoutType.ToString();
                switch (LayoutType)
                {
                    case DisLayoutType.SlopeIN:
                        result = "斜率布点";
                        break;
                    case DisLayoutType.SlopeOUT:
                        result = "非斜率布点";
                        break;
                    default:
                        result = LayoutType.ToString();
                        break;
                }
                return result;
            }
        }
        public DisCornerType CornerType { get; set; }

        public string CornerTypeDesc
        {
            get
            {
                string result = CornerType.ToString();
                switch (CornerType)
                {
                    case DisCornerType.Circlepoint:
                        result = "圆点";
                        break;
                    case DisCornerType.Checkerboard:
                        result = "棋盘格";
                        break;
                    default:
                        result = CornerType.ToString();
                        break;
                }
                return result;
            }
        }
        public double MaxRatio { get; set; }
        public PointFloat[] FinalPoints { get; set; }
    }
    public class GhostPointResultData : ViewModelBase
    {
        public GhostPointResultData(PointFloat centerPoint, float ledBlobGray, float ghostAvrGray)
        {
            CenterPoint = centerPoint;
            LedBlobGray = ledBlobGray;
            GhostAvrGray = ghostAvrGray;
        }

        public PointFloat CenterPoint { get; set; }
        public string CenterPointDis
        {
            get
            {
                return string.Format("{0},{1}", CenterPoint.X, CenterPoint.Y);
            }
        }
        public float LedBlobGray { get; set; }
        public float GhostAvrGray { get; set; }
    }
    public class GhostResultData : ViewModelBase
    {
        public GhostResultData(int rows, int cols, string ghostPixelNum, string ghostPixels, string ledPixelNum, string ledPixels, string ledCenters, string ledBlobGray, string ghostAvrGray)
        {
            Rows = rows;
            Cols = cols;
            GhostPixelNum = ghostPixelNum;
            GhostPixels = ghostPixels;
            LedPixelNum = ledPixelNum;
            LedPixels = ledPixels;
            LedCenters = ledCenters;
            LedBlobGray = ledBlobGray;
            GhostAvrGray = ghostAvrGray;
        }
        public GhostResultData(AlgResultGhostModel algResultGhostModel)
        {
            Rows = algResultGhostModel.Rows;
            Cols = algResultGhostModel.Cols;
            GhostPixelNum = algResultGhostModel.SingleGhostPixelNum;
            GhostPixels = algResultGhostModel.GhostPixels;
            LedPixelNum = algResultGhostModel.SingleLedPixelNum;
            LedPixels = algResultGhostModel.LEDPixels;
            LedCenters = algResultGhostModel.LEDCenters;
            LedBlobGray = algResultGhostModel.LEDBlobGray;
            GhostAvrGray = algResultGhostModel.GhostAverageGray;
        }

        public int Rows { get; set; }
        public int Cols { get; set; }
        public string GhostPixelNum { get; set; }
        public string GhostPixels { get; set; }
        public string LedPixelNum { get; set; }
        public string LedPixels { get; set; }
        public string LedCenters { get; set; }
        public string LedBlobGray { get; set; }
        public string GhostAvrGray { get; set; }
    }

    public class LedResultData : ViewModelBase
    {
        public LedResultData(Point point, double radius)
        {
            Point = point;
            Radius = radius;
        }
        public Point Point { get; set; }
        public double Radius { get; set; }
    }

    public class AlgorithmResult : ViewModelBase
    {
        private int _Id;
        private string _SerialNumber;
        private string _ImgFileName;
        private string _POITemplateName;
        private string? _RecvTime;
        private AlgorithmResultType _ResultType;
        private int _resultCode;
        private long _totalTime;
        private string _resultDesc;
        private ObservableCollection<PoiResultData> _PoiData;
        private ObservableCollection<FOVResultData> _FOVData;
        private ObservableCollection<MTFResultData> _MTFData;
        private ObservableCollection<SFRResultData> _SFRData;
        private ObservableCollection<GhostResultData> _GhostData;
        private ObservableCollection<DistortionResultData> _DistortionData;
        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; NotifyPropertyChanged(); } }
        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; NotifyPropertyChanged(); } }
        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        public string? RecvTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }

        public string ResultTypeDis
        {
            get
            {
                string result = "";
                switch (_ResultType)
                {
                    case AlgorithmResultType.POI_XY_UV:
                        result = "色度";
                        break;
                    case AlgorithmResultType.POI_Y:
                        result = "亮度";
                        break;
                    case AlgorithmResultType.POI:
                        result = "关注点";
                        break;
                    case AlgorithmResultType.Distortion:
                        result = "畸变";
                        break;
                    case AlgorithmResultType.Ghost:
                        result = "鬼影";
                        break;
                    default:
                        result = _ResultType.ToString();
                        break;
                }
                return result;
            }
        }
        public AlgorithmResultType ResultType
        {
            get { return _ResultType; }
            set { _ResultType = value; }
        }
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
        public ObservableCollection<PoiResultData> PoiData { get { return _PoiData; } set { _PoiData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<FOVResultData> FOVData { get { return _FOVData; } set { _FOVData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<MTFResultData> MTFData { get { return _MTFData; } set { _MTFData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<SFRResultData> SFRData { get { return _SFRData; } set { _SFRData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<GhostResultData> GhostData { get { return _GhostData; } set { _GhostData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<DistortionResultData> DistortionData { get { return _DistortionData; } set { _DistortionData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<LedResultData> LedResultDatas { get; set; }

        public AlgorithmResult()
        {
            this._PoiData = new ObservableCollection<PoiResultData>();
            this._FOVData = new ObservableCollection<FOVResultData>();
            this._MTFData = new ObservableCollection<MTFResultData>();
            this._SFRData = new ObservableCollection<SFRResultData>();
            this._GhostData = new ObservableCollection<GhostResultData>();
            this._DistortionData = new ObservableCollection<DistortionResultData>();
            LedResultDatas = new ObservableCollection<LedResultData>();
        }

        public AlgorithmResult(AlgResultMasterModel algResultMasterModel)
        {
            _Id = algResultMasterModel.Id;
            _SerialNumber = algResultMasterModel.BatchCode;
            _ImgFileName = algResultMasterModel.ImgFile;
            _POITemplateName = algResultMasterModel.TName;
            _RecvTime = algResultMasterModel.CreateDate.ToString();
            _ResultType = algResultMasterModel.ImgFileType;
            _resultCode = (int)algResultMasterModel.ResultCode;
            _totalTime = algResultMasterModel.TotalTime;
            _resultDesc = algResultMasterModel.Result;
        }

        public AlgorithmResult(int id, string serialNumber, string imgFileName, string pOITemplateName, string recvTime, AlgorithmResultType resultType, int? resultCode, string resultDesc, long totalTime = 0) : this()
        {
            _Id = id;
            _SerialNumber = serialNumber;
            _ImgFileName = imgFileName;
            _POITemplateName = pOITemplateName;
            _RecvTime = recvTime;
            _ResultType = resultType;
            _resultCode = (int)resultCode;
            _totalTime = totalTime;
            _resultDesc = resultDesc;
        }
    }


    public delegate void CurSelectionChanged(AlgorithmResult data);

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }
        public event CurSelectionChanged OnCurSelectionChanged;
        public DeviceAlgorithm Device { get; set; }
        public AlgorithmView(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
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
            List<string> headers = new List<string> { "序号", "批次号", "模板", "图像数据文件", "测量时间", "类型", "用时(时:分:秒)", "结果", "描述" };
            List<string> bdheaders = new List<string> { "Id", "SerialNumber", "POITemplateName", "ImgFileName", "RecvTime", "ResultTypeDis", "TotalTime", "Result", "ResultDesc" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            listView1.View = gridView;
            listView1.ItemsSource = AlgResults;
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






            var keyValuePairs = Enum.GetValues(typeof(AlgorithmResultType))
                .Cast<AlgorithmResultType>()
                .Select(e1 => new KeyValuePair<AlgorithmResultType, string>(e1, e1.ToString()))
                .ToList();
            TextBoxType.ItemsSource = keyValuePairs;




        }

        public ObservableCollection<PoiResultData> PoiResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<PoiResultData> PoiYResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<FOVResultData> FOVResultDatas { get; set; } = new ObservableCollection<FOVResultData>();
        public ObservableCollection<PoiResultData> MTFResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<GhostResultData> GhostResultDatas { get; set; } = new ObservableCollection<GhostResultData>();
        public ObservableCollection<GhostPointResultData> GhostPointResultDatas { get; set; } = new ObservableCollection<GhostPointResultData>();
        public ObservableCollection<SFRResultData> SFRResultDatas { get; set; } = new ObservableCollection<SFRResultData>();
        public ObservableCollection<DistortionResultData> DistortionResultDatas { get; set; } = new ObservableCollection<DistortionResultData>();
        public Dictionary<string, AlgorithmResult> resultDis { get; set; } = new Dictionary<string, AlgorithmResult>();
        public ObservableCollection<AlgorithmResult> AlgResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
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
                foreach (var item in AlgResults)
                {
                    value += item.Id + "," 
                        +item.SerialNumber + "," 
                        + item.POITemplateName  + "," 
                        + item.ImgFileName +","
                        + item.RecvTime + ","
                        + item.ResultTypeDis + ","
                        + item.TotalTime + ","
                        + item.Result + ","
                        + item.ResultDesc + ","
                        + Environment.NewLine;
                }
                file.WriteLine(value);
                ImageSource bitmapSource = ImageView.ImageShow.Source;
                ImageUtil.SaveImageSourceToFile(bitmapSource, Path.Combine( Path.GetDirectoryName(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
            }
        }

        public void AlgResultDataDraw(AlgResultMasterModel result)
        {
            AlgResultDataDraw(result.Id.ToString(), result.BatchCode, result.ImgFile, result.TName, result.ImgFileType, result.ResultCode, result.Result, result.TotalTime);
        }
        public void AlgResultDataDraw(string key, string serialNumber, string imgFileName, string templateName, AlgorithmResultType resultType, int? resultCode, string resultDesc, long totalTime)
        {
            if (!resultDis.ContainsKey(key))
            {
                AlgorithmResult result = new AlgorithmResult(AlgResults.Count + 1, serialNumber, imgFileName, templateName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), resultType, resultCode, resultDesc, totalTime);
                AlgResults.Add(result);
                resultDis[key] = result;
                RefreshResultListView();
            }
        }
        public void PoiDataDraw(AlgResultMasterModel result, List<POIResultCIEY> results)
        {
            PoiDataDraw(result.Id.ToString(), result.BatchCode, result.ImgFile, result.TName, results, result.ResultCode, result.Result, result.TotalTime);
        }

        public void PoiDataDraw(string key, string serialNumber, string imgFileName, string templateName, List<POIResultCIEY> results, int? resultCode, string resultDesc, long totalTime)
        {
            if (!resultDis.ContainsKey(key))
            {
                AlgorithmResult result = new AlgorithmResult(AlgResults.Count + 1, serialNumber, imgFileName, templateName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), AlgorithmResultType.POI_Y, resultCode, resultDesc, totalTime);
                AlgResults.Add(result);
                resultDis[key] = result;
                foreach (var item in results)
                {
                    PoiResultCIEYData resultData = new PoiResultCIEYData(item.Point, item.Data);
                    result.PoiData.Add(resultData);
                }
                RefreshResultListView();
            }
        }
        public void PoiDataDraw(AlgResultMasterModel result, List<POIResultCIExyuv> results)
        {
            PoiDataDraw(result.Id.ToString(), result.BatchCode, result.ImgFile, result.TName, results, result.ResultCode, result.Result, result.TotalTime);
        }
        public void PoiDataDraw(string key, string serialNumber, string imgFileName, string templateName, List<POIResultCIExyuv> results, int? resultCode, string resultDesc, long totalTime)
        {
            if (!resultDis.ContainsKey(key))
            {
                AlgorithmResult result = new AlgorithmResult(AlgResults.Count + 1, serialNumber, imgFileName, templateName, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), AlgorithmResultType.POI_XY_UV, resultCode, resultDesc, totalTime);
                AlgResults.Add(result);
                resultDis[key] = result;
                foreach (var item in results)
                {
                    PoiResultCIExyuvData resultData = new PoiResultCIExyuvData(item.Point, item.Data);
                    result.PoiData.Add(resultData);
                }
                RefreshResultListView();
            }
        }


        AlgResultLedcheckDao algResultLedcheckDao = new AlgResultLedcheckDao();

        public void AlgResultMasterModelDataDraw(AlgResultMasterModel result)
        {
            if (!resultDis.ContainsKey(result.Id.ToString()))
            {
                AlgorithmResult algorithmResult = new AlgorithmResult(result);
                AlgResults.Add(algorithmResult);
                RefreshResultListView();
                resultDis[result.Id.ToString()] = algorithmResult;
            }

        }

        private void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// 专门位鬼影设计的类
        /// </summary>
        class Point1
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        ResultService resultService = new ResultService();

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
                return;
            AlgorithmResult result = listView1.Items[listView1.SelectedIndex] as AlgorithmResult;
            if(result != null)
            {
                PoiResultDatas.Clear();
                PoiYResultDatas.Clear();
                ImageView.ResetPOIPoint();
                listViewSide.Visibility = Visibility.Collapsed;
                List<POIPoint> DrawPoiPoint = new List<POIPoint>();
                switch (result.ResultType)
                {   
                    case AlgorithmResultType.POI:
                        OnCurSelectionChanged?.Invoke(result);
                        break;
                    case AlgorithmResultType.POI_XY_UV:
                        OnCurSelectionChanged?.Invoke(result);
                        listViewY.Hide();
                        listView2.Show();

                        foreach (var item in result.PoiData)
                        {
                            PoiResultDatas.Add(item);
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.POI_Y:
                        OnCurSelectionChanged?.Invoke(result);
                        listView2.Hide();
                        listViewY.Show();
                        foreach (var item in result.PoiData)
                        {
                            PoiYResultDatas.Add(item);
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.FOV:
                        ImageView.OpenImage(result.ImgFileName);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.SFRData == null)
                        {
                            result.FOVData = new ObservableCollection<FOVResultData>();
                            List<AlgResultFOVModel> AlgResultFOVModels = resultService.GetFOVByPid(result.Id);
                            foreach (var item in AlgResultFOVModels)
                            {
                                FOVResultData fOVResultData = new FOVResultData(item);
                                result.FOVData.Add(fOVResultData);
                            };
                        }

                        List<string> bdheadersFOV = new List<string> { "Pattern", "Type", "Degrees" };
                        List<string> headersFOV = new List<string> { "Pattern", "Type", "Degrees" };
                        GridView gridViewFOV = new GridView();
                        for (int i = 0; i < headersFOV.Count; i++)
                        {
                            gridViewFOV.Columns.Add(new GridViewColumn() { Header = headersFOV[i], DisplayMemberBinding = new Binding(bdheadersFOV[i]) });
                        }
                        listViewSide.View = gridViewFOV;
                        listViewSide.Visibility =Visibility.Visible;
                        listViewSide.ItemsSource = result.FOVData;
                        break;
                    case AlgorithmResultType.SFR:
                        ImageView.OpenImage(result.ImgFileName);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.SFRData == null)
                        {
                            result.SFRData = new ObservableCollection<SFRResultData>();
                            List<AlgResultSFRModel> AlgResultSFRModels = resultService.GetSFRByPid(result.Id);
                            foreach (var item in AlgResultSFRModels)
                            {
                                var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                                var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                                for (int i = 0; i < Pdfrequencys.Length; i++)
                                {
                                    SFRResultData resultData = new SFRResultData(Pdfrequencys[i], PdomainSamplingDatas[i]);
                                    result.SFRData.Add(resultData);
                                }
                            };
                        }

                        List<string> bdheadersSFR = new List<string> { "pdfrequency", "pdomainSamplingData" };
                        List<string> headersSFR = new List<string> { "pdfrequency", "pdomainSamplingData" };
                        GridView gridViewSFR = new GridView();
                        for (int i = 0; i < headersSFR.Count; i++)
                        {
                            gridViewSFR.Columns.Add(new GridViewColumn() { Header = headersSFR[i], DisplayMemberBinding = new Binding(bdheadersSFR[i]) });
                        }
                        listViewSide.View = gridViewSFR;
                        listViewSide.ItemsSource = result.SFRData;
                        if (result.SFRData.Count > 0)
                        {
                            ImageView.AddRect(new Rect(10,10,10,10));
                        }

                        break;
                    case AlgorithmResultType.MTF:
                        ImageView.OpenImage(result.ImgFileName);
                        listViewSide.Visibility = Visibility.Visible;
                        if (result.MTFData == null)
                        {
                            result.MTFData = new ObservableCollection<MTFResultData>();
                            List<AlgResultMTFModel> AlgResultMTFModels = resultService.GetMTFByPid(result.Id);
                            foreach (var item in AlgResultMTFModels)
                            {
                                MTFResultData mTFResultData = new MTFResultData(item);
                                result.MTFData.Add(mTFResultData);
                            }
                        }


                        List<string> bdheadersMTF = new List<string> { "PixelPos", "PixelSize", "Shapes", "Articulation" };
                        List<string> headersMTF = new List<string> { "位置", "大小", "形状", "MTF" };
                        GridView gridViewMTF = new GridView();
                        for (int i = 0; i < headersMTF.Count; i++)
                        {
                            gridViewMTF.Columns.Add(new GridViewColumn() { Header = headersMTF[i], DisplayMemberBinding = new Binding(bdheadersMTF[i]) });
                        }
                        listViewSide.View = gridViewMTF;
                        listViewSide.ItemsSource = result.MTFData;
                        foreach (var item in result.MTFData)
                        {
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);

                        break;
                    case AlgorithmResultType.Ghost:
                        if (result.GhostData == null)
                        {
                            result.GhostData = new ObservableCollection<GhostResultData>();
                            List<AlgResultGhostModel> AlgResultGhostModels = resultService.GetGhostByPid(result.Id);
                            foreach (var item in AlgResultGhostModels)
                            {
                                GhostResultData ghostResultData = new GhostResultData(item);
                                result.GhostData.Add(ghostResultData);
                            }
                        }
                        try
                        {
                            string GhostPixels = result.GhostData[0].GhostPixels;
                            List<List<Point1>> GhostPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(GhostPixels);
                            int[] Ghost_pixel_X;
                            int[] Ghost_pixel_Y;
                            List<Point1> Points = new List<Point1>();
                            foreach (var item in GhostPixel)
                                foreach (var item1 in item)
                                    Points.Add(item1);

                            if (Points != null)
                            {
                                Ghost_pixel_X = new int[Points.Count];
                                Ghost_pixel_Y = new int[Points.Count];
                                for (int i = 0; i < Points.Count; i++)
                                {
                                    Ghost_pixel_X[i] = (int)Points[i].X;
                                    Ghost_pixel_Y[i] = (int)Points[i].Y;
                                }
                            }
                            else
                            {
                                Ghost_pixel_X = new int[1] { 1 };
                                Ghost_pixel_Y = new int[1] { 1 };
                            }

                            string LedPixels = result.GhostData[0].LedPixels;
                            List<List<Point1>> LedPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(LedPixels);
                            int[] LED_pixel_X;
                            int[] LED_pixel_Y;

                            Points.Clear();
                            foreach (var item in LedPixel)
                                foreach (var item1 in item)
                                    Points.Add(item1);

                            if (Points != null)
                            {
                                LED_pixel_X = new int[Points.Count];
                                LED_pixel_Y = new int[Points.Count];
                                for (int i = 0; i < Points.Count; i++)
                                {
                                    LED_pixel_X[i] = (int)Points[i].X;
                                    LED_pixel_Y[i] = (int)Points[i].Y;
                                }
                            }
                            else
                            {
                                LED_pixel_X = new int[1] { 1 };
                                LED_pixel_Y = new int[1] { 1 };
                            }
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImageView.OpenGhostImage(result.ImgFileName, LED_pixel_X, LED_pixel_Y, Ghost_pixel_X, Ghost_pixel_Y);
                            });


                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }


                        listViewSide.Visibility = Visibility.Visible;
                        List<string> bdheadersGhost = new List<string> { "LedCenters", "LedBlobGray", "GhostAvrGray" };
                        List<string> headersGhost = new List<string> { "质心坐标", "光斑灰度", "鬼影灰度" };
                        GridView gridViewGhost = new GridView();
                        for (int i = 0; i < headersGhost.Count; i++)
                        {
                            gridViewGhost.Columns.Add(new GridViewColumn() { Header = headersGhost[i], DisplayMemberBinding = new Binding(bdheadersGhost[i]) });
                        }
                        listViewSide.View = gridViewGhost;
                        listViewSide.ItemsSource = result.GhostData;
                        break;
                    case AlgorithmResultType.Distortion:
                        ImageView.OpenImage(result.ImgFileName);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.DistortionData == null)
                        {
                            result.DistortionData = new ObservableCollection<DistortionResultData>();
                            var Distortions = resultService.GetDistortionByPid(result.Id);
                            foreach (var item in Distortions)
                            {
                                DistortionResultData distortionResultData = new DistortionResultData(item);
                                result.DistortionData.Add(distortionResultData);
                            }
                        }
                        List<string> bdheadersDis = new List<string> { "DisTypeDesc", "SlopeTypeDesc", "LayoutTypeDesc", "CornerTypeDesc", "MaxRatio" };
                        List<string> headersDis = new List<string> { "类型", "斜率", "布点", "角点", "畸变率" };
                        GridView gridViewDis = new GridView();
                        for (int i = 0; i < headersDis.Count; i++)
                        {
                            gridViewDis.Columns.Add(new GridViewColumn() { Header = headersDis[i], DisplayMemberBinding = new Binding(bdheadersDis[i]) });
                        }
                        listViewSide.View = gridViewDis;
                        listViewSide.ItemsSource = result.DistortionData;
                        if (result.DistortionData.Count > 0)
                        {
                            List<Point> points = new List<Point>();
                            foreach (var item in result.DistortionData[0].FinalPoints)
                            {
                                points.Add(new Point(item.X, item.Y));
                            }
                            ImageView.AddPoint(points);
                        }
                       
                        break;
                    case AlgorithmResultType.Calibration:
                        break;
                    case AlgorithmResultType.LedCheck:
                        ImageView.OpenImage(result.ImgFileName);
                        listViewSide.Visibility = Visibility.Visible;
                        if (result.LedResultDatas == null)
                        {
                            result.LedResultDatas = new ObservableCollection<LedResultData>();
                            List<AlgResultLedcheckModel> AlgResultLedcheckModels = algResultLedcheckDao.GetAllByPid(result.Id);
                            foreach (var item in AlgResultLedcheckModels)
                            {
                                LedResultData ledResultData = new LedResultData(new Point((double)item.PosX, (double)item.PosY), (double)item.Radius);
                                result.LedResultDatas.Add(ledResultData);
                            };
                        }

                        bdheadersDis = new List<string> { "Point", "Radius" };
                        headersDis = new List<string> { "坐标", "半径"};
                        gridViewDis = new GridView();
                        for (int i = 0; i < headersDis.Count; i++)
                        {
                            gridViewDis.Columns.Add(new GridViewColumn() { Header = headersDis[i], DisplayMemberBinding = new Binding(bdheadersDis[i]) });
                        }
                        listViewSide.View = gridViewDis;
                        listViewSide.ItemsSource = result.LedResultDatas;
                        if (result.LedResultDatas.Count > 0)
                        {
                            List<Point> points = new List<Point>();
                            foreach (var item in result.LedResultDatas)
                            {
                                DrawingVisualCircle Circle = new DrawingVisualCircle();
                                Circle.Attribute.Center = item.Point;
                                Circle.Attribute.Radius = item.Radius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 2);
                                Circle.Render();
                                ImageView.ImageShow.AddVisual(Circle);
                            }
                        }
                        break;

                    default:
                        break;
                }

            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                AlgResults.RemoveAt(temp);
            }
        }

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listView2.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }
        private void listViewY_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        internal void OpenImage(CVCIEFileInfo fileInfo)
        {
            ImageView.OpenImage(fileInfo);
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            AlgResults.Clear();
        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = ListRow2.ActualHeight - 38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }
        AlgResultMasterDao algResultMasterDao = new AlgResultMasterDao();
        private void Search_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxType.SelectedIndex = -1;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text)&& string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxType.Text) && string.IsNullOrEmpty(TextBoxFile.Text))
            {
                AlgResults.Clear();
                List<AlgResultMasterModel> algResults = algResultMasterDao.GetAll();
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new AlgorithmResult(item);
                    AlgResults.Add(algorithmResult);
                }
                return;
            }
            else
            {
                string altype = string.Empty;
                if (TextBoxType.SelectedValue is AlgorithmResultType algorithmResultType)
                    altype = ((int)algorithmResultType).ToString();

                AlgResults.Clear();
                List<AlgResultMasterModel> algResults = algResultMasterDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, altype.ToString(), TextBoxFile.Text);
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new AlgorithmResult(item);
                    AlgResults.Add(algorithmResult);
                }

            }
        }
    }
}
