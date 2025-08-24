#pragma  warning disable CA1708,CS8602,CS8604,CS8629,CS8601
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Templates.JND
{
    public class ViewRsultJND : PoiResultData, IViewResult
    {
        public static void SaveCsv(ObservableCollection<ViewRsultJND> ViewRsultJNDs, string FileName)
        {
            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "名称", "位置", "大小", "形状", "h_jnd", "v_jnd" };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            foreach (var item in ViewRsultJNDs)
            {
                List<string> values = new()
                {
                    item.POIPoint.Id?.ToString(CultureInfo.InvariantCulture),
                    item.Name,
                    $"{item.Point.PixelX}|{item.Point.PixelY}" ,
                    $"{item.Point.Width}|{item.Point.Height}",
                    item.Shapes,
                    item.JND.h_jnd.ToString(),
                    item.JND.v_jnd.ToString(),
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }




        public MQTTMessageLib.Algorithm.POIResultDataJND JND { get { return _JND; } set { _JND = value; OnPropertyChanged(); } }

        private MQTTMessageLib.Algorithm.POIResultDataJND _JND;

        public PoiPointResultModel AlgResultJNDModel { get; set; }

        public ViewRsultJND(PoiPointResultModel detail)
        {
            AlgResultJNDModel = detail;
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
            JND = JsonConvert.DeserializeObject<MQTTMessageLib.Algorithm.POIResultDataJND>(detail.Value);
        }
    }
}
