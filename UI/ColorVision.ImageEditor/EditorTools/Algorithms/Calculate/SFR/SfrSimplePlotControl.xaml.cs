using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR
{
    public partial class SfrSimplePlotControl : UserControl
    {
        private double[] _frequencies;
        private double[] _sfrValues;
        private Dictionary<string, double[]> _multiChannelData;
        
        // Store references to scatter plots for visibility control
        private Scatter _scatterR;
        private Scatter _scatterG;
        private Scatter _scatterB;
        private Scatter _scatterL;

        public SfrSimplePlotControl()
        {
            InitializeComponent();

            var plt = WpfPlot.Plot;
            plt.Title("SFR (MTF vs Frequency)");
            plt.Axes.Bottom.Label.Text = "Frequency (cycle/pixel)";
            plt.Axes.Left.Label.Text = "MTF";
            plt.ShowLegend();
            plt.Legend.Alignment = Alignment.UpperRight;
        }

        public void SetData(double[] frequencies, double[] sfrValues, string label = "SFR")
        {
            _frequencies = frequencies;
            _sfrValues = sfrValues;
            _multiChannelData = null;

            var plt = WpfPlot.Plot;
            plt.Clear();

            if (frequencies == null || sfrValues == null || frequencies.Length == 0 || sfrValues.Length == 0)
            {
                WpfPlot.Refresh();
                return;
            }

            int n = Math.Min(frequencies.Length, sfrValues.Length);
            double[] freqData = frequencies.Take(n).ToArray();
            double[] sfrData = sfrValues.Take(n).ToArray();

            var scatter = plt.Add.Scatter(freqData, sfrData);
            scatter.LegendText = label;
            scatter.LineWidth = 2;
            scatter.MarkerSize = 0;

            // Set default axis limits: MTF (0-1) and Freq (0-1)
            plt.Axes.SetLimits(0, 1, 0, 1);
            
            WpfPlot.Refresh();
        }

        public void SetMultiChannelData(double[] frequencies, 
            double[] sfrR, double[] sfrG, double[] sfrB, double[] sfrL)
        {
            _frequencies = frequencies;
            _sfrValues = sfrL; // Default to L channel for queries
            _multiChannelData = new Dictionary<string, double[]>
            {
                { "R", sfrR },
                { "G", sfrG },
                { "B", sfrB },
                { "L", sfrL }
            };

            var plt = WpfPlot.Plot;
            plt.Clear();
            
            // Clear references
            _scatterR = null;
            _scatterG = null;
            _scatterB = null;
            _scatterL = null;

            if (frequencies == null || frequencies.Length == 0)
            {
                WpfPlot.Refresh();
                return;
            }

            int n = frequencies.Length;
            double[] freqData = frequencies.Take(n).ToArray();

            // Add R channel - Red
            if (sfrR != null && sfrR.Length >= n)
            {
                _scatterR = plt.Add.Scatter(freqData, sfrR.Take(n).ToArray());
                _scatterR.LegendText = "R";
                _scatterR.LineWidth = 2;
                _scatterR.MarkerSize = 0;
                _scatterR.Color = ScottPlot.Color.FromHex("#FF0000");
            }

            // Add G channel - Green
            if (sfrG != null && sfrG.Length >= n)
            {
                _scatterG = plt.Add.Scatter(freqData, sfrG.Take(n).ToArray());
                _scatterG.LegendText = "G";
                _scatterG.LineWidth = 2;
                _scatterG.MarkerSize = 0;
                _scatterG.Color = ScottPlot.Color.FromHex("#00FF00");
            }

            // Add B channel - Blue
            if (sfrB != null && sfrB.Length >= n)
            {
                _scatterB = plt.Add.Scatter(freqData, sfrB.Take(n).ToArray());
                _scatterB.LegendText = "B";
                _scatterB.LineWidth = 2;
                _scatterB.MarkerSize = 0;
                _scatterB.Color = ScottPlot.Color.FromHex("#0000FF");
            }

            // Add L channel - Gray/Black
            if (sfrL != null && sfrL.Length >= n)
            {
                _scatterL = plt.Add.Scatter(freqData, sfrL.Take(n).ToArray());
                _scatterL.LegendText = "L (Luminance)";
                _scatterL.LineWidth = 2.5f;
                _scatterL.MarkerSize = 0;
                _scatterL.Color = ScottPlot.Color.FromHex("#000000");
            }

            // Set default axis limits: MTF (0-1) and Freq (0-1)
            plt.Axes.SetLimits(0, 1, 0, 1);
            
            WpfPlot.Refresh();
        }

        public void SetChannelVisibility(bool showR, bool showG, bool showB, bool showL)
        {
            if (_scatterR != null) _scatterR.IsVisible = showR;
            if (_scatterG != null) _scatterG.IsVisible = showG;
            if (_scatterB != null) _scatterB.IsVisible = showB;
            if (_scatterL != null) _scatterL.IsVisible = showL;
            
            WpfPlot.Refresh();
        }

        public (double[] frequencies, double[] sfrValues) GetData()
        {
            return (_frequencies, _sfrValues);
        }

        public Dictionary<string, double[]> GetMultiChannelData()
        {
            return _multiChannelData;
        }

        public bool TryEvaluateMtfAtFrequency(double freq, out double mtf)
        {
            mtf = double.NaN;
            if (_frequencies == null || _sfrValues == null || _frequencies.Length < 2)
                return false;

            return TryInterpolateY(_frequencies, _sfrValues, freq, out mtf);
        }

        public bool TryEvaluateFrequencyAtMtf(double mtf, out double freq)
        {
            freq = double.NaN;
            if (_frequencies == null || _sfrValues == null || _frequencies.Length < 2)
                return false;

            return TryInterpolateX(_frequencies, _sfrValues, mtf, out freq);
        }

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

            // Not in range, return closest endpoint
            int idxMin = Array.IndexOf(ys, ys.Min());
            int idxMax = Array.IndexOf(ys, ys.Max());
            if (yTarget <= ys[idxMin]) { x = xs[idxMin]; return true; }
            if (yTarget >= ys[idxMax]) { x = xs[idxMax]; return true; }

            return false;
        }
    }
}
