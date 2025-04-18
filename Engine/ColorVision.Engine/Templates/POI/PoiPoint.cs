using ColorVision.UI.Sorts;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Templates.POI
{
    public class PoiPointParam : ViewModelBase
    {

        public bool DoKey { get => _DoKey; set { _DoKey = value; NotifyPropertyChanged(); } }
        private bool _DoKey = true;
        public bool DoHalo { get => _DoHalo; set { _DoHalo = value; NotifyPropertyChanged(); } }
        private bool _DoHalo = true;

        /// <summary>
        /// 结果缩放
        /// </summary>
        public double KeyScale { get => _KeyScale; set { _KeyScale = value; NotifyPropertyChanged(); } }
        private double _KeyScale = 1;
        /// <summary>
        /// 结果缩放
        /// </summary>
        public double HaloScale { get => _HaloScale; set { _HaloScale = value; NotifyPropertyChanged(); } }
        private double _HaloScale = 1;

        public int HaloThreadV { get => _HaloThreadV; set { _HaloThreadV = value; NotifyPropertyChanged(); } }
        private int _HaloThreadV = 500;

        public int KeyThreadV { get => _KeyThreadV; set { _KeyThreadV = value; NotifyPropertyChanged(); } }
        private int _KeyThreadV = 3000;

        public int HaloOutMOVE { get => _HaloOutMOVE; set { _HaloOutMOVE = value; NotifyPropertyChanged(); } }
        private int _HaloOutMOVE = 20;

        public int KeyOutMOVE { get => _KeyOutMOVE; set { _KeyOutMOVE = value; NotifyPropertyChanged(); } }
        private int _KeyOutMOVE = 5;

        public int KeyOffsetX { get => _KeyOffsetX; set { _KeyOffsetX = value; NotifyPropertyChanged(); } }
        private int _KeyOffsetX;
        public int KeyOffsetY { get => _KeyOffsetY; set { _KeyOffsetY = value; NotifyPropertyChanged(); } }
        private int _KeyOffsetY;

        public int HaloOffsetX { get => _HaloOffsetX; set { _HaloOffsetX = value; NotifyPropertyChanged(); } }
        private int _HaloOffsetX;

        public int HaloSize { get => _HaloSize; set { _HaloSize = value; NotifyPropertyChanged(); } }
        private int _HaloSize;


        public int HaloOffsetY { get => _HaloOffsetY; set { _HaloOffsetY = value; NotifyPropertyChanged(); } }
        private int _HaloOffsetY;

        /// <summary>
        /// 面积
        /// </summary>
        public double Area { get => _Area; set { _Area = value; NotifyPropertyChanged(); } }
        private double _Area = 1;

        /// <summary>
        /// 辉度
        /// </summary>
        public double Brightness { get => _Brightness; set { _Brightness = value; NotifyPropertyChanged(); } }
        private double _Brightness;
    }






    public class PoiPoint : ISortID
    {
        public PoiPoint(PoiDetailModel dbModel)
        {
            Id = dbModel.Id;
            Name = dbModel.Name ?? dbModel.Id.ToString();
            PointType = dbModel.Type;
            PixX = dbModel.PixX ?? 0;
            PixY = dbModel.PixY ?? 0;
            PixWidth = dbModel.PixWidth ?? 0;
            PixHeight = dbModel.PixHeight ?? 0;
        }

        public PoiPoint()
        {
        }

        public int Id { set; get; }

        public string Name { set; get; }
        public RiPointTypes PointType { set; get; }
        public double PixX { set; get; }
        public double PixY { set; get; }
        public double PixWidth { set; get; }
        public double PixHeight { set; get; }
        public PoiPointParam Param { get;set; }
    }

}
