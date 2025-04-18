using ColorVision.Engine.Templates.SFR;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;

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
        private static readonly SKColor s_gray = new(195, 195, 195);
        private static readonly SKColor s_gray1 = new(160, 160, 160);
        private static readonly SKColor s_gray2 = new(90, 90, 90);
        private static readonly SKColor s_dark3 = new(60, 60, 60);

        private void Window_Initialized(object sender, EventArgs e)
        {
            MTFChart.XAxes = new Axis[]
            {
       new Axis
        {
            Name = "Pdfrequency",
            NamePaint = new SolidColorPaint(s_gray1),
            TextSize = 18,
            Padding = new Padding(5, 15, 5, 5),
            LabelsPaint = new SolidColorPaint(s_gray),
            SeparatorsPaint = new SolidColorPaint
            {
                Color = s_gray,
                StrokeThickness = 1,
                PathEffect = new DashEffect([3, 3])
            },
            SubseparatorsPaint = new SolidColorPaint
            {
                Color = s_gray2,
                StrokeThickness = 0.5f
            },
            SubseparatorsCount = 9,
            ZeroPaint = new SolidColorPaint
            {
                Color = s_gray1,
                StrokeThickness = 2
            },
            TicksPaint = new SolidColorPaint
            {
                Color = s_gray,
                StrokeThickness = 1.5f
            },
            SubticksPaint = new SolidColorPaint
            {
                Color = s_gray,
                StrokeThickness = 1
            }
        }
            };
            MTFChart.YAxes = new Axis[]
            {
        new Axis
        {
            Name = "PdomainSamplingData",
            NamePaint = new SolidColorPaint(s_gray1),
            TextSize = 18,
            Padding = new Padding(5, 0, 15, 0),
            LabelsPaint = new SolidColorPaint(s_gray),
            SeparatorsPaint = new SolidColorPaint
            {
                Color = s_gray,
                StrokeThickness = 1,
                PathEffect = new DashEffect([3, 3])
            },
            SubseparatorsPaint = new SolidColorPaint
            {
                Color = s_gray2,
                StrokeThickness = 0.5f
            },
            SubseparatorsCount = 9,
            ZeroPaint = new SolidColorPaint
            {
                Color = s_gray1,
                StrokeThickness = 2
            },
            TicksPaint = new SolidColorPaint
            {
                Color = s_gray,
                StrokeThickness = 1.5f
            },
            SubticksPaint = new SolidColorPaint
            {
                Color = s_gray,
                StrokeThickness = 1
            }
        }
            };
            MTFChart.DrawMarginFrame = new DrawMarginFrame()
            {
                Fill = new SolidColorPaint(s_dark3),
                Stroke = new SolidColorPaint
                {
                    Color = s_gray,
                    StrokeThickness = 1
                }
            };
            Render(0);
            MTFChart.ZoomMode = ZoomAndPanMode.ZoomY | ZoomAndPanMode.PanY | ZoomAndPanMode.PanX; ;
        }

        double InterpolateValue(float[] frequencies, float[] samplingData, double targetFrequency)
        {
            if (frequencies.Length != samplingData.Length)
            {
                throw new ArgumentException("频率和采样数据数组长度不匹配！");
            }

            if (targetFrequency <= frequencies[0])
            {
                return samplingData[0];
            }
            else if (targetFrequency >= frequencies[frequencies.Length])
            {
                return samplingData[frequencies.Length];
            }

            for (int i = 0; i < frequencies.Length; i++)
            {
                if (targetFrequency >= frequencies[i] && targetFrequency < frequencies[i + 1])
                {
                    // 线性插值
                    double t = (targetFrequency - frequencies[i]) / (frequencies[i + 1] - frequencies[i]);
                    return samplingData[i] + t * (samplingData[i + 1] - samplingData[i]);
                }
            }

            throw new ArgumentException("目标频率超出范围！");
        }



        public void Render(int index )
        {
            List<ISeries> SeriesCollection = new List<ISeries>();

            var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(AlgResultSFRModels[index].Pdfrequency);
            var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(AlgResultSFRModels[index].PdomainSamplingData);

            var points = new PointF[Pdfrequencys.Length];

            var series = new LineSeries<ObservablePoint>
            {
                Values = Fetch(),
                Stroke = new SolidColorPaint(new SKColor(33, 150, 243), 4),
                Fill = null,
                GeometrySize = 0
            };

            List<ObservablePoint> Fetch()
            {
                var list = new List<ObservablePoint>();
                for (int i = 0; i < 48; i++)
                {
                    list.Add(new()
                    {
                        X = Pdfrequencys[i],
                        Y = PdomainSamplingDatas[i]
                    });
                }
                return list;
            }


            SeriesCollection.Add(series);
            MTFChart.Series = SeriesCollection;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(AlgResultSFRModels[0].Pdfrequency);
            var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(AlgResultSFRModels[0].PdomainSamplingData);
            double a = InterpolateValue(Pdfrequencys, PdomainSamplingDatas, 0.5);
            Resultextbox.Text = a.ToString();
        }
    }  
}
