#pragma warning disable CA1854,CS8625
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR
{
    public partial class SfrSimplePlotControl : UserControl
    {
        private const double Epsilon = 1e-9;
        
        private double[] _frequencies;
        private double[] _sfrValues;
        private Dictionary<string, double[]> _multiChannelData;
        private string _queryChannel = "L"; // Default to L channel
        
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

        private static double[] TrimToLength(double[] values, int length)
        {
            if (values == null || length <= 0)
            {
                return Array.Empty<double>();
            }

            int copyLength = Math.Min(values.Length, length);
            if (copyLength == values.Length)
            {
                return values;
            }

            double[] trimmed = new double[copyLength];
            Array.Copy(values, trimmed, copyLength);
            return trimmed;
        }

        public void SetData(double[] frequencies, double[] sfrValues, string label = "SFR")
        {
            _multiChannelData = null;

            var plt = WpfPlot.Plot;
            plt.Clear();

            if (frequencies == null || sfrValues == null || frequencies.Length == 0 || sfrValues.Length == 0)
            {
                _frequencies = Array.Empty<double>();
                _sfrValues = Array.Empty<double>();
                WpfPlot.Refresh();
                return;
            }

            int n = Math.Min(frequencies.Length, sfrValues.Length);
            double[] freqData = TrimToLength(frequencies, n);
            double[] sfrData = TrimToLength(sfrValues, n);
            _frequencies = freqData;
            _sfrValues = sfrData;

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
            _queryChannel = "L";

            var plt = WpfPlot.Plot;
            plt.Clear();
            
            // Clear references
            _scatterR = null;
            _scatterG = null;
            _scatterB = null;
            _scatterL = null;

            if (frequencies == null || sfrR == null || sfrG == null || sfrB == null || sfrL == null ||
                frequencies.Length == 0 || sfrR.Length == 0 || sfrG.Length == 0 || sfrB.Length == 0 || sfrL.Length == 0)
            {
                _frequencies = Array.Empty<double>();
                _sfrValues = Array.Empty<double>();
                _multiChannelData = null;
                WpfPlot.Refresh();
                return;
            }

            int n = Math.Min(frequencies.Length, Math.Min(Math.Min(sfrR.Length, sfrG.Length), Math.Min(sfrB.Length, sfrL.Length)));
            double[] freqData = TrimToLength(frequencies, n);
            double[] sfrDataR = TrimToLength(sfrR, n);
            double[] sfrDataG = TrimToLength(sfrG, n);
            double[] sfrDataB = TrimToLength(sfrB, n);
            double[] sfrDataL = TrimToLength(sfrL, n);

            _frequencies = freqData;
            _sfrValues = sfrDataL;
            _multiChannelData = new Dictionary<string, double[]>
            {
                { "R", sfrDataR },
                { "G", sfrDataG },
                { "B", sfrDataB },
                { "L", sfrDataL }
            };

            // Add R channel - Red
            _scatterR = plt.Add.Scatter(freqData, sfrDataR);
            _scatterR.LegendText = "R";
            _scatterR.LineWidth = 2;
            _scatterR.MarkerSize = 0;
            _scatterR.Color = ScottPlot.Color.FromHex("#FF0000");

            // Add G channel - Green
            _scatterG = plt.Add.Scatter(freqData, sfrDataG);
            _scatterG.LegendText = "G";
            _scatterG.LineWidth = 2;
            _scatterG.MarkerSize = 0;
            _scatterG.Color = ScottPlot.Color.FromHex("#00FF00");

            // Add B channel - Blue
            _scatterB = plt.Add.Scatter(freqData, sfrDataB);
            _scatterB.LegendText = "B";
            _scatterB.LineWidth = 2;
            _scatterB.MarkerSize = 0;
            _scatterB.Color = ScottPlot.Color.FromHex("#0000FF");

            // Add L channel - Gray/Black
            _scatterL = plt.Add.Scatter(freqData, sfrDataL);
            _scatterL.LegendText = "L (Luminance)";
            _scatterL.LineWidth = 2.5f;
            _scatterL.MarkerSize = 0;
            _scatterL.Color = ScottPlot.Color.FromHex("#000000");

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

        /// <summary>
        /// Set the channel to use for query operations (MTF@Freq and Freq@MTF).
        /// </summary>
        /// <param name="channel">Channel name: "R", "G", "B", or "L"</param>
        public void SetQueryChannel(string channel)
        {
            _queryChannel = channel;
            
            if (_multiChannelData != null && _multiChannelData.ContainsKey(channel))
            {
                _sfrValues = _multiChannelData[channel];
            }
        }

        /// <summary>
        /// Get the current query channel name.
        /// </summary>
        public string GetQueryChannel()
        {
            return _queryChannel;
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
