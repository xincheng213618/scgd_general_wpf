#pragma warning disable CS8629, CS8604,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class PoiResultCIEYData : PoiResultData, IViewResult
    {
        public static void SaveCsv(ObservableCollection<PoiResultCIEYData> poiResultCIExyuvDatas, string FileName)
        {
            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "名称", "位置", "大小", "形状", "Y" };

            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));

            // 写入数据行
            foreach (var item in poiResultCIExyuvDatas)
            {
                List<string> values = new()
                {
                    item.POIPoint.Id?.ToString(CultureInfo.InvariantCulture),
                    item.Name,
                    $"{item.Point.PixelX}|{item.Point.PixelY}" ,
                    $"{item.Point.Width}|{item.Point.Height}",
                    item.Shapes,
                    item.Y.ToString(CultureInfo.InvariantCulture),
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }




        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        private double _Y;

        public PoiResultCIEYData(PoiPointResultModel pOIPointResultModel) : base(pOIPointResultModel)
        {
            if (pOIPointResultModel.Value != null)
            {
                POIResultDataCIEY pOIDataCIEY = JsonConvert.DeserializeObject<POIResultDataCIEY>(pOIPointResultModel.Value);
                Y = pOIDataCIEY.Y >= 0 ? pOIDataCIEY.Y : 0.001;
            }
        }
        public PoiResultCIEYData() : base()
        {
        }

    }

    public class PoiResultData : ViewModelBase, IViewResult
    {
        public PoiResultData()
        {

        }

        public PoiPointResultModel POIPointResultModel { get; set; }

        public PoiResultData(PoiPointResultModel pOIPointResultModel)
        {
            POIPointResultModel = pOIPointResultModel;
            Point = new POIPoint(pOIPointResultModel.PoiId ?? -1, -1, pOIPointResultModel.PoiName, pOIPointResultModel.PoiType, (int)pOIPointResultModel.PoiX, (int)pOIPointResultModel.PoiY, pOIPointResultModel.PoiWidth ?? 0, pOIPointResultModel.PoiHeight ?? 0);
        }
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;


        public POIPoint Point { get { return POIPoint; } set { POIPoint = value; NotifyPropertyChanged(); } }

        public string Name { get => Point.Name; set { Point.Name = value; NotifyPropertyChanged(); } }

        public string PixelPos { get { return string.Format("{0},{1}", POIPoint.PixelX, POIPoint.PixelY); } }

        public string PixelSize { get { return string.Format("{0},{1}", POIPoint.Width, POIPoint.Height); } }

        public string Shapes => POIPoint.PointType switch
        {
            POIPointTypes.None => "None",
            POIPointTypes.SolidPoint => "点",
            POIPointTypes.Rect => "矩形",
            POIPointTypes.Polygon => "多边形",
            POIPointTypes.PolygonFour => "四边形",
            POIPointTypes.Circle or _ => "圆形 ",
        };

        public POIPoint POIPoint { get; set; }
    }
}
