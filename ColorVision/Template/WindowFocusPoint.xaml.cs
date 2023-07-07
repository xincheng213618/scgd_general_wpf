using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using ColorVision.Template;
using ColorVision.Util;
using cvColorVision;
using Gu.Wpf.Geometry;
using HandyControl.Tools.Extension;
using log4net;
using Microsoft.VisualBasic.Logging;
using MySqlX.XDevAPI.Relational;
using Newtonsoft.Json;
using OpenCvSharp.Flann;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.Zoombox;

namespace ColorVision.Template
{


    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        private static int No = 1;
        public PoiParam():base(No++)
        {
        }
        public PoiParam(int id) : base(id)
        {
        }
        public PoiParam(PoiMasterModel dbModel) : base(dbModel.Id ?? -1)
        {
            //this.ID = dbModel.Id ?? -1;
            this.PoiName = dbModel.Name ?? string.Empty;
            this.Width = dbModel.Width ?? 0;
            this.Height = dbModel.Height ?? 0;
            this.Type = dbModel.Type ?? 0;
            this.DatumArea.X1X = dbModel.LeftTopX ?? 0;
            this.DatumArea.X1Y = dbModel.LeftTopY ?? 0;
            this.DatumArea.X2X = dbModel.RightTopX ?? 0;
            this.DatumArea.X2Y = dbModel.RightTopY ?? 0;
            this.DatumArea.X3X = dbModel.RightBottomX ?? 0;
            this.DatumArea.X3Y = dbModel.RightBottomY ?? 0;
            this.DatumArea.X4X = dbModel.LeftBottomX ?? 0;
            this.DatumArea.X4Y = dbModel.LeftBottomY ?? 0;
            this.DatumArea.CenterX = (this.DatumArea.X2X - this.DatumArea.X1X)/2;
            this.DatumArea.CenterY = (this.DatumArea.X4Y - this.DatumArea.X1Y) /2;
            this.CfgJson = dbModel.CfgJson ?? string.Empty;
        }

