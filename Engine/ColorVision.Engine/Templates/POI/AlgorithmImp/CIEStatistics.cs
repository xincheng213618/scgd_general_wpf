using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    /// <summary>
    /// Statistics for CIE Y Data
    /// </summary>
    public class CIEYStatistics
    {
        public double CenterLuminance { get; set; }
        public double AverageLuminance { get; set; }
        public double MaxLuminance { get; set; }
        public double MinLuminance { get; set; }
        public double UniformityMinDivMax { get; set; }
        public double UniformityDiffDivAvg { get; set; }
        public double UniformityDiffDivMax { get; set; }
        public double StandardDeviation { get; set; }
        public double StandardDeviationPercent { get; set; }

        public static CIEYStatistics Calculate(ObservableCollection<PoiResultCIEYData> items)
        {
            var stats = new CIEYStatistics();
            
            if (items == null || items.Count == 0)
                return stats;

            var luminances = items.Select(o => o.Y).ToList();

            stats.AverageLuminance = luminances.Average();
            stats.MaxLuminance = luminances.Max();
            stats.MinLuminance = luminances.Min();
            stats.StandardDeviation = Math.Sqrt(luminances.Sum(v => Math.Pow(v - stats.AverageLuminance, 2)) / luminances.Count);

            var center = FindCenterPoint(items);
            stats.CenterLuminance = center?.Y ?? 0;

            stats.UniformityMinDivMax = stats.MaxLuminance != 0 ? stats.MinLuminance / stats.MaxLuminance * 100.0 : 0;
            stats.UniformityDiffDivAvg = stats.AverageLuminance != 0 ? (stats.MaxLuminance - stats.MinLuminance) / stats.AverageLuminance * 100.0 : 0;
            stats.UniformityDiffDivMax = stats.MaxLuminance != 0 ? (stats.MaxLuminance - stats.MinLuminance) / stats.MaxLuminance * 100.0 : 0;
            stats.StandardDeviationPercent = stats.AverageLuminance != 0 ? stats.StandardDeviation / stats.AverageLuminance * 100.0 : 0;

            return stats;
        }

        private static PoiResultCIEYData? FindCenterPoint(IList<PoiResultCIEYData> list)
        {
            if (list == null || list.Count == 0) return null;

            var centerByName = list.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.Name) &&
                o.Name.IndexOf("Center", StringComparison.OrdinalIgnoreCase) >= 0);
            if (centerByName != null) return centerByName;

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

        public override string ToString()
        {
            var culture = CultureInfo.InvariantCulture;
            return $"Center: {CenterLuminance.ToString(culture)} cd/m², " +
                   $"Avg: {AverageLuminance.ToString(culture)} cd/m², " +
                   $"Max: {MaxLuminance.ToString(culture)} cd/m², " +
                   $"Min: {MinLuminance.ToString(culture)} cd/m², " +
                   $"Uniformity(Min/Max): {UniformityMinDivMax.ToString("F2", culture)}%";
        }
    }

    /// <summary>
    /// Statistics for CIE xyuv Data
    /// </summary>
    public class CIExyuvStatistics
    {
        public double CenterLuminance { get; set; }
        public double AverageLuminance { get; set; }
        public double MaxLuminance { get; set; }
        public double MinLuminance { get; set; }
        public double UniformityMinDivMax { get; set; }
        public double UniformityDiffDivAvg { get; set; }
        public double UniformityDiffDivMax { get; set; }
        public double StandardDeviation { get; set; }
        public double StandardDeviationPercent { get; set; }
        public double ColorUniformityDeltaUv { get; set; }
        public double ColorUniformityDeltaX { get; set; }
        public double ColorUniformityDeltaY { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterU { get; set; }
        public double CenterV { get; set; }
        public double CenterCCT { get; set; }
        public double CenterWave { get; set; }
        public double DeltaWave { get; set; }

        public static CIExyuvStatistics Calculate(ObservableCollection<PoiResultCIExyuvData> items)
        {
            var stats = new CIExyuvStatistics();
            
            if (items == null || items.Count == 0)
                return stats;

            var luminances = items.Select(o => o.Y).ToList();

            stats.AverageLuminance = luminances.Average();
            stats.MaxLuminance = luminances.Max();
            stats.MinLuminance = luminances.Min();
            stats.StandardDeviation = SampleStandardDeviation(luminances);

            var center = FindCenterPoint(items);
            stats.CenterLuminance = center?.Y ?? 0;
            stats.CenterX = center?.x ?? 0;
            stats.CenterY = center?.y ?? 0;
            stats.CenterU = center?.u ?? 0;
            stats.CenterV = center?.v ?? 0;
            stats.CenterCCT = center?.CCT ?? 0;
            stats.CenterWave = center?.Wave ?? 0;

            double maxX = items.Max(o => o.x);
            double minX = items.Min(o => o.x);
            double maxY = items.Max(o => o.y);
            double minY = items.Min(o => o.y);

            stats.ColorUniformityDeltaUv = CalcMaxDeltaUv(items);
            stats.ColorUniformityDeltaX = maxX - minX;
            stats.ColorUniformityDeltaY = maxY - minY;

            double maxWave = items.Max(o => o.Wave);
            double minWave = items.Min(o => o.Wave);
            stats.DeltaWave = maxWave - minWave;

            stats.UniformityMinDivMax = stats.MaxLuminance != 0 ? stats.MinLuminance / stats.MaxLuminance * 100.0 : 0;
            stats.UniformityDiffDivAvg = stats.AverageLuminance != 0 ? (stats.MaxLuminance - stats.MinLuminance) / stats.AverageLuminance * 100.0 : 0;
            stats.UniformityDiffDivMax = stats.MaxLuminance != 0 ? (stats.MaxLuminance - stats.MinLuminance) / stats.MaxLuminance * 100.0 : 0;
            stats.StandardDeviationPercent = (stats.AverageLuminance != 0 && !double.IsNaN(stats.StandardDeviation)) ? stats.StandardDeviation / stats.AverageLuminance * 100.0 : double.NaN;

            return stats;
        }

        private static double SampleStandardDeviation(IList<double> data)
        {
            int n = data.Count;
            if (n <= 1) return double.NaN;
            double mean = data.Average();
            double sumSq = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = data[i] - mean;
                sumSq += d * d;
            }
            return Math.Sqrt(sumSq / (n - 1));
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

        public override string ToString()
        {
            var culture = CultureInfo.InvariantCulture;
            return $"Center: {CenterLuminance.ToString(culture)} cd/m², " +
                   $"Avg: {AverageLuminance.ToString(culture)} cd/m², " +
                   $"Max: {MaxLuminance.ToString(culture)} cd/m², " +
                   $"Min: {MinLuminance.ToString(culture)} cd/m², " +
                   $"Uniformity(Min/Max): {UniformityMinDivMax.ToString("F2", culture)}%";
        }
    }
}
