#pragma warning disable CS8629, CS8604,CS8602
using ColorVision.Common.MVVM;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class PoiResultCIEYData : PoiResultData, IViewResult
    {
        /// <summary>
        /// 保存仅包含 Y 的 CSV
        /// </summary>
        /// <param name="items">数据集合</param>
        /// <param name="fileName">文件名</param>
        /// <param name="includeLegacyStats">是否附加旧版“统计信息”表格</param>
        public static void SaveCsv(ObservableCollection<PoiResultCIEYData> items, string fileName, bool includeLegacyStats = true)
        {
            var culture = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            // --------------- 数据区列头 ---------------
            var headers = new[] { "Id", "名称", "位置", "大小", "形状", "Y" };
            sb.AppendLine(string.Join(",", headers));

            if (items == null || items.Count == 0)
            {
                // 空数据也保持格式：空行 + Measurement + （可选）统计信息
                AppendBlankLines(sb, 3, headers.Length);
                AppendMeasurementSection(sb, null);
                if (includeLegacyStats) AppendLegacyStats(sb, null);
                File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
                return;
            }

            // --------------- 数据行 ---------------
            foreach (var it in items)
            {
                string id = it.POIPoint?.Id?.ToString(culture) ?? "";
                string name = EscapeCsv(it.Name);
                string pos = it.Point != null ? $"{it.Point.PixelX}|{it.Point.PixelY}" : "|";
                string size = it.Point != null ? $"{it.Point.Width}|{it.Point.Height}" : "|";
                string shape = EscapeCsv(it.Shapes);
                string yStr = it.Y.ToString(culture);

                sb.AppendLine(string.Join(",", id, name, pos, size, shape, yStr));
            }

            // --------------- 空行 ---------------
            AppendBlankLines(sb, 3, headers.Length - 1); // -1 -> (列数-1)个逗号

            // --------------- Measurement Section ---------------
            AppendMeasurementSection(sb, items);

            // --------------- 旧版统计信息（可选）---------------
            if (includeLegacyStats)
                AppendLegacyStats(sb, items);

            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
        }

        #region Measurement Section

        /// <summary>
        /// 输出 Measurement Item 统计块
        /// 仅包含 Y 相关：Center / Avg / Max / Min / Uniformities / Standard Deviation
        /// </summary>
        private static void AppendMeasurementSection(StringBuilder sb, ObservableCollection<PoiResultCIEYData>? items)
        {
            sb.AppendLine("Measurement Item,Value,Unit,,,,,,,,,,,,");
            if (items == null || items.Count == 0) return;

            var culture = CultureInfo.InvariantCulture;
            var luminances = items.Select(o => o.Y).ToList();

            double avg = luminances.Average();
            double max = luminances.Max();
            double min = luminances.Min();
            double std = Math.Sqrt(luminances.Sum(v => Math.Pow(v - avg, 2)) / luminances.Count); // 总体标准差

            var center = FindCenterPoint(items);

            double uniformityMinDivMax = max != 0 ? min / max * 100.0 : 0;
            double uniformityDiffDivAvg = avg != 0 ? (max - min) / avg * 100.0 : 0;
            double uniformityDiffDivMax = max != 0 ? (max - min) / max * 100.0 : 0;
            double stdPercent = avg != 0 ? std / avg * 100.0 : 0;

            void Row(string item, object? value, string unit = "")
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(item),
                    value == null ? "" : Convert.ToString(value, culture),
                    unit,
                    "", "", "", "", "", "", "", "", "", ""));
            }

            Row("Center Luminance", center == null ? "" : center.Y.ToString(culture), "cd/m^2");
            Row("Average Luminance", avg.ToString(culture), "cd/m^2");
            Row("Max Luminance", max.ToString(culture), "cd/m^2");
            Row("Min Luminance", min.ToString(culture), "cd/m^2");
            Row("Luminance uniformity(Min/Max*100%)", uniformityMinDivMax.ToString(culture), "%");
            Row("Luminance uniformity((Max-Min)/Avg*100%)", uniformityDiffDivAvg.ToString(culture), "%");
            Row("Luminance uniformity(((Max-Min)/Max*100%)", uniformityDiffDivMax.ToString(culture), "%");
            Row("Standard Deviation Lv", std.ToString(culture), "STDEV(Lv)");
            Row("Standard Deviation Lv (%)", stdPercent.ToString(culture), "% (Stdev/Avg*100%)");

            // 色度 / CCT / 波长等因单通道缺失，不输出
        }

        #endregion

        #region Legacy Stats Section

        /// <summary>
        /// 旧版统计信息：仅 Y
        /// 表头：属性,最大值,最大值所在名称,最小值,最小值所在名称,平均值,方差,均匀性
        /// 均匀性 = (Min/Max)*100
        /// </summary>
        private static void AppendLegacyStats(StringBuilder sb, ObservableCollection<PoiResultCIEYData>? items)
        {
            sb.AppendLine();
            sb.AppendLine("统计信息");
            sb.AppendLine("属性,最大值,最大值所在名称,最小值,最小值所在名称,平均值,方差,均匀性");

            if (items == null || items.Count == 0) return;

            var culture = CultureInfo.InvariantCulture;
            var list = items.ToList();
            var YValues = list.Select(o => o.Y).ToList();

            double max = YValues.Max();
            double min = YValues.Min();
            double avg = YValues.Average();
            double variance = YValues.Sum(v => Math.Pow(v - avg, 2)) / YValues.Count; // 总体方差
            double uniformity = max != 0 ? (min / max) * 100.0 : 0;

            int idxMax = YValues.IndexOf(max);
            int idxMin = YValues.IndexOf(min);

            string nameMax = GetDisplayName(list, idxMax);
            string nameMin = GetDisplayName(list, idxMin);

            string line = string.Join(",",
                "Y",
                max.ToString(culture),
                EscapeCsv(nameMax),
                min.ToString(culture),
                EscapeCsv(nameMin),
                avg.ToString(culture),
                variance.ToString(culture),
                uniformity.ToString("F2", culture));

            sb.AppendLine(line);
        }

        #endregion

        #region Helpers

        private static void AppendBlankLines(StringBuilder sb, int count, int commaCountPerLine)
        {
            // commaCountPerLine = 列数 - 1
            string line = new string(',', commaCountPerLine);
            for (int i = 0; i < count; i++)
                sb.AppendLine(line);
        }

        private static PoiResultCIEYData? FindCenterPoint(IList<PoiResultCIEYData> list)
        {
            if (list == null || list.Count == 0) return null;

            // 1) 名称包含 Center
            var centerByName = list.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.Name) &&
                o.Name.IndexOf("Center", StringComparison.OrdinalIgnoreCase) >= 0);
            if (centerByName != null) return centerByName;

            // 2) 按几何中心
            double avgX = list.Average(o => (double)(o.Point?.PixelX ?? 0));
            double avgY = list.Average(o => (double)(o.Point?.PixelY ?? 0));

            PoiResultCIEYData? closest = null;
            double best = double.MaxValue;
            foreach (var it in list)
            {
                double dx = (it.Point?.PixelX ?? 0) - avgX;
                double dy = (it.Point?.PixelY ?? 0) - avgY;
                double d2 = dx * dx + dy * dy;
                if (d2 < best)
                {
                    best = d2;
                    closest = it;
                }
            }
            return closest;
        }

        private static string GetDisplayName(List<PoiResultCIEYData> list, int index)
        {
            if (index < 0 || index >= list.Count) return "";
            var it = list[index];
            if (string.IsNullOrWhiteSpace(it.Name))
            {
                // 尝试用 POI Id 或 P_index
                if (it.POIPoint?.Id.HasValue == true)
                    return $"P_{it.POIPoint.Id.Value}";
                return $"P_{index}";
            }
            return it.Name;
        }

        private static string EscapeCsv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains('"')) s = s.Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return $"\"{s}\"";
            return s;
        }

        #endregion




        public double Y { get { return _Y; } set { _Y = value; OnPropertyChanged(); } }
        private double _Y;

        public PoiResultCIEYData(PoiPointResultModel pOIPointResultModel) : base(pOIPointResultModel)
        {
            if (pOIPointResultModel.Value != null)
            {
                POIResultDataCIEY pOIDataCIEY = JsonConvert.DeserializeObject<POIResultDataCIEY>(pOIPointResultModel.Value);
                //这里是因为输出不会小于0所以做一个置位
                //老板说，先改成>0试试
                Y = pOIDataCIEY.Y > 0 ? pOIDataCIEY.Y : 0.0001;
            }
        }

        public PoiResultCIEYData() : base()
        {
        }

    }

    public class PoiResultData : ViewModelBase, IViewResult
    {
        public ContextMenu ContextMenu { get; set; }
        public PoiResultData()
        {

        }

        public PoiPointResultModel POIPointResultModel { get; set; }

        public PoiResultData(PoiPointResultModel pOIPointResultModel)
        {
            POIPointResultModel = pOIPointResultModel;
            Point = new POIPoint(pOIPointResultModel.PoiId ?? -1, pOIPointResultModel.Pid ??-1, pOIPointResultModel.PoiName, pOIPointResultModel.PoiType, (int)pOIPointResultModel.PoiX, (int)pOIPointResultModel.PoiY, pOIPointResultModel.PoiWidth ?? 0, pOIPointResultModel.PoiHeight ?? 0);
        }
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;


        public POIPoint Point { get { return POIPoint; } set { POIPoint = value; OnPropertyChanged(); } }

        public string Name { get => Point.Name; set { Point.Name = value; OnPropertyChanged(); } }

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
