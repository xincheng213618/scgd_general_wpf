#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using MQTTMessageLib.Algorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace ColorVision.Services.Devices.Algorithm.Views
{
    public class PoiResultCIExyuvData : PoiResultData
    {
        public static void SaveCsv(ObservableCollection<PoiResultCIExyuvData>  poiResultCIExyuvDatas, string FileName)
        {
            var csvBuilder = new StringBuilder();
            List<string> properties = new List<string> { "名称", "位置", "大小", "形状", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };

            // 写入列头
            for (int i = 0; i < properties.Count; i++)
            {
                // 添加列名
                csvBuilder.Append(properties[i]);

                // 如果不是最后一列，则添加逗号
                if (i < properties.Count - 1)
                    csvBuilder.Append(',');
            }
            // 添加换行符
            csvBuilder.AppendLine();


            foreach (var item in poiResultCIExyuvDatas)
            {
                if (item is PoiResultCIExyuvData poiResultCIExyuvData)
                {
                    csvBuilder.Append(poiResultCIExyuvData.Name + ",");
                    csvBuilder.Append(poiResultCIExyuvData.PixelPos + ",");
                    csvBuilder.Append(poiResultCIExyuvData.PixelSize + ",");
                    csvBuilder.Append(poiResultCIExyuvData.Shapes + ",");
                    csvBuilder.Append(poiResultCIExyuvData.CCT + ",");
                    csvBuilder.Append(poiResultCIExyuvData.Wave + ",");
                    csvBuilder.Append(poiResultCIExyuvData.X + ",");
                    csvBuilder.Append(poiResultCIExyuvData.Y + ",");
                    csvBuilder.Append(poiResultCIExyuvData.Z + ",");
                    csvBuilder.Append(poiResultCIExyuvData.u + ",");
                    csvBuilder.Append(poiResultCIExyuvData.v + ",");
                    csvBuilder.Append(poiResultCIExyuvData.x + ",");
                    csvBuilder.Append(poiResultCIExyuvData.y + ",");
                    csvBuilder.AppendLine();
                }
            }
            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }


        public double CCT { get { return _CCT; } set { _CCT = value; NotifyPropertyChanged(); } }
        public double Wave { get { return _Wave; } set { _Wave = value; NotifyPropertyChanged(); } }
        public double X { get { return _X; } set { _X = value; NotifyPropertyChanged(); } }
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        public double Z { get { return _Z; } set { _Z = value; NotifyPropertyChanged(); } }
        public double u { get { return _u; } set { _u = value; NotifyPropertyChanged(); } }
        public double v { get { return _v; } set { _v = value; NotifyPropertyChanged(); } }
        public double x { get { return _x; } set { _x = value; NotifyPropertyChanged(); } }
        public double y { get { return _y; } set { _y = value; NotifyPropertyChanged(); } }

        private double _y;
        private double _x;
        private double _u;
        private double _v;
        private double _X;
        private double _Y;
        private double _Z;
        private double _Wave;
        private double _CCT;

        public PoiResultCIExyuvData() { }

        public PoiResultCIExyuvData(POIPoint point, POIDataCIExyuv data)
        {
            Point = point;
            u = data.u;
            v = data.v;
            x = data.x;
            y = data.y;
            X = data.X;
            Y = data.Y;
            Z = data.Z;
            CCT = data.CCT;
            Wave = data.Wave;
        }
    }
}
