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
        private const double Epsilon = 1e-9;
        
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

        /// <summary>
        /// Find MTF value at a given frequency using linear interpolation.
        /// </summary>
        public double FindMtfAtFreq(double targetFreq)
        {
            if (_frequencies == null || _sfrValues == null || _frequencies.Length != _sfrValues.Length || _sfrValues.Length < 2)
                return double.NaN;

            // Find the bracket
            for (int i = 0; i < _frequencies.Length - 1; i++)
            {
                double x1 = _frequencies[i];
                double x2 = _frequencies[i + 1];

                if (targetFreq >= x1 && targetFreq <= x2)
                {
                    double y1 = _sfrValues[i];
                    double y2 = _sfrValues[i + 1];

                    // Linear interpolation
                    if (Math.Abs(x2 - x1) < Epsilon)
                        return y1;
                    
                    return y1 + (targetFreq - x1) * (y2 - y1) / (x2 - x1);
                }
            }

            return double.NaN;
        }

        /// <summary>
        /// Find frequency at a given MTF threshold.
        /// Implementation based on find_freq_at_threshold in slanted.cpp
        /// </summary>
        public double FindFreqAtThreshold(double threshold)
        {
            if (_frequencies == null || _sfrValues == null || _frequencies.Length != _sfrValues.Length || _sfrValues.Length == 0)
                return 0.0;

            // Find the first pair of points that bracket the threshold
            // We need to find where y1 >= threshold && y2 < threshold
            for (int i = 0; i < _sfrValues.Length - 1; i++)
            {
                double y1 = _sfrValues[i];
                double y2 = _sfrValues[i + 1];

                if (y1 >= threshold && y2 < threshold)
                {
                    // Get the corresponding bracketing values for x (frequency)
                    double x1 = _frequencies[i];
                    double x2 = _frequencies[i + 1];

                    // Perform linear interpolation
                    if (Math.Abs(y2 - y1) < Epsilon)
                    {
                        return x1;
                    }
                    return x1 + (threshold - y1) * (x2 - x1) / (y2 - y1);
                }
            }

            return 0.0;
        }
    }
}
