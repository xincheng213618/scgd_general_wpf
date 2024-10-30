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
        int[] RedHistogram;
        int[] GreenHistogram;
        int[] BlueHistogram;
        int[] GrayHistogram; // 灰度直方图

        double? MaxY;

        private LineSeries<int> redSeries;
        private LineSeries<int> greenSeries;
        private LineSeries<int> blueSeries;
        private LineSeries<int> graySeries; // 灰度直方图
        public ISeries[] SeriesCollection { get; set; }
        public HistogramChartWindow(int[] redHistogram, int[] greenHistogram ,int[] blueHistogram)
        {
            //对数缩放
            RedHistogram = redHistogram;
            GreenHistogram = greenHistogram;
            BlueHistogram = blueHistogram;

            MaxY = (RedHistogram.Sum() + GreenHistogram.Sum() + BlueHistogram.Sum()) / 100;

            InitializeComponent();
            this.ApplyCaption();
            DrawHistograms(HistogramChart, RedHistogram, GreenHistogram, BlueHistogram);

        }

        public HistogramChartWindow(int[] graySeries)
        {
            GrayHistogram = graySeries;

            InitializeComponent();
            this.ApplyCaption();
            DrawHistograms(HistogramChart, GrayHistogram);

        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            HistogramChart.XAxes = new Axis[]
            {
                new Axis
                {
                    MaxLimit = 255,
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
            var grayValues = new List<int>(grayHistogram);

            graySeries = new LineSeries<int>
            {
                Values = grayValues,
                Name = "Gray",
                Fill = new SolidColorPaint(new SKColor(128, 128, 128, 100)), // 半透明灰色阴影
                Stroke = new SolidColorPaint(new SKColor(128, 128, 128)),
                LineSmoothness = 10,
                GeometrySize = 0
            };

            SeriesCollection = new ISeries[] { graySeries };
            chart.Series = SeriesCollection;

            // 复选框控制逻辑
            AddCheckBoxForChannel("Gray", graySeries); // 添加灰度通道的复选框
        }

        private void DrawHistograms(CartesianChart chart, int[] redHistogram, int[] greenHistogram, int[] blueHistogram)
        {
            var redValues = new List<int>(redHistogram);
            var greenValues = new List<int>(greenHistogram);
            var blueValues = new List<int>(blueHistogram);

            redSeries = new LineSeries<int>
            {
                Values = redValues,
                Name = "Red",
                Fill = new SolidColorPaint(new SKColor(255, 0, 0, 60)), // 半透明红色阴影
                Stroke = new SolidColorPaint(new SKColor(255, 0, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };

             greenSeries = new LineSeries<int>
            {
                Values = greenValues,
                Name = "Green",
                Fill = new SolidColorPaint(new SKColor(0, 255, 0, 80)), // 半透明绿色阴影
                Stroke = new SolidColorPaint(new SKColor(0, 255, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };

            blueSeries = new LineSeries<int>
            {
                Values = blueValues,
                Name = "Blue",
                Fill = new SolidColorPaint(new SKColor(0, 0, 255, 100)), // 半透明蓝色阴影
                Stroke = new SolidColorPaint(new SKColor(0, 0, 255)) ,
                LineSmoothness = 10,
                GeometrySize = 0,
            };
            SeriesCollection = new ISeries[] { redSeries, greenSeries, blueSeries };
            chart.Series = SeriesCollection;

            // 复选框控制逻辑
            AddCheckBoxForChannel("Red", redSeries);
            AddCheckBoxForChannel("Green", greenSeries);
            AddCheckBoxForChannel("Blue", blueSeries);
        }

        // 添加复选框并设置切换逻辑
        private void AddCheckBoxForChannel(string channelName, LineSeries<int> series)
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
