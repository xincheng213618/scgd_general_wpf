using ScottPlot;
using ScottPlot.Palettes;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.SFR2
{
    public enum Sfr2ShowType
    {
        All = -1,
        VerAverage = -2, // 垂直平均 (Top + Bottom) / 2
        HorAverage = -3, // 水平平均 (Left + Right) / 2
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3,
    }

    public partial class Sfr2PlotControl : UserControl
    {
        private SFR2ResultFile _data;
        private bool _showAll;
        private string _selectedPoi;
        private Sfr2ShowType _showType = Sfr2ShowType.All;

        // 为了插值稳定，不依赖内部 plottable 的数据结构，自己存储一份
        private readonly Dictionary<string, (double[] xs, double[] ys)> _seriesData = new();

        private readonly Dictionary<string, Scatter> _lines = new();

        public Sfr2PlotControl()
        {
            InitializeComponent();

            var plt = WpfPlot.Plot;
            plt.Title("SFR (MTF vs Frequency)");
            plt.Axes.Bottom.Label.Text = "Frequency (cycle/pixel)";
            plt.Axes.Left.Label.Text = "MTF";
            plt.ShowLegend();
            plt.Legend.Alignment = Alignment.UpperRight;
        }

        public void SetData(SFR2ResultFile data)
        {
            _data = data;
            AutoSelectFirstPoi();
            Redraw();
        }

        public void SetShowAll(bool showAll)
        {
            _showAll = showAll;
            Redraw();
        }

        public void SetShowType(Sfr2ShowType type)
        {
            _showType = type;
            Redraw();
        }

        public void SelectPoi(string poiName)
        {
            _selectedPoi = poiName;
            Redraw();
        }

        public IEnumerable<string> GetPoiNames()
        {
            if (_data?.result == null) yield break;
            foreach (var r in _data.result)
                yield return r.name;
        }

        // 线性插值：MTF@Frequency
        public bool TryEvaluateMtfAtFrequency(string seriesLabel, double freq, out double mtf)
        {
            mtf = double.NaN;
            if (!_seriesData.TryGetValue(seriesLabel, out var s)) return false;
            return TryInterpolateY(s.xs, s.ys, freq, out mtf);
        }

        // 线性插值：Frequency@MTF
        public bool TryEvaluateFrequencyAtMtf(string seriesLabel, double mtf, out double freq)
        {
            freq = double.NaN;
            if (!_seriesData.TryGetValue(seriesLabel, out var s)) return false;
            return TryInterpolateX(s.xs, s.ys, mtf, out freq);
        }

        private void AutoSelectFirstPoi()
        {
            if (_data?.result == null || _data.result.Count == 0) return;
            _selectedPoi = _data.result[0].name;
        }

        private void Redraw()
        {
            _seriesData.Clear();
            var plt = WpfPlot.Plot;
            plt.Clear();

            if (_data?.result == null || _data.result.Count == 0)
            {
                WpfPlot.Refresh();
                return;
            }

            var palette = new Category10();
            int colorIndex = 0;

            IEnumerable<SFR2ResultItem> poiEnumerable = _showAll
                ? _data.result
                : _data.result.Where(r => r.name == _selectedPoi);

            foreach (var poi in poiEnumerable)
            {
                foreach (var series in EnumerateSeries(poi, _showType))
                {
                    var color = palette.GetColor(colorIndex++);

                    // ScottPlot v5: Add.Scatter(...)
                    var sp = plt.Add.Scatter(series.xs, series.ys, color: color);

                    // 保存数据用于插值查询
                    _seriesData[series.label] = (series.xs, series.ys);
                }
            }

            plt.Axes.AutoScale();
            WpfPlot.Refresh();
        }

        private IEnumerable<(string label, double[] xs, double[] ys)> EnumerateSeries(SFR2ResultItem poi, Sfr2ShowType type)
        {
            if (poi?.data == null) yield break;

            if (type == Sfr2ShowType.All)
            {
                foreach (var c in poi.data.OrderBy(d => d.id))
                {
                    if (c?.frequency == null || c?.domainSamplingData == null) continue;
                    yield return ($"{poi.name}{GetChildSuffix(c.id)}",
                                  c.frequency.ToArray(),
                                  c.domainSamplingData.ToArray());
                }
                yield break;
            }

            if (type == Sfr2ShowType.HorAverage)
            {
                var left = poi.data.FirstOrDefault(d => d.id == 0);
                var right = poi.data.FirstOrDefault(d => d.id == 2);
                if (TryAverage(left, right, out var xs, out var ys))
                    yield return ($"{poi.name}{GetChildSuffix(-3)}", xs, ys);
                yield break;
            }

            if (type == Sfr2ShowType.VerAverage)
            {
                var top = poi.data.FirstOrDefault(d => d.id == 1);
                var bottom = poi.data.FirstOrDefault(d => d.id == 3);
                if (TryAverage(top, bottom, out var xs, out var ys))
                    yield return ($"{poi.name}{GetChildSuffix(-2)}", xs, ys);
                yield break;
            }

            int id = (int)type;
            var curve = poi.data.FirstOrDefault(d => d.id == id);
            if (curve?.frequency != null && curve?.domainSamplingData != null)
                yield return ($"{poi.name}{GetChildSuffix(id)}",
                              curve.frequency.ToArray(),
                              curve.domainSamplingData.ToArray());
        }

        private static string GetChildSuffix(int id) => id switch
        {
            -3 => "-horAverage",
            -2 => "-verAverage",
            0 => "-Left",
            1 => "-Top",
            2 => "-Right",
            3 => "-Bottom",
            _ => ""
        };

        // --- 插值与平均 ---

        private static bool TryInterpolateY(double[] xs, double[] ys, double xTarget, out double y)
        {
            y = double.NaN;
            if (xs == null || ys == null || xs.Length < 2 || xs.Length != ys.Length) return false;

            int n = xs.Length;
            if (xTarget <= xs[0]) { y = ys[0]; return true; }
            if (xTarget >= xs[n - 1]) { y = ys[n - 1]; return true; }

            int i = Array.BinarySearch(xs, xTarget);
            if (i >= 0)
            {
                y = ys[i];
                return true;
            }
            i = ~i;
            int i0 = Math.Max(0, i - 1);
            int i1 = Math.Min(n - 1, i);

            double x0 = xs[i0], x1 = xs[i1];
            double y0 = ys[i0], y1 = ys[i1];
            if (x1 == x0) { y = y0; return true; }

            double t = (xTarget - x0) / (x1 - x0);
            y = y0 + t * (y1 - y0);
            return true;
        }

        private static bool TryInterpolateX(double[] xs, double[] ys, double yTarget, out double x)
        {
            x = double.NaN;
            if (xs == null || ys == null || xs.Length < 2 || xs.Length != ys.Length) return false;

            for (int i = 0; i < ys.Length - 1; i++)
            {
                double y0 = ys[i], y1 = ys[i + 1];
                double x0 = xs[i], x1 = xs[i + 1];

                if (yTarget >= Math.Min(y0, y1) && yTarget <= Math.Max(y0, y1))
                {
                    if (y1 == y0) { x = x0; return true; }
                    double t = (yTarget - y0) / (y1 - y0);
                    x = x0 + t * (x1 - x0);
                    return true;
                }
            }

            // 不在范围内，返回最近端点
            int idxMin = Array.IndexOf(ys, ys.Min());
            int idxMax = Array.IndexOf(ys, ys.Max());
            if (yTarget <= ys[idxMin]) { x = xs[idxMin]; return true; }
            if (yTarget >= ys[idxMax]) { x = xs[idxMax]; return true; }

            return false;
        }

        private static bool TryAverage(SFR2Curve a, SFR2Curve b, out double[] xs, out double[] ys)
        {
            xs = null;
            ys = null;
            if (a == null || b == null) return false;
            if (a.frequency == null || b.frequency == null) return false;
            if (a.domainSamplingData == null || b.domainSamplingData == null) return false;

            int n = Math.Min(a.frequency.Count, b.frequency.Count);
            n = Math.Min(n, Math.Min(a.domainSamplingData.Count, b.domainSamplingData.Count));
            if (n < 2) return false;

            xs = new double[n];
            ys = new double[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = a.frequency[i];
                ys[i] = 0.5 * (a.domainSamplingData[i] + b.domainSamplingData[i]);
            }
            return true;
        }
    }
}