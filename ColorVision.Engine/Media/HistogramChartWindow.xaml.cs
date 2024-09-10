using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Text.RegularExpressions;
using NPOI.SS.Formula.Functions;

namespace ColorVision.Engine.Media
{
    /// <summary>
    /// HistogramChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HistogramChartWindow : Window
    {
        int[] RedHistogram;
        int[] GreenHistogram;
        int[] BlueHistogram;

        double MaxY = 0;
        public HistogramChartWindow(int[] redHistogram, int[] greenHistogram ,int[] blueHistogram)
        {
            //对数缩放
            RedHistogram = LogScale(redHistogram);
            GreenHistogram = LogScale(greenHistogram);
            BlueHistogram = LogScale(blueHistogram);

            MaxY = (RedHistogram.Sum() + GreenHistogram.Sum() + BlueHistogram.Sum()) / 100;

            InitializeComponent();
        }
        // 对数缩放函数
        int[] LogScale(int[] data)
        {
            return data.Select(v => (int)v).ToArray();   
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            DrawHistograms(HistogramChart, RedHistogram, GreenHistogram, BlueHistogram);
        }

        private void DrawHistograms(CartesianChart chart, int[] redHistogram, int[] greenHistogram, int[] blueHistogram)
        {
            var redValues = new List<int>(redHistogram);
            var greenValues = new List<int>(greenHistogram);
            var blueValues = new List<int>(blueHistogram);

            var redSeries = new LineSeries<int>
            {
                Values = redValues,
                Name = "Red",
                Fill = new SolidColorPaint(new SKColor(255, 0, 0, 60)), // 半透明红色阴影
                Stroke = new SolidColorPaint(new SKColor(255, 0, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };

            var greenSeries = new LineSeries<int>
            {
                Values = greenValues,
                Name = "Green",
                Fill = new SolidColorPaint(new SKColor(0, 255, 0, 80)), // 半透明绿色阴影
                Stroke = new SolidColorPaint(new SKColor(0, 255, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };

            var blueSeries = new LineSeries<int>
            {
                Values = blueValues,
                Name = "Blue",
                Fill = new SolidColorPaint(new SKColor(0, 0, 255, 100)), // 半透明蓝色阴影
                Stroke = new SolidColorPaint(new SKColor(0, 0, 255)) ,
                LineSmoothness = 10,
                GeometrySize = 0,
            };

            chart.Series = new ISeries[] { redSeries, greenSeries, blueSeries };
            chart.XAxes = new Axis[]
            {
                new Axis
                {
                    CrosshairLabelsBackground = SKColors.DarkOrange.AsLvcColor(),
                    CrosshairLabelsPaint = new SolidColorPaint(SKColors.DarkRed, 1),
                    CrosshairPaint = new SolidColorPaint(SKColors.DarkOrange, 1),
                    MaxLimit =255,
                    MinLimit =0
                   
                }
            };
            chart.YAxes = new Axis[]
            {
                new Axis(){
                    IsVisible =true ,
                    MaxLimit = MaxY ,
                    MinLimit =0,
                    Labeler = value => Regex.Replace(value.ToString("E1"), @"E\+?0*(\d+)", " x10^$1"),
                }
            };

        }

    }
}
