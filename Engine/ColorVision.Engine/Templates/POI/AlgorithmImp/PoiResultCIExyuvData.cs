#pragma warning disable CA1708,CS8604,CS8602
using ColorVision.Engine.Interfaces;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class PoiResultCIExyuvData : PoiResultData, IViewResult
    {
        public static void SaveCsv(ObservableCollection<PoiResultCIExyuvData> poiResultCIExyuvDatas, string FileName)
        {

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "名称", "位置", "大小", "形状", "CCT", "Wave", "X", "Y", "Z", "u'", "v'", "x", "y" };

            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));

            // 写入数据行
            foreach (var item in poiResultCIExyuvDatas)
            {
                if (item.Name == null) item.Name = string.Empty;

                if (item.Name.Contains(',') || item.Name.Contains('"'))
                {
                    item.Name = $"\"{item.Name.Replace("\"", "\"\"")}\"";
                }
                List<string> values = new()
                {
                    item.POIPoint.Id?.ToString(CultureInfo.InvariantCulture),
                    item.Name,
                    $"{item.Point.PixelX}|{item.Point.PixelY}" ,
                    $"{item.Point.Width}|{item.Point.Height}",
                    item.Shapes,
                    item.CCT.ToString(CultureInfo.InvariantCulture),
                    item.Wave.ToString(CultureInfo.InvariantCulture),
                    item.X.ToString(CultureInfo.InvariantCulture),
                    item.Y.ToString(CultureInfo.InvariantCulture),
                    item.Z.ToString(CultureInfo.InvariantCulture),
                    item.u.ToString(CultureInfo.InvariantCulture),
                    item.v.ToString(CultureInfo.InvariantCulture),
                    item.x.ToString(CultureInfo.InvariantCulture),
                    item.y.ToString(CultureInfo.InvariantCulture)
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            // 统计计算
            var maxValues = new Dictionary<string, double>();
            var minValues = new Dictionary<string, double>();
            var sumValues = new Dictionary<string, double>();
            var maxNames = new Dictionary<string, string>();
            var minNames = new Dictionary<string, string>();
            var count = poiResultCIExyuvDatas.Count;

            // 初始化字典
            foreach (var property in properties.Skip(4)) // 假设前四个属性不是数字
            {
                maxValues[property] = double.MinValue;
                minValues[property] = double.MaxValue;
                sumValues[property] = 0.0;
                maxNames[property] = string.Empty;
                minNames[property] = string.Empty;

                // 计算最大值、最小值和总和
                foreach (var item in poiResultCIExyuvDatas)
                {
                    if (typeof(PoiResultCIExyuvData).GetProperty(property)?.GetValue(item) is double dd)
                    {
                        if (dd > maxValues[property])
                        {
                            maxValues[property] = dd;
                            maxNames[property] = item.Name ?? item.Id.ToString();
                        }
                        if (dd < minValues[property])
                        {
                            minValues[property] = dd;
                            minNames[property] = item.Name ?? item.Id.ToString();
                        }
                        sumValues[property] += dd;
                    }
                }
            }



            // 计算平均值
            var meanValues = sumValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / count);

            // 计算方差
            var varianceValues = new Dictionary<string, double>();
            foreach (var property in properties.Skip(4)) // 假设前三个属性不是数字
            {
                varianceValues[property] = 0.0;

                foreach (var item in poiResultCIExyuvDatas)
                {
                    if (typeof(PoiResultCIExyuvData).GetProperty(property)?.GetValue(item) is double dd)
                    {
                        varianceValues[property] += Math.Pow(dd - meanValues[property], 2);
                    }
                }
                varianceValues[property] = varianceValues[property] / count;
            }


            // 将统计数据添加到CSV
            csvBuilder.AppendLine("\n统计信息");
            csvBuilder.AppendLine("属性,最大值,最大值所在名称,最小值,最小值所在名称,平均值,方差,均匀性");
            foreach (var property in properties.Skip(4))
            {
                double uniformity = (maxValues[property] != 0) ? (minValues[property] / maxValues[property]) * 100 : 0;

                List<string> stats = new()
        {
            property,
            maxValues[property].ToString(CultureInfo.InvariantCulture),
            maxNames[property].ToString(CultureInfo.InvariantCulture),
            minValues[property].ToString(CultureInfo.InvariantCulture),
            minNames[property].ToString(CultureInfo.InvariantCulture),
            meanValues[property].ToString(CultureInfo.InvariantCulture),
            varianceValues[property].ToString(CultureInfo.InvariantCulture),
            uniformity.ToString("F2", CultureInfo.InvariantCulture)
        };
                csvBuilder.AppendLine(string.Join(",", stats));
            }

            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }

        public double CCT { get { return _CCT; } set { _CCT = value; NotifyPropertyChanged(); } }
        private double _CCT;

        public double Wave { get { return _Wave; } set { _Wave = value; NotifyPropertyChanged(); } }
        private double _Wave;

        public double X { get { return _X; } set { _X = value; NotifyPropertyChanged(); } }
        private double _X;

        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        private double _Y;

        public double Z { get { return _Z; } set { _Z = value; NotifyPropertyChanged(); } }
        private double _Z;

        public double u { get { return _u; } set { _u = value; NotifyPropertyChanged(); } }
        private double _u;

        public double v { get { return _v; } set { _v = value; NotifyPropertyChanged(); } }
        private double _v;
        public double x { get { return _x; } set { _x = value; NotifyPropertyChanged(); } }
        private double _x;

        public double y { get { return _y; } set { _y = value; NotifyPropertyChanged(); } }
        private double _y;


        public PoiResultCIExyuvData() { }

        public PoiResultCIExyuvData(PoiPointResultModel pOIPointResultModel) : base(pOIPointResultModel)
        {
            if (pOIPointResultModel.Value != null)
            {
                POIResultDataCIExyuv pOIDataCIExyuv = JsonConvert.DeserializeObject<POIResultDataCIExyuv>(pOIPointResultModel.Value);
                CCT = pOIDataCIExyuv.CCT;
                Wave = pOIDataCIExyuv.Wave;

                //这里是因为输出不会小于0所以做一个置位
                X = pOIDataCIExyuv.X >= 0 ? pOIDataCIExyuv.X : 0.001;
                Y = pOIDataCIExyuv.Y >= 0 ? pOIDataCIExyuv.Y : 0.001;
                Z = pOIDataCIExyuv.Z >= 0 ? pOIDataCIExyuv.Z : 0.001;
                u = pOIDataCIExyuv.u >= 0 ? pOIDataCIExyuv.u : 0.001;
                v = pOIDataCIExyuv.v >= 0 ? pOIDataCIExyuv.v : 0.001;
                x = pOIDataCIExyuv.x >= 0 ? pOIDataCIExyuv.x : 0.001;
                y = pOIDataCIExyuv.y >= 0 ? pOIDataCIExyuv.y : 0.001;
            }
        }


    }
}
