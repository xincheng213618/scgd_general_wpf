using ColorVision.MVVM;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Templates.POI
{
    public class POIFilter : ViewModelBase
    {
        public bool NoAreaEnable { get => _NoAreaEnable; set { _NoAreaEnable = value; NotifyPropertyChanged(); } }
        private bool _NoAreaEnable;
        public bool Enable { get => _Enable; set { _Enable = value; NotifyPropertyChanged(); } }
        private bool _Enable;
        public float Threshold { get => _Threshold; set { _Threshold = value; NotifyPropertyChanged(); } }
        private float _Threshold = 50;

        public bool XYZEnable { get => _XYZEnable; set { _XYZEnable = value; NotifyPropertyChanged(); } }
        private bool _XYZEnable;
        public int XYZType { get => _XYZType; set { _XYZType = value; NotifyPropertyChanged(); } }
        private int _XYZType;
    }
    public class DatumArea : ViewModelBase
    {

        public bool IsShowDatum { get => _IsShowDatum; set { _IsShowDatum = value; NotifyPropertyChanged(); } }
        private bool _IsShowDatum;

        public bool IsShowDatumArea { get => _IsShowDatumArea; set { _IsShowDatumArea = value; NotifyPropertyChanged(); } }
        private bool _IsShowDatumArea;

        public bool IsLayoutUpdated { get => _IsLayoutUpdated; set { _IsLayoutUpdated = value; NotifyPropertyChanged(); } }
        private bool _IsLayoutUpdated;

        public Point X3 { get; set; } = new Point() { X = 300, Y = 300 };
        public Point X4 { get; set; } = new Point() { X = 100, Y = 300 };
        public Point Center { get; set; } = new Point() { X = 200, Y = 200 };

        public int X1X { get => _X1X; set { _X1X = value; NotifyPropertyChanged(); } }
        private int _X1X = 100;
        public int X1Y { get => _X1Y; set { _X1Y = value; NotifyPropertyChanged(); } }
        private int _X1Y = 100;

        public int X2X { get => _X2X; set { _X2X = value; NotifyPropertyChanged(); } }
        private int _X2X = 300;

        public int X2Y { get => _X2Y; set { _X2Y = value; NotifyPropertyChanged(); } }
        private int _X2Y = 100;

        public int X3X { get => _X3X; set { _X3X = value; NotifyPropertyChanged(); } }
        private int _X3X = 300;

        public int X3Y { get => _X3Y; set { _X3Y = value; NotifyPropertyChanged(); } }
        private int _X3Y = 300;

        public int X4X { get => _X4X; set { _X4X = value; NotifyPropertyChanged(); } }
        private int _X4X = 100;

        public int X4Y { get => _X4Y; set { _X4Y = value; NotifyPropertyChanged(); } }
        private int _X4Y = 300;

        [JsonIgnore]
        public int CenterX { get => (int)Center.X; set { Center = new Point(value, Center.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int CenterY { get => (int)Center.Y; set { Center = new Point(Center.X, value); NotifyPropertyChanged(); } }

        public RiPointTypes PointType { set; get; }

        [JsonIgnore]
        public bool IsAreaCircle { get => PointType == RiPointTypes.Circle; set { if (value) PointType = RiPointTypes.Circle; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaRect { get => PointType == RiPointTypes.Rect; set { if (value) PointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaMask { get => PointType == RiPointTypes.Mask; set { if (value) PointType = RiPointTypes.Mask; NotifyPropertyChanged(); } }
        
        [JsonIgnore]
        public bool IsAreaPolygon { get => PointType == RiPointTypes.Polygon; set { if (value) PointType = RiPointTypes.Polygon; NotifyPropertyChanged(); if (value) IsUserDraw = true; } }

        public bool IsUserDraw { get => _IsUserDraw; set { _IsUserDraw = value; NotifyPropertyChanged(); } }
        private bool _IsUserDraw;

        public int AreaCircleRadius { get => _AreaCircleRadius; set { _AreaCircleRadius = value; NotifyPropertyChanged(); } }
        private int _AreaCircleRadius = 100;

        public int AreaCircleNum { get => _AreaCircleNum; set { _AreaCircleNum = value; NotifyPropertyChanged(); } }
        private int _AreaCircleNum = 6;

        public int AreaCircleAngle { get => _AreaCircleAngle; set { _AreaCircleAngle = value; NotifyPropertyChanged(); } }
        private int _AreaCircleAngle;

        public int AreaRectWidth { get => _AreaRectWidth; set { _AreaRectWidth = value; NotifyPropertyChanged(); } }
        private int _AreaRectWidth = 200;

        public int AreaRectHeight { get => _AreaRectHeight; set { _AreaRectHeight = value; NotifyPropertyChanged(); } }
        private int _AreaRectHeight = 200;

        public int AreaRectRow { get => _AreaRectRow; set { _AreaRectRow = value; NotifyPropertyChanged(); } }
        private int _AreaRectRow = 3;

        public int AreaRectCol { get => _AreaRectCol; set { _AreaRectCol = value; NotifyPropertyChanged(); } }
        private int _AreaRectCol = 3;


        public int AreaPolygonRow { get => _AreaPolygonRow; set { _AreaPolygonRow = value; NotifyPropertyChanged(); } }
        private int _AreaPolygonRow = 3;

        public int AreaPolygonCol { get => _AreaPolygonCol; set { _AreaPolygonCol = value; NotifyPropertyChanged(); } }
        private int _AreaPolygonCol = 3;

        public int AreaPolygonLenNum { get => _AreaPolygonLenNum; set { _AreaPolygonLenNum = value; NotifyPropertyChanged(); foreach (var item in Polygons)  item.SplitNumber = value; } }
        private int _AreaPolygonLenNum ;

        public bool AreaPolygonUsNode { get => _AreaPolygonUsNode; set { _AreaPolygonUsNode = value; NotifyPropertyChanged(); } }
        private bool _AreaPolygonUsNode = true;


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


        public ObservableCollection<PolygonPoint> Polygons { get; set; } = new ObservableCollection<PolygonPoint>();


        public int DefaultCircleRadius { get => _DefaultCircleRadius; set { _DefaultCircleRadius = value; NotifyPropertyChanged(); } }
        private int _DefaultCircleRadius = 10;

        public int DefaultRectWidth { get => _DefaultRectWidth; set { _DefaultRectWidth = value; NotifyPropertyChanged(); } }
        private int _DefaultRectWidth = 20;

        public int DefaultRectHeight { get => _DefaultRectHeight; set { _DefaultRectHeight = value; NotifyPropertyChanged(); } }
        private int _DefaultRectHeight = 20;


        public double LedLen1 { get => _LedLen1; set { _LedLen1 = value; NotifyPropertyChanged(); } }
        private double _LedLen1;

        public double LedLen2 { get => _LedLen2; set { _LedLen2 = value; NotifyPropertyChanged(); } }
        private double _LedLen2;

        public double LedLen3 { get => _LedLen3; set { _LedLen3 = value; NotifyPropertyChanged(); } }
        private double _LedLen3;

        public double LedLen4 { get => _LedLen4; set { _LedLen4 = value; NotifyPropertyChanged(); } }
        private double _LedLen4;

        public POIFilter Filter { get; set; }
    }


    public class PolygonPoint:ViewModelBase
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PolygonPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public int SplitNumber { get => _SplitNumber; set { _SplitNumber = value; NotifyPropertyChanged(); } }
        private int _SplitNumber =1;


        public override string ToString()
        {
            return $"X:{(int)X},Y:{(int)Y}";
        }
    }

}
