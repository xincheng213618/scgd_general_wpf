using ColorVision.Engine.Templates.SFR;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Newtonsoft.Json;
using NPOI.OpenXmlFormats.Dml.Chart;
using SkiaSharp;
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

namespace ColorVision.Engine.Templates.ARVR.SFR
{
    /// <summary>
    /// WindowSFR.xaml 的交互逻辑
    /// </summary>
    public partial class WindowSFR : Window
    {

        public List<AlgResultSFRModel> AlgResultSFRModels { get; set; }

        public WindowSFR(List<AlgResultSFRModel> algResultSFRModels)
        {
            AlgResultSFRModels = algResultSFRModels;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MTFChart.XAxes = new Axis[]
            {
                new Axis
                {
                    MaxLimit =1,
                    MinLimit =0,
                }
            };
            MTFChart.YAxes = new Axis[]
            {
                new Axis(){
                    IsVisible =true ,
                    MaxLimit = 100 ,
                    MinLimit =0,
                }
            };
            MTFChart.ZoomMode = ZoomAndPanMode.ZoomY | ZoomAndPanMode.PanY | ZoomAndPanMode.PanX; ;
            List<ISeries> SeriesCollection = new List<ISeries>();

            foreach (var item in AlgResultSFRModels)
            {
                var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                var blueSeries = new ScatterSeries<float>
                {
                    Values = PdomainSamplingDatas,
                    Name = "Blue",
                    Fill = new SolidColorPaint(new SKColor(0, 0, 255, 100)), // 半透明蓝色阴影
                    Stroke = new SolidColorPaint(new SKColor(0, 0, 255)),
                    MinGeometrySize = 2,
                    GeometrySize = 0,
                };
                SeriesCollection.Add(blueSeries);
            }

            MTFChart.Series = SeriesCollection;
        }
    }
}
