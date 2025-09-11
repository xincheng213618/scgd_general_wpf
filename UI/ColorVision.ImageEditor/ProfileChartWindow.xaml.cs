using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ProfileChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProfileChartWindow : Window
    {
        private List<double> _profileData;
        public ProfileChartWindow(List<double> profileData,string title)
        {
            _profileData = profileData;
            InitializeComponent();
            this.Title = title;
            
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var redValues = new List<double>(_profileData);

            double MaxY = redValues.Max();
            ProfileChart.XAxes = new Axis[]
{
                new Axis
                {
                    MaxLimit = redValues.Count,
                    MinLimit =0,
                    Labels = Enumerable.Range(0, redValues.Count).Select(x => x.ToString()).ToArray() // 确保显示0到255的每个标签
                }
};
            ProfileChart.YAxes = new Axis[]
            {
                new Axis(){
                    IsVisible =true ,
                    MaxLimit = MaxY ,
                    MinLimit =0,
                    Labeler = value => value.ToString("F0")
                }
            };

            ProfileChart.ZoomMode = ZoomAndPanMode.ZoomY | ZoomAndPanMode.PanY | ZoomAndPanMode.PanX; ;



            var Serieschannel1 = new LineSeries<double>
            {
                Values = redValues,
                Name = "Red",
                Fill = new SolidColorPaint(new SKColor(255, 0, 0, 60)),
                Stroke = new SolidColorPaint(new SKColor(255, 0, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };


            var SeriesCollection = new ISeries[] { Serieschannel1};
            ProfileChart.Series = SeriesCollection;
        }

        private void SaveChartButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveDataButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