        public string CfgJson {
            get => JsonConvert.SerializeObject(DatumArea);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    DatumArea ??= new DatumArea();
                }
                else
                {
                    DatumArea = JsonConvert.DeserializeObject<DatumArea>(value) ?? new DatumArea();
                }
            }
        }


        public string PoiName { get { return _PoiName; } set { _PoiName = value; NotifyPropertyChanged(); } }
        private string _PoiName;

        public int Type { get => _Type; set { _Type = value; NotifyPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height;


        /// <summary>
        /// 关注点列表
        /// </summary>
        public List<PoiParamData> PoiPoints { get; set; } = new List<PoiParamData>();

        public DatumArea DatumArea { get; set; } = new DatumArea();




        [JsonIgnore]
        public bool IsPointCircle { get => DeafultPointType == RiPointTypes.Circle; set { if (value) DeafultPointType = RiPointTypes.Circle; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DeafultPointType == RiPointTypes.Rect; set { if (value) DeafultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointMask { get => DeafultPointType == RiPointTypes.Mask; set { if (value) DeafultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        public RiPointTypes DeafultPointType { set; get; }

    }


    public enum RiPointTypes
    {
        Circle = 0,
        Rect = 1,
        Mask = 2
    }

    public class PoiParamData
    {
        public PoiParamData(PoiDetailModel dbModel)
        {
            ID = dbModel.Id;
            Name = dbModel.Name ?? dbModel.Id.ToString();
            PointType = dbModel.Type switch
            {
                0 => RiPointTypes.Circle,
                1 => RiPointTypes.Rect,
                2 => RiPointTypes.Mask,
                _ => RiPointTypes.Circle,
            };
            PixX = dbModel.PixX ?? 0;
            PixY = dbModel.PixY ?? 0;
            PixWidth = dbModel.PixWidth ?? 0;
            PixHeight = dbModel.PixHeight ?? 0;
        }

        public PoiParamData()
        {
        }

        public int ID { set; get; }

        public string Name { set; get; }
        public RiPointTypes PointType { set; get; }
        public double PixX { set; get; }
        public double PixY { set; get; }
        public double PixWidth { set; get; }
        public double PixHeight { set; get; }
    }

    public class DatumArea:ViewModelBase
    {

        public bool IsShowDatum { get => _IsShowDatum; set { _IsShowDatum = value; NotifyPropertyChanged(); } }
        private bool _IsShowDatum = true;

        public bool IsShowDatumArea { get => _IsShowDatumArea; set { _IsShowDatumArea = value; NotifyPropertyChanged(); } }
        private bool _IsShowDatumArea = true;



        public Point X1 { get; set; } = new Point() { X = 100, Y = 100 };
        public Point X2 { get; set; } = new Point() { X = 300, Y = 100 };
        public Point X3 { get; set; } = new Point() { X = 300, Y = 300 };
        public Point X4 { get; set; } = new Point() { X = 100, Y = 300 };
        public Point Center { get; set; } = new Point() { X = 200, Y = 200 };
        [JsonIgnore()]
        public int X1X { get => (int)X1.X; set { X1 = new Point(value, X1.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X1Y { get => (int)X1.Y; set { X1 = new Point(X1.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X2X { get => (int)X2.X; set { X2 = new Point(value, X2.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X2Y { get => (int)X2.Y; set { X2 = new Point(X2.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X3X { get => (int)X3.X; set { X3 = new Point(value, X3.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X3Y { get => (int)X3.Y; set { X3 = new Point(X3.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X4X { get => (int)X4.X; set { X4 = new Point(value, X4.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X4Y { get => (int)X4.Y; set { X4 = new Point(X4.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int CenterX { get => (int)Center.X; set { Center = new Point(value, Center.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int CenterY { get => (int)Center.Y; set { Center = new Point(Center.X, value); NotifyPropertyChanged(); } }

        public RiPointTypes PointType { set; get; }
        [JsonIgnore]
        public bool IsAreaCircle { get => PointType == RiPointTypes.Circle;set { if (value) PointType = RiPointTypes.Circle; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaRect { get => PointType == RiPointTypes.Rect; set { if (value) PointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaMask { get => PointType == RiPointTypes.Mask; set { if (value) PointType = RiPointTypes.Mask; NotifyPropertyChanged(); } }


        public int AreaCircleRadius { get => _AreaCircleRadius; set { _AreaCircleRadius = value; NotifyPropertyChanged(); } }
        private int _AreaCircleRadius= 100;

        public int AreaCircleNum { get => _AreaCircleNum; set { _AreaCircleNum = value; NotifyPropertyChanged(); } }
        private int _AreaCircleNum = 6;

        public int AreaCircleAngle { get=>_AreaCircleAngle; set { _AreaCircleAngle = value; NotifyPropertyChanged(); } }
        private int _AreaCircleAngle;

        public int AreaRectWidth { get=>_AreaRectWidth; set { _AreaRectWidth = value; NotifyPropertyChanged(); } }
        private int _AreaRectWidth = 200;

        public int AreaRectHeight { get=>_AreaRectHeight; set { _AreaRectHeight = value; NotifyPropertyChanged(); } }
        private int _AreaRectHeight = 200;

        public int AreaRectRow { get=>_AreaRectRow; set { _AreaRectRow = value; NotifyPropertyChanged(); } }
        private int _AreaRectRow = 3;

        public int AreaRectCol { get=>_AreaRectCol; set { _AreaRectCol = value; NotifyPropertyChanged(); } }
        private int _AreaRectCol = 3;


        public int AreaPolygonRow { get => _AreaPolygonRow; set { _AreaPolygonRow = value; NotifyPropertyChanged(); } }
        private int _AreaPolygonRow = 3;

        public int AreaPolygonCol { get => _AreaPolygonCol; set { _AreaPolygonCol = value; NotifyPropertyChanged(); } }
        private int _AreaPolygonCol = 3;


        public Point Polygon1 { get; set; } = new Point() { X = 100, Y = 100 };
        public Point Polygon2 { get; set; } = new Point() { X = 300, Y = 100 };
        public Point Polygon3 { get; set; } = new Point() { X = 300, Y = 300 };
        public Point Polygon4 { get; set; } = new Point() { X = 100, Y = 300 };
        [JsonIgnore()]
        public int Polygon1X { get => (int)Polygon1.X; set { Polygon1 = new Point(value, Polygon1.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon1Y { get => (int)Polygon1.Y; set { Polygon1 = new Point(Polygon1.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon2X { get => (int)Polygon2.X; set { Polygon2 = new Point(value, Polygon2.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon2Y { get => (int)Polygon2.Y; set { Polygon2 = new Point(Polygon2.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon3X { get => (int)Polygon3.X; set { Polygon3 = new Point(value, Polygon3.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon3Y { get => (int)Polygon3.Y; set { Polygon3 = new Point(Polygon3.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon4X { get => (int)Polygon4.X; set { Polygon4 = new Point(value, Polygon4.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon4Y { get => (int)Polygon4.Y; set { Polygon4 = new Point(Polygon4.X, value); NotifyPropertyChanged(); } }


        public int DefaultCircleRadius { get => _DefaultCircleRadius; set { _DefaultCircleRadius = value; NotifyPropertyChanged(); } }
        private int _DefaultCircleRadius = 10;

        public int DefaultRectWidth { get => _DefaultRectWidth; set { _DefaultRectWidth = value; NotifyPropertyChanged(); } }
        private int _DefaultRectWidth = 20;

        public int DefaultRectHeight { get => _DeafultRectHeight; set { _DeafultRectHeight = value; NotifyPropertyChanged(); } }
        private int _DeafultRectHeight = 20;

    }

    /// <summary>
    /// WindowFocusPoint.xaml 的交互逻辑
    /// </summary>
    public partial class WindowFocusPoint : Window, INotifyPropertyChanged
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowFocusPoint));
        public enum BorderType
        {
            [Description("绝对值")]
            Absolute,
            [Description("相对值")]
            Relative
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        DatumArea DatumAreaPoints { get; set; } = new DatumArea();
        SoftwareConfig SoftwareConfig { get; set; }
        public WindowFocusPoint()
        {
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            PoiParam = new PoiParam();
            DatumAreaPoints = PoiParam.DatumArea;
            StackPanelDatumAreaPoints.DataContext = PoiParam.DatumArea;
            this.DataContext = PoiParam;
        }

        PoiParam PoiParam { get; set; }
        public WindowFocusPoint(PoiParam poiParam)
        {
            PoiParam = poiParam;
            DatumAreaPoints = PoiParam.DatumArea; 
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            StackPanelDatumAreaPoints.DataContext = DatumAreaPoints;
            this.DataContext = PoiParam;

        }


        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        public bool IsLayoutUpdated { get => _IsLayoutUpdated; set { _IsLayoutUpdated = value; NotifyPropertyChanged(); if (value) UpdateVisualLayout(value);  } }
        private bool _IsLayoutUpdated = true;
        private void Button_Click_UpdateVisualLayout(object sender, RoutedEventArgs e)
        {
            UpdateVisualLayout(true);
        }

        private void UpdateVisualLayout(bool IsLayoutUpdated)
        {
            foreach (var item in DefaultPoint)
            {
                if (item is DrawingVisualDatumCircle visualDatumCircle)
                {
                    visualDatumCircle.Attribute.Radius = 5 / Zoombox1.ContentMatrix.M11;
                }
            }

            if (drawingVisualDatum != null && drawingVisualDatum is IDrawingVisualDatum dw)
            {
                dw.GetAttribute().Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
            }

            if (IsLayoutUpdated)
            {
                foreach (var item in DrawingVisualLists)
                {
                    DrawAttributeBase drawAttributeBase = item.GetAttribute();
                    if (drawAttributeBase is CircleAttribute circleAttribute)
                    {
                        circleAttribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    }
                    else if (drawAttributeBase is RectangleAttribute rectangleAttribute)
                    {
                        rectangleAttribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    }
                }
            }
        }



        private async void Window_Initialized(object sender, EventArgs e)
        {
            SettingGroup.DataContext = this;

            ComboBoxBorderType.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()
                                             select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType.SelectedIndex = 0;
            ComboBoxBorderType.SelectionChanged += (s, e) =>
            {
                if (ComboBoxBorderType.SelectedItem is KeyValuePair<string, BorderType> KeyValue && KeyValue.Value is BorderType communicateType)
                {

                }
            };
            WindowState = WindowState.Maximized;


            ImageShow.VisualsAdd += (s, e) =>
            {
                if (s is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && s is Visual visual1)
                {
                    DrawingVisualLists.Add(visual);
                    visual.GetAttribute().PropertyChanged += (s1, e1) =>
                    {
                        if (e1.PropertyName == "IsShow")
                        {
                            ListView1.ScrollIntoView(visual);
                            ListView1.SelectedIndex = DrawingVisualLists.IndexOf(visual);
                            if (visual.GetAttribute().IsShow == true)
                            {
                                if (!ImageShow.ContainsVisual(visual1))
                                {
                                    ImageShow.AddVisual(visual1);
                                }
                            }
                            else
                            {
                                if (ImageShow.ContainsVisual(visual1))
                                {
                                    ImageShow.RemoveVisual(visual1);
                                }
                            }
                        }
                    };
                }
            };

            //如果是不显示
            ImageShow.VisualsRemove += (s, e) =>
            {
                if (s is IDrawingVisual visual)
                {
                    if (visual.GetAttribute().IsShow)
                        DrawingVisualLists.Remove(visual);
                }
            };

            double oldmax = Zoombox1.ContentMatrix.M11;
            Zoombox1.LayoutUpdated += (s, e) =>
            {
                if (oldmax != Zoombox1.ContentMatrix.M11)
                {
                    oldmax = Zoombox1.ContentMatrix.M11;
                    UpdateVisualLayout(IsLayoutUpdated);
                }
            };
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;

            if (PoiParam.Height != 0 && PoiParam.Width != 0)
            {
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Visible;
                WaitControlProgressBar.Value = 0;
                await Task.Delay(100);
                if(SoftwareConfig.IsUseMySql)
                    TemplateControl.GetInstance().LoadPoiDetailFromDB(PoiParam);
                WaitControlProgressBar.Value = 10;

                if (PoiParam.PoiPoints.Count > 500)
                    IsLayoutUpdated = false;

                CreateImage(PoiParam.Width, PoiParam.Height, System.Windows.Media.Colors.White,false);
                WaitControlProgressBar.Value = 20;
                PoiParamToDrawingVisual(PoiParam);
                DatumSet();
                ShowDatumArea();
                log.Debug("Render Poi end");
            }
            else
            {
                PoiParam.Width = 400;
                PoiParam.Height = 300;
            }
            this.Closed += (s, e) =>
            {
                PoiParam.PoiPoints.Clear();
                foreach (var item in DrawingVisualLists)
                {
                    DrawAttributeBase drawAttributeBase = item.GetAttribute();
                    if (drawAttributeBase is CircleAttribute circle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID =circle.ID,
                            Name = circle.Name,
                            PointType = RiPointTypes.Circle,
                            PixX = circle.Center.X,
                            PixY = circle.Center.Y,
                            PixWidth = circle.Radius,
                            PixHeight = circle.Radius,
                        };
                        PoiParam.PoiPoints.Add(poiParamData);

                    }
                    else if (drawAttributeBase is RectangleAttribute rectangle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID =rectangle.ID,
                            Name = rectangle.Name,
                            PointType = RiPointTypes.Rect,
                            PixX = rectangle.Rect.X,
                            PixY = rectangle.Rect.Y,
                            PixWidth = rectangle.Rect.Width,
                            PixHeight = rectangle.Rect.Height,
                        };
                        PoiParam.PoiPoints.Add(poiParamData);
                    }
                }

                if (SoftwareConfig.IsUseMySql)
                {
                    new PoiMasterDao().Save(new PoiMasterModel(PoiParam) { Name = PoiParam.PoiName });
                }
            };


        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                OpenImage(filePath);
            }
        }

        private void OpenCAD_Click(object sender, RoutedEventArgs e)
        {
            CreateImage(PoiParam.Width, PoiParam.Height, Colors.White,false);
        }

        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));

                if (ImageShow.Source == null)
                {
                    ImageShow.Source = new BitmapImage(new Uri(filePath));
                    Zoombox1.ZoomUniform();
                    InitDatumAreaValue((int)bitmapImage.Width, (int)bitmapImage.Height);
                }
                else
                {
                    if (ImageShow.Source is BitmapImage img && (img.PixelWidth != bitmapImage.PixelWidth || img.PixelHeight != bitmapImage.PixelHeight))
                    {
                        InitDatumAreaValue((int)bitmapImage.Width, (int)bitmapImage.Height);
                        ImageShow.Source = bitmapImage;
                        Zoombox1.ZoomUniform();
                    }
                    else
                    {
                        ImageShow.Source = bitmapImage;
                    }

                }
                PoiParam.Width = bitmapImage.PixelWidth;
                PoiParam.Height = bitmapImage.PixelHeight;
            }
        }
        private bool Init;

        private void CreateImage(int width, int height, System.Windows.Media.Color color,bool IsClear = true)
        {
            Thread thread = new Thread(() => 
            {
                BitmapImage bitmapImage = ImageUtil.CreateSolidColorBitmap(width, height, color);
                bitmapImage.Freeze();
                Application.Current.Dispatcher.Invoke(() =>
                {

                    if (ImageShow.Source == null)
                    {
                        ImageShow.Source = bitmapImage;
                        Zoombox1.ZoomUniform();
                        if (IsClear|| !Init)
                            InitDatumAreaValue((int)bitmapImage.Width,(int)bitmapImage.Height);
                    }
                    else
                    {
                        if (ImageShow.Source is BitmapImage img && (img.PixelWidth != bitmapImage.PixelWidth || img.PixelHeight != bitmapImage.PixelHeight))
                        {
                            InitDatumAreaValue((int)bitmapImage.Width, (int)bitmapImage.Height);
                            ImageShow.Source = bitmapImage;
                            Zoombox1.ZoomUniform();
                        }
                        else
                        {
                            ImageShow.Source = bitmapImage;
                        }

                    }
                    if (IsClear)
                    {
                        ImageShow.Clear();
                        DrawingVisualLists.Clear();
                        PropertyGrid2.SelectedObject = null;
                    }
                    if (Init)
                    {
                        WaitControl.Visibility = Visibility.Hidden;
                        WaitControlProgressBar.Visibility = Visibility.Hidden;
                    }
                    Init = true;

                });
            });
            thread.Start();
        }


        private void InitDatumAreaValue(int width,int height)
        {
            PoiParam.DatumArea.X1X = 0;
            PoiParam.DatumArea.X1Y = 0;
            PoiParam.DatumArea.X2X = width;
            PoiParam.DatumArea.X2Y = 0;
            PoiParam.DatumArea.X3X = width;
            PoiParam.DatumArea.X3Y = height;
            PoiParam.DatumArea.X4X = 0;
            PoiParam.DatumArea.X4Y = height;
            PoiParam.DatumArea.CenterX = width / 2;
            PoiParam.DatumArea.CenterY = height / 2;

            PoiParam.DatumArea.AreaCircleRadius = width> height? height / 2: width/2;
            PoiParam.DatumArea.AreaRectWidth = width;
            PoiParam.DatumArea.AreaRectHeight = height;

            PoiParam.DatumArea.Polygon1X = 0;
            PoiParam.DatumArea.Polygon1Y = 0;
            PoiParam.DatumArea.Polygon2X = width;
            PoiParam.DatumArea.Polygon2Y = 0;
            PoiParam.DatumArea.Polygon3X = width;
            PoiParam.DatumArea.Polygon3Y = height;
            PoiParam.DatumArea.Polygon4X = 0;
            PoiParam.DatumArea.Polygon4Y = height;
            ShowDatumArea();
            DatumSet();

        }

        private async void PoiParamToDrawingVisual(PoiParam poiParam)
        {
            int i = 0;

            foreach (var item in poiParam.PoiPoints)
            {
                i++;
                if (i % 50 == 0)
                {
                    WaitControlProgressBar.Value = 20 + i * 79 / poiParam.PoiPoints.Count;
                    await Task.Delay(1);
                }
                switch (item.PointType)
                {
                    case RiPointTypes.Circle:
                        DrawingVisualCircleWord drawingVisualCircle = new DrawingVisualCircleWord();
                        drawingVisualCircle.Attribute.Center = new Point(item.PixX, item.PixY);
                        drawingVisualCircle.Attribute.Radius = item.PixWidth;
                        drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                        drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red,  item.PixWidth/30);
                        drawingVisualCircle.Attribute.ID = item.ID;
                        drawingVisualCircle.Attribute.Name = item.Name;
                        drawingVisualCircle.Render();
                        ImageShow.AddVisual(drawingVisualCircle);
                        break;
                    case RiPointTypes.Rect:
                        DrawingVisualRectangle drawingVisualRectangle = new DrawingVisualRectangle();
                        drawingVisualRectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                        drawingVisualRectangle.Attribute.Brush = Brushes.Transparent;
                        drawingVisualRectangle.Attribute.Pen = new Pen(Brushes.Red,  item.PixWidth/30);
                        drawingVisualRectangle.Attribute.ID = item.ID;
                        drawingVisualRectangle.Attribute.Name = item.Name;
                        drawingVisualRectangle.Render();
                        ImageShow.AddVisual(drawingVisualRectangle);
                        break;
                    case RiPointTypes.Mask:
                        break;
                }
            }
            WaitControlProgressBar.Value = 99;

            if (Init)
            {
                WaitControl.Visibility = Visibility.Hidden;
                WaitControlProgressBar.Visibility = Visibility.Hidden;
            }
            Init = true;
        }





        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();

        private void SetDeafult_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButtonBuildMode2.IsChecked == true)
            {
                if (ImageShow.Source is BitmapImage bitmapImage)
                {
                    if (!double.TryParse(TextBoxUp.Text, out double startU))
                        startU = 0;

                    if (!double.TryParse(TextBoxDown.Text, out double startD))
                        startD = 0;

                    if (!double.TryParse(TextBoxLeft.Text, out double startL))
                        startL = 0;
                    if (!double.TryParse(TextBoxRight.Text, out double startR))
                        startR = 0;

                    if (ComboBoxBorderType.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                    {
                        startU = bitmapImage.PixelHeight * startU / 100;
                        startD = bitmapImage.PixelHeight * startD / 100;

                        startL = bitmapImage.PixelWidth * startL / 100;
                        startR = bitmapImage.PixelWidth * startR / 100;
                    }

                    PoiParam.DatumArea.X1X = (int)startL;
                    PoiParam.DatumArea.X1Y = (int)startU;
                    PoiParam.DatumArea.X2X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.DatumArea.X2Y = (int)startU;
                    PoiParam.DatumArea.X3X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.DatumArea.X3Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.DatumArea.X4X = (int)startR;
                    PoiParam.DatumArea.X4Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.DatumArea.CenterX = (int)bitmapImage.PixelWidth / 2;
                    PoiParam.DatumArea.CenterY = bitmapImage.PixelHeight / 2;

                }
            }
            DatumSet();
        }

        private void DatumSet()
        {

            foreach (var item in DefaultPoint)
            {
                ImageShow.RemoveVisual(item);
            }
            DefaultPoint.Clear();
            if (PoiParam.DatumArea.IsShowDatum)
            {
                List<Point> Points = new List<Point>()
                {
                    new Point(DatumAreaPoints.X1.X, DatumAreaPoints.X1.Y),
                    new Point(DatumAreaPoints.X2.X, DatumAreaPoints.X2.Y),
                    new Point(DatumAreaPoints.X3.X, DatumAreaPoints.X3.Y),
                    new Point(DatumAreaPoints.X4.X, DatumAreaPoints.X4.Y),
                    new Point(DatumAreaPoints.Center.X, DatumAreaPoints.Center.Y),
                };

                for (int i = 0; i < Points.Count; i++)
                {
                    DrawingVisualDatumCircle drawingVisual = new DrawingVisualDatumCircle();
                    drawingVisual.Attribute.Center = Points[i];
                    drawingVisual.Attribute.Radius = 5 / Zoombox1.ContentMatrix.M11;
                    drawingVisual.Attribute.Brush = Brushes.Blue;
                    drawingVisual.Attribute.Pen = new Pen(Brushes.Blue, 2);
                    drawingVisual.Attribute.ID = i + 1;
                    drawingVisual.Render();
                    DefaultPoint.Add(drawingVisual);
                    ImageShow.AddVisual(drawingVisual);
                }
            }


        }


        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }
        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var Point = Mouse.GetPosition(ImageShow);
            var DrawingVisual = ImageShow.GetVisual(Point);

            if (DrawingVisual != null && DrawingVisual is IDrawingVisual drawing)
            {
                var ContextMenu = new ContextMenu();

                MenuItem menuItem = new MenuItem() { Header = "隐藏(_H)" };
                menuItem.Click += (s, e) =>
                {
                    drawing.GetAttribute().IsShow = false;
                };
                MenuItem menuIte2 = new MenuItem() { Header = "删除(_D)" };

                menuIte2.Click += (s, e) =>
                {
                    ImageShow.RemoveVisual(DrawingVisual);
                    PropertyGrid2.SelectedObject = null;
                };
                ContextMenu.Items.Add(menuItem);
                ContextMenu.Items.Add(menuIte2);
                ImageShow.ContextMenu = ContextMenu;
            }
            else
            {
                ImageShow.ContextMenu = null;
            }

        }
        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas)
            {
                var MouseDownP = e.GetPosition(drawCanvas);

                if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                {
                    PropertyGrid2.SelectedObject = drawingVisual.GetAttribute();

                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);
                }
            }
        }

        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is BitmapImage bitmapImage)
            {
                int start = DrawingVisualLists.Count;
                switch (PoiParam.DatumArea.PointType)
                {
                    case RiPointTypes.Circle:
                        if (PoiParam.DatumArea.AreaCircleNum < 1)
                        {
                            MessageBox.Show("绘制的个数不能小于1");
                            return;
                        }

                        for (int i = 0; i < PoiParam.DatumArea.AreaCircleNum; i++)
                        {

                            double x1 = PoiParam.DatumArea.CenterX + PoiParam.DatumArea.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                            double y1 = PoiParam.DatumArea.CenterY + PoiParam.DatumArea.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);


                            switch (PoiParam.DeafultPointType)
                            {
                                case RiPointTypes.Circle:
                                    DrawingVisualCircle Circle = new DrawingVisualCircleWord();
                                    Circle.Attribute.Center = new Point(x1, y1);
                                    Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                    Circle.Attribute.ID = start + i + 1;
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                    Rectangle.Attribute.Rect = new Rect(x1 - PoiParam.DatumArea.DefaultRectWidth / 2, y1 - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                    Rectangle.Attribute.ID = start + i + 1;
                                    Rectangle.Render();
                                    ImageShow.AddVisual(Rectangle);
                                    break;
                                case RiPointTypes.Mask:
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case RiPointTypes.Rect:

                        int cols = PoiParam.DatumArea.AreaRectCol;
                        int rows = PoiParam.DatumArea.AreaRectRow;

                        if (rows < 1 || cols < 1)
                        {
                            MessageBox.Show("点阵数的行列不能小于1");
                            return;
                        }
                        double Width = PoiParam.DatumArea.AreaRectWidth;
                        double Height = PoiParam.DatumArea.AreaRectHeight;


                        double startU = PoiParam.DatumArea.CenterY - Height / 2;
                        double startD = bitmapImage.PixelHeight - PoiParam.DatumArea.CenterY - Height / 2;
                        double startL = PoiParam.DatumArea.CenterX - Width / 2;
                        double startR = bitmapImage.PixelWidth - PoiParam.DatumArea.CenterX - Width / 2;


                        double StepRow = (bitmapImage.PixelHeight - startD - startU) / (rows - 1);
                        double StepCol = (bitmapImage.PixelWidth - startL - startR) / (cols - 1);

                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                switch (PoiParam.DeafultPointType)
                                {
                                    case RiPointTypes.Circle:
                                        DrawingVisualCircle Circle = new DrawingVisualCircleWord();
                                        Circle.Attribute.Center = new Point(startL + StepCol * j, startU + StepRow * i);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                        Circle.Attribute.ID = start + i * cols + j + 1;
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                        Rectangle.Attribute.Rect = new Rect(startL + StepCol * j - PoiParam.DatumArea.DefaultRectWidth / 2, startU + StepRow * i - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectWidth);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                        Rectangle.Attribute.ID = start + i * cols + j + 1;
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                        break;
                                    case RiPointTypes.Mask:
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }

                        break;
                    case RiPointTypes.Mask:


                        List<Point> pts_src = new List<Point>();
                        pts_src.Add(PoiParam.DatumArea.Polygon1);
                        pts_src.Add(PoiParam.DatumArea.Polygon2);
                        pts_src.Add(PoiParam.DatumArea.Polygon3);
                        pts_src.Add(PoiParam.DatumArea.Polygon4);


                        List<Point> points = SortPolyPoints(pts_src);

                        cols = PoiParam.DatumArea.AreaPolygonCol;
                        rows = PoiParam.DatumArea.AreaPolygonRow;


                        double rowStep = 1.0 / (rows-1);
                        double columnStep = 1.0 / (cols - 1);
                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < rows; j++)
                            {
                                // Calculate the position of the point within the quadrilateral
                                double x = (1 - i * rowStep) * (1 - j * columnStep) * points[0].X +
                                           (1 - i * rowStep) * (j * columnStep) * points[1].X +
                                           (i * rowStep) * (1 - j * columnStep) * points[3].X +
                                           (i * rowStep) * (j * columnStep) * points[2].X;

                                double y = (1 - i * rowStep) * (1 - j * columnStep) * points[0].Y +
                                           (1 - i * rowStep) * (j * columnStep) * points[1].Y +
                                           (i * rowStep) * (1 - j * columnStep) * points[3].Y +
                                           (i * rowStep) * (j * columnStep) * points[2].Y;

                                Point point = new Point(x, y);

                                switch (PoiParam.DeafultPointType)
                                {
                                    case RiPointTypes.Circle:
                                        DrawingVisualCircle Circle = new DrawingVisualCircleWord();
                                        Circle.Attribute.Center = new Point(point.X, point.Y);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                        Circle.Attribute.ID = start + i * cols + j + 1;
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                        Rectangle.Attribute.Rect = new Rect(point.X - PoiParam.DatumArea.DefaultRectWidth / 2, point.Y - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                        Rectangle.Attribute.ID = start + i * cols + j + 1;
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                        break;
                                    case RiPointTypes.Mask:
                                        break;
                                    default:
                                        break;
                                }


                            }
                        }

                        break;
                    default:
                        break;
                }

            }
        }


        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            
            foreach (var item in DrawingVisualLists.ToList())
            {
                if (item is Visual visual)
                    ImageShow.RemoveVisual(visual);
            }
            PropertyGrid2.SelectedObject = null;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[listView.SelectedIndex] is IDrawingVisual drawingVisual && drawingVisual is Visual visual)
            {

                PropertyGrid2.SelectedObject = drawingVisual.GetAttribute();
                ImageShow.TopVisual(visual);
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
        }
        private void MenuItem_DrawingVisual_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Visual visual)
            {
                PropertyGrid2.SelectedObject = null;
                ImageShow.RemoveVisual(visual);
            }
        }

        private void ListView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[ListView1.SelectedIndex] is Visual visual)
                {
                    ImageShow.RemoveVisual(visual);
                    PropertyGrid2.SelectedObject = null;
                }
            }
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {

        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {

        }
        DrawingVisual drawingVisualDatum;
        private void ShowDatumArea_Click(object sender, RoutedEventArgs e)
        {
            ShowDatumArea();
        }

        private void ShowDatumArea()
        {
            if (drawingVisualDatum != null)
            {
                ImageShow.RemoveVisual(drawingVisualDatum);
            }
            if (PoiParam.DatumArea.IsShowDatumArea)
            {
                switch (PoiParam.DatumArea.PointType)
                {
                    case RiPointTypes.Circle:
                        DrawingVisualDatumCircle Circle = new DrawingVisualDatumCircle();
                        Circle.Attribute.Center = PoiParam.DatumArea.Center;
                        Circle.Attribute.Radius = PoiParam.DatumArea.AreaCircleRadius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Circle.Render();
                        drawingVisualDatum = Circle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Rect:
                        double Width = PoiParam.DatumArea.AreaRectWidth;
                        double Height = PoiParam.DatumArea.AreaRectHeight;
                        DrawingVisualDatumRectangle Rectangle = new DrawingVisualDatumRectangle();
                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Center - new Vector((int)(Width / 2), (int)(Height / 2)), (PoiParam.DatumArea.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Render();
                        drawingVisualDatum = Rectangle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Mask:

                        List<Point> pts_src = new List<Point>();
                        pts_src.Add(PoiParam.DatumArea.Polygon1);
                        pts_src.Add(PoiParam.DatumArea.Polygon2);
                        pts_src.Add(PoiParam.DatumArea.Polygon3);
                        pts_src.Add(PoiParam.DatumArea.Polygon4);


                        List<Point> result = SortPolyPoints(pts_src);



                        DrawingVisualDatumPolygon Polygon = new DrawingVisualDatumPolygon() { IsDrawing = false };
                        Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon.Attribute.Brush = Brushes.Transparent;
                        Polygon.Attribute.Points.Add(result[0]);
                        Polygon.Attribute.Points.Add(result[1]);
                        Polygon.Attribute.Points.Add(result[2]);
                        Polygon.Attribute.Points.Add(result[3]);
                        Polygon.Render();
                        drawingVisualDatum = Polygon;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    default:
                        break;
                }

            }


        }

        public static List<Point> SortPolyPoints(List<Point> vPoints)
        {
            if (vPoints == null || vPoints.Count == 0) return new List<Point>();
            //计算重心
            Point center = new Point();
            double X = 0, Y = 0;
            for (int i = 0; i < vPoints.Count; i++)
            {
                X += vPoints[i].X;
                Y += vPoints[i].Y;
            }
            center = new Point((int)X / vPoints.Count, (int)Y / vPoints.Count);
            //冒泡排序
            for (int i = 0; i < vPoints.Count - 1; i++)
            {
                for (int j = 0; j < vPoints.Count - i - 1; j++)
                {
                    if (PointCmp(vPoints[j], vPoints[j + 1], center))
                    {
                        (vPoints[j + 1], vPoints[j]) = (vPoints[j], vPoints[j + 1]);
                    }
                }
            }
            return vPoints;
        }

        private static bool PointCmp(Point a, Point b, Point center)
        {
            if (a.X >= 0 && b.X < 0)
                return true;
            else if (a.X == 0 && b.X == 0)
                return a.Y > b.Y;
            //向量OA和向量OB的叉积
            double det = (a.X - center.X) * (b.Y - center.Y) - (b.X - center.X) * (a.Y - center.Y);
            if (det < 0)
                return true;
            if (det > 0)
                return false;
            //向量OA和向量OB共线，以距离判断大小
            double d1 = (a.X - center.X) * (a.X - center.X) + (a.Y - center.Y) * (a.Y - center.Y);
            double d2 = (b.X - center.X) * (b.X - center.X) + (b.Y - center.Y) * (b.Y - center.Y);
            return d1 > d2;
        }

        private void button_save_Click(object sender, RoutedEventArgs e)
        {
            if (SoftwareConfig.IsUseMySql)
            {
                PoiParam.PoiPoints.Clear();
                foreach (var item in DrawingVisualLists)
                {
                    DrawAttributeBase drawAttributeBase = item.GetAttribute();
                    if (drawAttributeBase is CircleAttribute circle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID = circle.ID,
                            Name = circle.Name,
                            PointType = RiPointTypes.Circle,
                            PixX = circle.Center.X,
                            PixY = circle.Center.Y,
                            PixWidth = circle.Radius,
                            PixHeight = circle.Radius,
                        };
                        PoiParam.PoiPoints.Add(poiParamData);

                    }
                    else if (drawAttributeBase is RectangleAttribute rectangle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID = rectangle.ID,
                            Name = rectangle.Name,
                            PointType = RiPointTypes.Rect,
                            PixX = rectangle.Rect.X,
                            PixY = rectangle.Rect.Y,
                            PixWidth = rectangle.Rect.Width,
                            PixHeight = rectangle.Rect.Height,
                        };
                        PoiParam.PoiPoints.Add(poiParamData);
                    }
                }
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Collapsed;
                WaitControlText.Text = "数据正在保存";
                Thread thread = new Thread(() =>
                {
                    TemplateControl.GetInstance().SavePOI2DB(PoiParam);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        WaitControl.Visibility = Visibility.Collapsed;
                    });
                });
                thread.Start();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("生成关注点");
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导入关注点");
        }
    }

}
