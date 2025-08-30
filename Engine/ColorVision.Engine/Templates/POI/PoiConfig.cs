﻿#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.ImageEditor;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    public enum XYZType
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    public class POIFilter : ViewModelBase
    {
        public bool NoAreaEnable { get => _NoAreaEnable; set { _NoAreaEnable = value; OnPropertyChanged(); if (value) { Enable = false; XYZEnable = false; } } }
        private bool _NoAreaEnable;
        public bool Enable { get => _Enable; set { _Enable = value; OnPropertyChanged(); if (value) { NoAreaEnable = false; XYZEnable = false; } } }
        private bool _Enable;

        public bool XYZEnable { get => _XYZEnable; set { _XYZEnable = value; OnPropertyChanged(); if (value) { NoAreaEnable = false; Enable = false; } } }
        private bool _XYZEnable;
        public XYZType XYZType { get => _XYZType; set { _XYZType = value; OnPropertyChanged(); } }
        private XYZType _XYZType;

        public float Threshold { get => _Threshold; set { _Threshold = value; OnPropertyChanged(); } }
        private float _Threshold = 50;
    }


    public class PointInt : ViewModelBase
    {
        public int X { get => _X; set { _X = value; OnPropertyChanged(); } }
        private int _X;

        public int Y { get => _Y; set { _Y = value; OnPropertyChanged(); } }
        private int _Y;
    }


    public class PoiConfig : ViewModelBase
    {
        [JsonIgnore]
        public RelayCommand SetPoiFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenPoiCIEFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand FindLuminousAreaEditCommand { get; set; }
        [JsonIgnore]
        public RelayCommand FindLuminousAreaCornerEditCommand { get; set; }
        
        [JsonIgnore]
        public RelayCommand EditCalibrationTemplateCommand { get; set; }

        public PoiConfig()
        {
            SetPoiFileCommand = new RelayCommand(a => SetPoiCIEFile());
            OpenPoiCIEFileCommand = new RelayCommand(a => OpenPoiCIEFile());
            FindLuminousAreaEditCommand = new RelayCommand(a => new PropertyEditorWindow(FindLuminousArea) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            FindLuminousAreaCornerEditCommand = new RelayCommand(a => new PropertyEditorWindow(FindLuminousAreaCorner) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            EditCalibrationTemplateCommand = new RelayCommand(a => OpenCalibrationTemplate());
        }
        [JsonIgnore]
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams => DeviceCamera?.PhyCamera?.CalibrationParams;
        [JsonIgnore]
        public DeviceCamera DeviceCamera => ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();

        public void OpenCalibrationTemplate()
        {

            if (DeviceCamera.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            var ITemplate = new TemplateCalibrationParam(DeviceCamera.PhyCamera);
            var windowTemplate = new TemplateEditorWindow(ITemplate, CalibrationTemplateIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }



        public int CalibrationTemplateIndex { get=> _CalibrationTemplateIndex; set { _CalibrationTemplateIndex = value; } }
        private int _CalibrationTemplateIndex;



        public bool LockDeafult { get => _LockDeafult; set { _LockDeafult = value; OnPropertyChanged(); } }
        private bool _LockDeafult;
        public bool UseCenter { get => _UseCenter; set { _UseCenter = value; OnPropertyChanged(); } }
        private bool _UseCenter = false;

        public double DefalutWidth { get => _DefalutWidth; set { if (LockDeafult) return;  _DefalutWidth = value; OnPropertyChanged(); } } 
        private double _DefalutWidth = 30;

        public double DefalutHeight { get => _DefalutHeight; set { if (LockDeafult) return; _DefalutHeight = value; OnPropertyChanged(); } }
        private double _DefalutHeight = 30;
        public double DefalutRadius { get => _DefalutRadius; set { if (LockDeafult) return; _DefalutRadius = value; OnPropertyChanged(); } }
        private double _DefalutRadius = 30;




        [JsonIgnore]
        public bool IsPointCircle { get => DefaultPointType == GraphicTypes.Circle; set { if (value) DefaultPointType = GraphicTypes.Circle; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DefaultPointType == GraphicTypes.Rect; set { if (value) DefaultPointType = GraphicTypes.Rect; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointMask { get => DefaultPointType == GraphicTypes.Mask; set { if (value) DefaultPointType = GraphicTypes.Rect; OnPropertyChanged(); } }
        public GraphicTypes DefaultPointType { set; get; }

        public FindLuminousArea FindLuminousArea { get; set; } = new FindLuminousArea();

        public FindLuminousAreaCorner FindLuminousAreaCorner { get; set; } = new FindLuminousAreaCorner();

        public string BackgroundFilePath { get => _BackgroundFilePath; set { _BackgroundFilePath = value; OnPropertyChanged(); } }
        private string _BackgroundFilePath;

        public string? PoiFixFilePath { get => _PoiFixFilePath; set { _PoiFixFilePath =value; OnPropertyChanged(); } }
        private string? _PoiFixFilePath;


        public bool IsShowDatum { get => _IsShowDatum; set { _IsShowDatum = value; OnPropertyChanged(); } }
        private bool _IsShowDatum;

        public bool IsShowText { get => _IsShowText; set { _IsShowText = value; OnPropertyChanged(); } }
        private bool _IsShowText = true;

        public bool IsShowPoiConfig { get => _IsShowPoiConfig; set { _IsShowPoiConfig = value; OnPropertyChanged(); } }
        private bool _IsShowPoiConfig;

        public bool IsLayoutUpdated { get => _IsLayoutUpdated; set { _IsLayoutUpdated = value; OnPropertyChanged(); } }
        private bool _IsLayoutUpdated = true;

        public Point Center { get; set; } = new Point() { X = 200, Y = 200 };


        [JsonIgnore]
        public int CenterX { get => (int)Center.X; set { Center = new Point(value, Center.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int CenterY { get => (int)Center.Y; set { Center = new Point(Center.X, value); OnPropertyChanged(); } }

        public GraphicTypes PointType { set; get; }

        [JsonIgnore]
        public bool IsAreaCircle { get => PointType == GraphicTypes.Circle; set { if (value) PointType = GraphicTypes.Circle; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaRect { get => PointType == GraphicTypes.Rect; set { if (value) PointType = GraphicTypes.Rect; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaMask { get => PointType == GraphicTypes.Mask; set { if (value) PointType = GraphicTypes.Mask; OnPropertyChanged(); } }

        [JsonIgnore]
        public bool IsAreaPolygon { get => PointType == GraphicTypes.Polygon; set { if (value) PointType = GraphicTypes.Polygon; OnPropertyChanged(); } }

        public bool IsUserDraw { get => _IsUserDraw; set { _IsUserDraw = value; OnPropertyChanged(); } }
        private bool _IsUserDraw;



        public int AreaCircleRadius { get => _AreaCircleRadius; set { _AreaCircleRadius = value; OnPropertyChanged(); } }
        private int _AreaCircleRadius = 100;

        public int AreaCircleNum { get => _AreaCircleNum; set { _AreaCircleNum = value; OnPropertyChanged(); } }
        private int _AreaCircleNum = 6;

        public int AreaCircleAngle { get => _AreaCircleAngle; set { _AreaCircleAngle = value; OnPropertyChanged(); } }
        private int _AreaCircleAngle;


        public int AreaRectX { get => _AreaRectX; set { _AreaRectX = value; OnPropertyChanged(); } }
        private int _AreaRectX ;
        public int AreaRectY { get => _AreaRectY; set { _AreaRectY = value; OnPropertyChanged(); } }
        private int _AreaRectY;

        public int AreaRectWidth { get => _AreaRectWidth; set { _AreaRectWidth = value; OnPropertyChanged(); } }
        private int _AreaRectWidth = 200;

        public int AreaRectHeight { get => _AreaRectHeight; set { _AreaRectHeight = value; OnPropertyChanged(); } }
        private int _AreaRectHeight = 200;


        public int AreaRectRow { get => _AreaRectRow; set { _AreaRectRow = value; OnPropertyChanged(); } }
        private int _AreaRectRow = 3;

        public int AreaRectCol { get => _AreaRectCol; set { _AreaRectCol = value; OnPropertyChanged(); } }
        private int _AreaRectCol = 3;


        public int AreaPolygonRow { get => _AreaPolygonRow; set { _AreaPolygonRow = value; OnPropertyChanged(); } }
        private int _AreaPolygonRow = 3;

        public int AreaPolygonCol { get => _AreaPolygonCol; set { _AreaPolygonCol = value; OnPropertyChanged(); } }
        private int _AreaPolygonCol = 3;

        public int AreaPolygonLenNum { get => _AreaPolygonLenNum; set { _AreaPolygonLenNum = value; OnPropertyChanged(); foreach (var item in Polygons) item.SplitNumber = value; } }
        private int _AreaPolygonLenNum;

        public bool AreaPolygonUsNode { get => _AreaPolygonUsNode; set { _AreaPolygonUsNode = value; OnPropertyChanged(); } }
        private bool _AreaPolygonUsNode = true;


        public Point Polygon1 { get; set; } = new Point() { X = 100, Y = 100 };
        public Point Polygon2 { get; set; } = new Point() { X = 300, Y = 100 };
        public Point Polygon3 { get; set; } = new Point() { X = 300, Y = 300 };
        public Point Polygon4 { get; set; } = new Point() { X = 100, Y = 300 };


        [JsonIgnore()]
        public int Polygon1X { get => (int)Polygon1.X; set { Polygon1 = new Point(value, Polygon1.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon1Y { get => (int)Polygon1.Y; set { Polygon1 = new Point(Polygon1.X, value); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon2X { get => (int)Polygon2.X; set { Polygon2 = new Point(value, Polygon2.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon2Y { get => (int)Polygon2.Y; set { Polygon2 = new Point(Polygon2.X, value); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon3X { get => (int)Polygon3.X; set { Polygon3 = new Point(value, Polygon3.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon3Y { get => (int)Polygon3.Y; set { Polygon3 = new Point(Polygon3.X, value); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon4X { get => (int)Polygon4.X; set { Polygon4 = new Point(value, Polygon4.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon4Y { get => (int)Polygon4.Y; set { Polygon4 = new Point(Polygon4.X, value); OnPropertyChanged(); } }

        public bool IsPointInt { get => _IsPointInt; set { _IsPointInt = value; OnPropertyChanged(); } }
        private bool _IsPointInt;

        public PointInt PointInt1 { get => _PointInt1; set { _PointInt1 = value; } }
        private PointInt _PointInt1 = new PointInt();

        public PointInt PointInt2 { get => _PointInt2; set { _PointInt2 = value; } }
        private PointInt _PointInt2 = new PointInt();
        public PointInt PointInt3 { get => _PointInt3; set { _PointInt3 = value; } }
        private PointInt _PointInt3 = new PointInt();

        public PointInt PointInt4 { get => _PointInt4; set { _PointInt4 = value; } }
        private PointInt _PointInt4 = new PointInt();



        public ObservableCollection<PolygonPoint> Polygons { get; set; } = new ObservableCollection<PolygonPoint>();

        public int DefaultCircleRadius { get => _DefaultCircleRadius; set { _DefaultCircleRadius = value; OnPropertyChanged(); } }
        private int _DefaultCircleRadius = 10;

        public int DefaultRectWidth { get => _DefaultRectWidth; set { _DefaultRectWidth = value; OnPropertyChanged(); } }
        private int _DefaultRectWidth = 20;

        public int DefaultRectHeight { get => _DefaultRectHeight; set { _DefaultRectHeight = value; OnPropertyChanged(); } }
        private int _DefaultRectHeight = 20;

        public double LedLen1 { get => _LedLen1; set { _LedLen1 = value; OnPropertyChanged(); } }
        private double _LedLen1;

        public double LedLen2 { get => _LedLen2; set { _LedLen2 = value; OnPropertyChanged(); } }
        private double _LedLen2;

        public double LedLen3 { get => _LedLen3; set { _LedLen3 = value; OnPropertyChanged(); } }
        private double _LedLen3;

        public double LedLen4 { get => _LedLen4; set { _LedLen4 = value; OnPropertyChanged(); } }
        private double _LedLen4;

        public POIFilter Filter { get; set; } = new POIFilter();

        public bool IsPoiCIEFile { get => _IsPoiCIEFile; set { _IsPoiCIEFile = value; OnPropertyChanged(); } }
        private bool _IsPoiCIEFile;

        public string PoiCIEFileName { get => _PoiCIEFileName; set { _PoiCIEFileName = value; OnPropertyChanged(); } }
        private string _PoiCIEFileName;

        public int Thickness { get => _Thickness; set { _Thickness = value; OnPropertyChanged(); } }
        private int _Thickness = 1;

        public void OpenPoiCIEFile()
        {
            if (File.Exists(PoiCIEFileName))
                if (Directory.GetParent(PoiCIEFileName)?.FullName is string FullName)
                    PlatformHelper.OpenFolder(FullName);
        }

        public void SetPoiCIEFile()
        {
            using (System.Windows.Forms.OpenFileDialog saveFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                saveFileDialog.Filter = "All Files (*.*)|*.*";
                saveFileDialog.Title = "Save File";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PoiCIEFileName = saveFileDialog.FileName;
                }
            }

        }
    }


    public class PolygonPoint : ViewModelBase
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PolygonPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public int SplitNumber { get => _SplitNumber; set { _SplitNumber = value; OnPropertyChanged(); } }
        private int _SplitNumber = 1;


        public override string ToString()
        {
            return $"X:{(int)X},Y:{(int)Y}";
        }
    }

}
