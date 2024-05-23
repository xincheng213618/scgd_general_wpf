#pragma warning disable CS8629, CS8604
using ColorVision.Common.MVVM;
using ColorVision.Services.Devices.Algorithm.Dao;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Services.Devices.Algorithm.Views
{
    public class PoiResultCIEYData : PoiResultData
    {
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        private double _Y;

        public PoiResultCIEYData(POIPointResultModel pOIPointResultModel) : base(pOIPointResultModel)
        {
            if (pOIPointResultModel.Value != null)
            {
                POIDataCIEY pOIDataCIEY = JsonConvert.DeserializeObject<POIDataCIEY>(pOIPointResultModel.Value);
                Y = pOIDataCIEY.Y >= 0 ? pOIDataCIEY.Y : 0.001;
            }
        }

    }

    public class PoiResultData : ViewModelBase
    {
        public PoiResultData()
        {

        }
        public ObservableCollection<ValidateRuleResult>? ValidateSingles { get; set; }

        public POIPointResultModel POIPointResultModel { get; set; }

        public PoiResultData(POIPointResultModel pOIPointResultModel)
        {
            POIPointResultModel = pOIPointResultModel;
            if (pOIPointResultModel.ValidateResult !=null)
                ValidateSingles = JsonConvert.DeserializeObject<ObservableCollection<ValidateRuleResult>>(pOIPointResultModel.ValidateResult);
            Point = new POIPoint(pOIPointResultModel.PoiId??-1, -1, pOIPointResultModel.PoiName, (POIPointTypes)pOIPointResultModel.PoiType, (int)pOIPointResultModel.PoiX, (int)pOIPointResultModel.PoiY, (int)pOIPointResultModel.PoiWidth, (int)pOIPointResultModel.PoiHeight);
        }

        public POIPoint Point { get { return POIPoint; } set { POIPoint = value; NotifyPropertyChanged(); } }

        public string Name { get { return POIPoint.Name; } }
        public string PixelPos { get { return string.Format("{0},{1}", POIPoint.PixelX, POIPoint.PixelY); } }

        public string PixelSize { get { return string.Format("{0},{1}", POIPoint.Width, POIPoint.Height); } }

        public string Shapes { get { return string.Format("{0}", POIPoint.PointType == 0 ? "圆形" : "矩形"); } }

        protected POIPoint POIPoint { get; set; }
    }
}
