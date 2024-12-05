using ColorVision.UI.Sorts;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Engine.Templates.POI
{
    public class PoiPointParam : ViewModelBase
    {
        public double KeyScale { get => _KeyScale; set { _KeyScale = value; NotifyPropertyChanged(); } }
        private double _KeyScale = 1;

        public double HaloScale { get => _HaloScale; set { _HaloScale = value; NotifyPropertyChanged(); } }
        private double _HaloScale = 1;
        public int HaloThreadV { get => _HaloThreadV; set { _HaloThreadV = value; NotifyPropertyChanged(); } }
        private int _HaloThreadV = 500;

        public int KeyThreadV { get => _KeyThreadV; set { _KeyThreadV = value; NotifyPropertyChanged(); } }
        private int _KeyThreadV = 3000;

        public int HaloOutMOVE { get => _HaloOutMOVE; set { _HaloOutMOVE = value; NotifyPropertyChanged(); } }
        private int _HaloOutMOVE = 20;

        public int KeyOutMOVE { get => _KeyOutMOVE; set { _KeyOutMOVE = value; NotifyPropertyChanged(); } }
        private int _KeyOutMOVE = 35;
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
            ValidateTId = dbModel.ValidateTId;
            try
            {
                Param = JsonConvert.DeserializeObject<PoiPointParam>(dbModel.Remark) ?? new PoiPointParam();
            }
            catch
            {
                Param = new PoiPointParam();
            }
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

        public int? ValidateTId { set; get; }

        public PoiPointParam Param { get;set; }
    }

}
