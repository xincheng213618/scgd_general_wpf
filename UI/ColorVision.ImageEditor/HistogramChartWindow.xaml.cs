using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Text.RegularExpressions;
using ColorVision.Themes;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using LiveChartsCore.Measure;

namespace ColorVision.ImageEditor
{

    public class HistogramChartConfig:ViewModelBase ,IConfig
    {
        public static HistogramChartConfig Instance => ConfigService.Instance.GetRequiredService<HistogramChartConfig>();

    }

    /// <summary>
    /// HistogramChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HistogramChartWindow : Window
    {
        int[] channe1;
        int[] GreenHistogram;
        int[] BlueHistogram;

        double MaxY;

        public bool IsLog { get; set; }
        private LineSeries<double> Serieschannel1;
        private LineSeries<double> greenSeries;
        private LineSeries<double> blueSeries;
        private LineSeries<double> graySeries; // 灰度直方图
        public ISeries[] SeriesCollection { get; set; }
        public HistogramChartWindow(int[] redHistogram, int[] greenHistogram ,int[] blueHistogram)
        {
            //对数缩放
            channe1 = redHistogram;
            GreenHistogram = greenHistogram;
            BlueHistogram = blueHistogram;

            MaxY = (channe1.Sum() + GreenHistogram.Sum() + BlueHistogram.Sum()) / 100;

            InitializeComponent();
            this.ApplyCaption();
            DrawHistograms(HistogramChart, channe1, GreenHistogram, BlueHistogram);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            IsLog = !IsLog;
            ToolStackPanel.Children.Clear();
            if (GreenHistogram != null && BlueHistogram != null)
            {
                DrawHistograms(HistogramChart, channe1, GreenHistogram, BlueHistogram);
            }
            else
            {
                DrawHistograms(HistogramChart, channe1);

            }

            if (IsLog)
            {
                HistogramChart.YAxes = new Axis[]
                {
                    new Axis()
                    {
                    IsVisible =true ,
                    MinLimit =0,
                    }
                };
            }
            else
            {
                HistogramChart.YAxes = new Axis[]
                {
                    new Axis()
                    {
                    IsVisible =true ,
                    MaxLimit = MaxY ,
                    MinLimit =0,
                    Labeler = value => Regex.Replace(value.ToString("E1"), @"E\+?0*(\d+)", "x10^$1"),
                    }
                };

            }
        }

        /// <summary>
        /// 对数组做对数变换
        /// </summary>
        /// <summary>
        /// 对数组做对数变换，返回 double[]
        /// </summary>
        private double[] ToLog(int[] hist)
        {
            return hist.Select(v => Math.Log(v + 1)).ToArray();
        }

        public HistogramChartWindow(int[] graySeries)
        {
            channe1 = graySeries;

            InitializeComponent();
            this.ApplyCaption();

            DrawHistograms(HistogramChart, channe1);

        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            HistogramChart.XAxes = new Axis[]
            {
                new Axis
                {
                    MaxLimit = channe1.Length,
                    MinLimit =0,
                    Labels = Enumerable.Range(0, 256).Select(x => x.ToString()).ToArray() // 确保显示0到255的每个标签
                }
            };
            HistogramChart.YAxes = new Axis[]
            {
                new Axis(){
                    IsVisible =true ,
                    MaxLimit = MaxY ,
                    MinLimit =0,
                    Labeler = value => Regex.Replace(value.ToString("E1"), @"E\+?0*(\d+)", "x10^$1"),
                }
            };

            HistogramChart.ZoomMode = ZoomAndPanMode.ZoomY | ZoomAndPanMode.PanY | ZoomAndPanMode.PanX; ;

        }
        private void DrawHistograms(CartesianChart chart, int[] grayHistogram)
        {
            double[] valuesToUse = IsLog ? ToLog(grayHistogram) : grayHistogram.Select(x => (double)x).ToArray();
            var grayValues = new List<double>(valuesToUse);

            graySeries = new LineSeries<double>
            {
                Values = grayValues,
                Name = "Gray",
                Fill = new SolidColorPaint(new SKColor(128, 128, 128, 100)),
                Stroke = new SolidColorPaint(new SKColor(128, 128, 128)),
                LineSmoothness = 10,
                GeometrySize = 0
            };

            SeriesCollection = new ISeries[] { graySeries };
            chart.Series = SeriesCollection;
            AddCheckBoxForChannel("Gray", graySeries);
        }


        private void DrawHistograms(CartesianChart chart, int[] redHistogram, int[] greenHistogram, int[] blueHistogram)
        {
            double[] redToUse = IsLog ? ToLog(redHistogram) : redHistogram.Select(x => (double)x).ToArray();
            double[] greenToUse = IsLog ? ToLog(greenHistogram) : greenHistogram.Select(x => (double)x).ToArray();
            double[] blueToUse = IsLog ? ToLog(blueHistogram) : blueHistogram.Select(x => (double)x).ToArray();

            var redValues = new List<double>(redToUse);
            var greenValues = new List<double>(greenToUse);
            var blueValues = new List<double>(blueToUse);

            Serieschannel1 = new LineSeries<double>
            {
                Values = redValues,
                Name = "Red",
                Fill = new SolidColorPaint(new SKColor(255, 0, 0, 60)),
                Stroke = new SolidColorPaint(new SKColor(255, 0, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };
            greenSeries = new LineSeries<double>
            {
                Values = greenValues,
                Name = "Green",
                Fill = new SolidColorPaint(new SKColor(0, 255, 0, 80)),
                Stroke = new SolidColorPaint(new SKColor(0, 255, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };
            blueSeries = new LineSeries<double>
            {
                Values = blueValues,
                Name = "Blue",
                Fill = new SolidColorPaint(new SKColor(0, 0, 255, 100)),
                Stroke = new SolidColorPaint(new SKColor(0, 0, 255)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };
            SeriesCollection = new ISeries[] { Serieschannel1, greenSeries, blueSeries };
            chart.Series = SeriesCollection;
            AddCheckBoxForChannel("Red", Serieschannel1);
            AddCheckBoxForChannel("Green", greenSeries);
            AddCheckBoxForChannel("Blue", blueSeries);
        }

        // 添加复选框并设置切换逻辑
        private void AddCheckBoxForChannel(string channelName, LineSeries<double> series)
        {
            CheckBox checkBox = new CheckBox
            {
                IsChecked = true,
                Content = channelName.Substring(0, 1),
                Margin = new Thickness(0, 0, 5, 0)
            };

            checkBox.Checked += (s, e) => series.IsVisible = true;
            checkBox.Unchecked += (s, e) => series.IsVisible = false;

            ToolStackPanel.Children.Add(checkBox);
        }


    }
}
