﻿#pragma warning disable CS8629, CS8604
using ColorVision.Common.MVVM;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI
{
    public class PoiResultCIEYData : PoiResultData, IViewResult
    {
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        private double _Y;

        public PoiResultCIEYData(PoiPointResultModel pOIPointResultModel) : base(pOIPointResultModel)
        {
            if (pOIPointResultModel.Value != null)
            {
                POIDataCIEY pOIDataCIEY = JsonConvert.DeserializeObject<POIDataCIEY>(pOIPointResultModel.Value);
                Y = pOIDataCIEY.Y >= 0 ? pOIDataCIEY.Y : 0.001;
            }
        }

    }

    public class PoiResultData : ViewModelBase, IViewResult
    {
        public PoiResultData()
        {

        }
        public ObservableCollection<ValidateRuleResult>? ValidateSingles { get; set; }

        public PoiPointResultModel POIPointResultModel { get; set; }

        public PoiResultData(PoiPointResultModel pOIPointResultModel)
        {
            POIPointResultModel = pOIPointResultModel;
            if (pOIPointResultModel.ValidateResult != null)
                ValidateSingles = JsonConvert.DeserializeObject<ObservableCollection<ValidateRuleResult>>(pOIPointResultModel.ValidateResult);
            Point = new POIPoint(pOIPointResultModel.PoiId ?? -1, -1, pOIPointResultModel.PoiName, pOIPointResultModel.PoiType, (int)pOIPointResultModel.PoiX, (int)pOIPointResultModel.PoiY, pOIPointResultModel.PoiWidth ?? 0, pOIPointResultModel.PoiHeight ?? 0);
        }
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;


        public POIPoint Point { get { return POIPoint; } set { POIPoint = value; NotifyPropertyChanged(); } }

        public string Name { get { return POIPoint.Name; } }
        public string PixelPos { get { return string.Format("{0},{1}", POIPoint.PixelX, POIPoint.PixelY); } }

        public string PixelSize { get { return string.Format("{0},{1}", POIPoint.Width, POIPoint.Height); } }

        public string Shapes => POIPoint.PointType switch
        {
            POIPointTypes.None => "None",
            POIPointTypes.SolidPoint => "点",
            POIPointTypes.Rect => "矩形",
            POIPointTypes.Mask => "多边形",
            POIPointTypes.Circle or _ => "圆形 ",
        };

        protected POIPoint POIPoint { get; set; }
    }
}
