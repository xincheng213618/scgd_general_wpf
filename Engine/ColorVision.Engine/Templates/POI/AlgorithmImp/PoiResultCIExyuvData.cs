#pragma warning disable CA1708,CS8604,CS8602
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{

    public static class POIResultCIExyuvDataHelper
    {
        /// <summary>
        /// 生成示例风格的 CSV，并在末尾附加旧版“统计信息”表格
        /// </summary>
        public static void SaveCsv(this ObservableCollection<PoiResultCIExyuvData> items, string fileName)
        {
            var csv = new StringBuilder();
            var culture = CultureInfo.InvariantCulture;

            // ---------------------------
            //  数据区列头
            // ---------------------------
            string[] dataHeaders = new[]
            {
                "number","shape","center_x","center_y","w_Radius","h",
                "X","Y(luminance)","Z","CIE 1931Cx","CIE 1931Cy","CIE 1976u'","CIE 1976v'","CCT(K)","DominantWave(nm)"
            };
            csv.AppendLine(string.Join(",", dataHeaders));

            if (items == null || items.Count == 0)
            {
                AppendBlankLines(csv, 5);
                AppendMeasurementSection(csv, null);
                // 仍然输出空的统计信息块
                AppendLegacyStats(csv, null);
                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
                return;
            }

            // ---------------------------
            //  数据区内容
            // ---------------------------
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                string number = string.IsNullOrWhiteSpace(it.Name) ? $"P_{i}" : EscapeCsv(it.Name);
                string shape = EscapeCsv(it.Shapes);

                float cx = it.Point?.PixelX ?? 0;
                float cy = it.Point?.PixelY ?? 0;
                float w = it.Point?.Width ?? 100;
                float h = it.Point?.Height ?? 0;

                string wRadius = w.ToString(culture);
                string hh = h == 0 ? "" : h.ToString(culture);

                string[] row =
                {
                    number,
                    shape,
                    cx.ToString(culture),
                    cy.ToString(culture),
                    wRadius,
                    hh,
                    FormatDouble(it.X),
                    FormatDouble(it.Y),
                    FormatDouble(it.Z),
                    FormatDouble(it.x),
                    FormatDouble(it.y),
                    FormatDouble(it.u),
                    FormatDouble(it.v),
                    FormatDouble(it.CCT),
                    FormatDouble(it.Wave) // 这里保持向后兼容: 如果字段名 Wave -> 用 it.Wave
                };
                // 修正 DominantWave 字段名 (原属性是 Wave)
                row[row.Length - 1] = FormatDouble(it.Wave);

                csv.AppendLine(string.Join(",", row));
            }

            // ---------------------------
            //  空行 (与示例风格保持)
            // ---------------------------
            AppendBlankLines(csv, 5);

            // ---------------------------
            //  Measurement Section
            // ---------------------------
            AppendMeasurementSection(csv, items);

            // ---------------------------
            //  追加旧版统计信息表格
            // ---------------------------
            AppendLegacyStats(csv, items);

            File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
        }

        private static void AppendBlankLines(StringBuilder sb, int count)
        {
            for (int i = 0; i < count; i++)
                sb.AppendLine(new string(',', 14)); // 15 列 -> 14 个逗号
        }

        private static void AppendMeasurementSection(StringBuilder sb, ObservableCollection<PoiResultCIExyuvData>? items)
        {
            sb.AppendLine("Measurement Item,Value,Unit,,,,,,,,,,,,");
            if (items == null || items.Count == 0) return;

            var culture = CultureInfo.InvariantCulture;
            var luminances = items.Select(o => o.Y).ToList();
            double avgL = luminances.Average();
            double maxL = luminances.Max();
            double minL = luminances.Min();
            double stdL = Math.Sqrt(luminances.Sum(v => Math.Pow(v - avgL, 2)) / luminances.Count); // 总体标准差

            var center = FindCenterPoint(items);

            double maxX = items.Max(o => o.x);
            double minX = items.Min(o => o.x);
            double maxY = items.Max(o => o.y);
            double minY = items.Min(o => o.y);

            double deltaUv = CalcMaxDeltaUv(items);
            double deltaX = maxX - minX;
            double deltaY = maxY - minY;

            double maxWave = items.Max(o => o.Wave);
            double minWave = items.Min(o => o.Wave);
            double deltaWave = maxWave - minWave;

            double uniformityMinDivMax = maxL != 0 ? minL / maxL * 100.0 : 0;
            double uniformityMaxMinDivAvg = avgL != 0 ? (maxL - minL) / avgL * 100.0 : 0;
            double uniformityMaxMinDivMax = maxL != 0 ? (maxL - minL) / maxL * 100.0 : 0;
            double stdPercent = avgL != 0 ? stdL / avgL * 100.0 : 0;

            void Row(string item, object? value, string unit = "")
            {
                sb.AppendLine(string.Join(",", EscapeCsv(item),
                    value == null ? "" : Convert.ToString(value, culture),
                    unit,
                    "", "", "", "", "", "", "", "", "", ""));
            }

            Row("Center Luminance", center == null ? "" : FormatDouble(center.Y), "cd/m^2");
            Row("Average Luminance", FormatDouble(avgL), "cd/m^2");
            Row("Max Luninance", FormatDouble(maxL), "cd/m^2");
            Row("Min Luninance", FormatDouble(minL), "cd/m^2");
            Row("Luminance uniformity(Min/Max*100%)", FormatDouble(uniformityMinDivMax), "%");
            Row("Luminance uniformity((Max-Min)/Avg*100%)", FormatDouble(uniformityMaxMinDivAvg), "%");
            Row("Luminance uniformity(((Max-Min)/Max*100%)", FormatDouble(uniformityMaxMinDivMax), "%");
            Row("Standard Deviation Lv", FormatDouble(stdL), "STDEV(lv)");
            Row("Standard Deviation Lv (%)", FormatDouble(stdPercent), "% (Stdev/Avg*100%)");
            Row("Color Uniformity(Δuv)", FormatDouble(deltaUv), "Δuv=max sqrt((ui-uj)^2+(vi-vj)^2)");
            Row("Color Uniformity(Δx)", FormatDouble(deltaX), "x_max-x_min");
            Row("Color Uniformity(Δy)", FormatDouble(deltaY), "y_max-y_min");

            if (center != null)
            {
                Row("Center CIE1931 Chromatic Coordinates x", FormatDouble(center.x));
                Row("Center CIE1931 Chromatic Coordinates y", FormatDouble(center.y));
                Row("Center CIE1976 Chromatic Coordinates u'", FormatDouble(center.u));
                Row("Center CIE1976 Chromatic Coordinates v'", FormatDouble(center.v));
                Row("Center Correlated Color Temperature(CCT)", FormatDouble(center.CCT), "K");
                Row("Center DominantWave(D)", FormatDouble(center.Wave), "nm");
            }
            else
            {
                Row("Center CIE1931 Chromatic Coordinates x", "");
                Row("Center CIE1931 Chromatic Coordinates y", "");
                Row("Center CIE1976 Chromatic Coordinates u'", "");
                Row("Center CIE1976 Chromatic Coordinates v'", "");
                Row("Center Correlated Color Temperature(CCT)", "", "K");
                Row("Center DominantWave(D)", "", "nm");
            }

            Row("Delta DominantWave(ΔD)", FormatDouble(deltaWave), "nm (max-min)");
        }

        /// <summary>
        /// 附加旧版统计信息表格
        /// 属性,最大值,最大值所在名称,最小值,最小值所在名称,平均值,方差,均匀性
        /// 均匀性 = (Min/Max)*100
        /// </summary>
        private static void AppendLegacyStats(StringBuilder sb, ObservableCollection<PoiResultCIExyuvData>? items)
        {
            sb.AppendLine(); // 分隔空行
            sb.AppendLine("统计信息");
            sb.AppendLine("属性,最大值,最大值所在名称,最小值,最小值所在名称,平均值,方差,均匀性");

            if (items == null || items.Count == 0) return;

            var list = items.ToList();
            var culture = CultureInfo.InvariantCulture;

            // 要统计的属性对应 getter 委托
            var statsTargets = new (string Label, Func<PoiResultCIExyuvData, double> Selector)[]
            {
                ("CCT", o => o.CCT),
                ("Wave", o => o.Wave),
                ("X", o => o.X),
                ("Y", o => o.Y),
                ("Z", o => o.Z),
                ("u'", o => o.u),
                ("v'", o => o.v),
                ("x", o => o.x),
                ("y", o => o.y),
            };

            for (int i = 0; i < statsTargets.Length; i++)
            {
                var (label, sel) = statsTargets[i];
                var values = list.Select(sel).ToList();

                double max = values.Max();
                double min = values.Min();
                double avg = values.Average();
                // 总体方差
                double variance = values.Sum(v => Math.Pow(v - avg, 2)) / values.Count;
                double uniformity = max != 0 ? (min / max) * 100.0 : 0;

                int idxMax = values.IndexOf(max);
                int idxMin = values.IndexOf(min);

                string nameMax = GetDisplayName(list, idxMax);
                string nameMin = GetDisplayName(list, idxMin);

                string line = string.Join(",",
                    EscapeCsv(label),
                    FormatDouble(max),
                    EscapeCsv(nameMax),
                    FormatDouble(min),
                    EscapeCsv(nameMin),
                    FormatDouble(avg),
                    FormatDouble(variance),
                    uniformity.ToString("F2", culture)
                );
                sb.AppendLine(line);
            }
        }

        private static string GetDisplayName(List<PoiResultCIExyuvData> list, int index)
        {
            if (index < 0 || index >= list.Count) return "";
            var it = list[index];
            return string.IsNullOrWhiteSpace(it.Name) ? $"P_{index}" : it.Name;
        }

        private static PoiResultCIExyuvData? FindCenterPoint(IList<PoiResultCIExyuvData> list)
        {
            if (list == null || list.Count == 0) return null;
            var centerByName = list.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.Name) &&
                o.Name.IndexOf("Center", StringComparison.OrdinalIgnoreCase) >= 0);
            if (centerByName != null) return centerByName;

            double avgX = list.Average(o => (double)(o.Point?.PixelX ?? 0));
            double avgY = list.Average(o => (double)(o.Point?.PixelY ?? 0));

            PoiResultCIExyuvData? closest = null;
            double bestDist2 = double.MaxValue;
            foreach (var it in list)
            {
                double dx = (it.Point?.PixelX ?? 0) - avgX;
                double dy = (it.Point?.PixelY ?? 0) - avgY;
                double d2 = dx * dx + dy * dy;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    closest = it;
                }
            }
            return closest;
        }

        private static double CalcMaxDeltaUv(IList<PoiResultCIExyuvData> list)
        {
            double maxD = 0;
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    double du = list[i].u - list[j].u;
                    double dv = list[i].v - list[j].v;
                    double d = Math.Sqrt(du * du + dv * dv);
                    if (d > maxD) maxD = d;
                }
            }
            return maxD;
        }

        private static string EscapeCsv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains('"')) s = s.Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return $"\"{s}\"";
            return s;
        }

        private static string FormatDouble(double d) => d.ToString("G", CultureInfo.InvariantCulture);
    }


    public class PoiResultCIExyuvData : PoiResultData, IViewResult
    {


        public double CCT { get { return _CCT; } set { _CCT = value; OnPropertyChanged(); } }
        private double _CCT;

        public double Wave { get { return _Wave; } set { _Wave = value; OnPropertyChanged(); } }
        private double _Wave;

        public double X { get { return _X; } set { _X = value; OnPropertyChanged(); } }
        private double _X;

        public double Y { get { return _Y; } set { _Y = value; OnPropertyChanged(); } }
        private double _Y;

        public double Z { get { return _Z; } set { _Z = value; OnPropertyChanged(); } }
        private double _Z;

        public double u { get { return _u; } set { _u = value; OnPropertyChanged(); } }
        private double _u;

        public double v { get { return _v; } set { _v = value; OnPropertyChanged(); } }
        private double _v;
        public double x { get { return _x; } set { _x = value; OnPropertyChanged(); } }
        private double _x;

        public double y { get { return _y; } set { _y = value; OnPropertyChanged(); } }
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
                //老板说，先改成>0试试
                X = pOIDataCIExyuv.X > 0 ? pOIDataCIExyuv.X : 0.0001;
                Y = pOIDataCIExyuv.Y > 0 ? pOIDataCIExyuv.Y : 0.0001;
                Z = pOIDataCIExyuv.Z > 0 ? pOIDataCIExyuv.Z : 0.0001;
                u = pOIDataCIExyuv.u > 0 ? pOIDataCIExyuv.u : 0.0001;
                v = pOIDataCIExyuv.v > 0 ? pOIDataCIExyuv.v : 0.0001;
                x = pOIDataCIExyuv.x > 0 ? pOIDataCIExyuv.x : 0.0001;
                y = pOIDataCIExyuv.y > 0 ? pOIDataCIExyuv.y : 0.0001;
            }
        }


    }
}
