#pragma warning disable CA1707
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public class PseudoValue : ViewModelBase
    {
        public string ValText { get => _ValText; set { _ValText = value; NotifyPropertyChanged(); } }
        private string _ValText;

        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _Color;
    }

    /// <summary>
    /// PseudoColor.xaml 的交互逻辑
    /// </summary>
    public partial class PseudoColor : Window
    {
        public ColorMap ColorMap { get; set; }
        double MinMax { get; set; } 
        double MinData { get; set; }
        double MaxData { get; set; }= 255;
        public ObservableCollection<PseudoValue> PseudoValues { get; set; }

        public PseudoColor(double mindata = 0, double maxdata = 255)
        {
            PseudoValues = new ObservableCollection<PseudoValue>();
            ColorMap = new ColorMap();
            MinData = mindata;
            MaxData = maxdata;
            MinMax = (maxdata - MinMax) / 255;
            InitializeComponent();
            this.ApplyCaption();

        }

        public ColormapTypes ColormapTypes { get; set; } = ColormapTypes.COLORMAP_JET;

        public static Dictionary<ColormapTypes, string> GetColormapDictionary()
        {
            var colormapDictionary = new Dictionary<ColormapTypes, string>
        {
            { ColormapTypes.COLORMAP_AUTUMN, "Assets/Colormaps/colorscale_autumn.jpg" },
            { ColormapTypes.COLORMAP_BONE, "Assets/Colormaps/colorscale_bone.jpg" },
            { ColormapTypes.COLORMAP_JET, "Assets/Colormaps/colorscale_jet.jpg" },
            { ColormapTypes.COLORMAP_WINTER, "Assets/Colormaps/colorscale_winter.jpg" },
            { ColormapTypes.COLORMAP_RAINBOW, "Assets/Colormaps/colorscale_rainbow.jpg" },
            { ColormapTypes.COLORMAP_OCEAN, "Assets/Colormaps/colorscale_ocean.jpg" },
            { ColormapTypes.COLORMAP_SUMMER, "Assets/Colormaps/colorscale_summer.jpg" },
            { ColormapTypes.COLORMAP_SPRING, "Assets/Colormaps/colorscale_spring.jpg" },
            { ColormapTypes.COLORMAP_COOL, "Assets/Colormaps/colorscale_cool.jpg" },
            { ColormapTypes.COLORMAP_HSV, "Assets/Colormaps/colorscale_hsv.jpg" },
            { ColormapTypes.COLORMAP_PINK, "Assets/Colormaps/colorscale_pink.jpg" },
            { ColormapTypes.COLORMAP_HOT, "Assets/Colormaps/colorscale_hot.jpg" },
            { ColormapTypes.COLORMAP_PARULA, "Assets/Colormaps/colorscale_parula.jpg" },
            { ColormapTypes.COLORMAP_MAGMA, "Assets/Colormaps/colorscale_magma.jpg" },
            { ColormapTypes.COLORMAP_INFERNO, "Assets/Colormaps/colorscale_inferno.jpg" },
            { ColormapTypes.COLORMAP_PLASMA, "Assets/Colormaps/colorscale_plasma.jpg" },
            { ColormapTypes.COLORMAP_VIRIDIS, "Assets/Colormaps/colorscale_viridis.jpg" },
            { ColormapTypes.COLORMAP_CIVIDIS, "Assets/Colormaps/colorscale_cividis.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT, "Assets/Colormaps/colorscale_twilight.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT_SHIFTED, "Assets/Colormaps/colorscale_twilight_shifted.jpg" },
            { ColormapTypes.COLORMAP_TURBO, "Assets/Colormaps/colorscale_turbo.jpg" },
            { ColormapTypes.COLORMAP_DEEPGREEN, "Assets/Colormaps/colorscale_deepgreen.jpg" }
        };

            return colormapDictionary;
        }

        private void ComColormapTypes_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ColormapTypes = GetColormapDictionary().First(x => x.Value == ComColormapTypes.SelectedValue.ToString()).Key;
            string valuepath = ComColormapTypes.SelectedValue.ToString();
            ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision;component/{valuepath}", UriKind.Relative));
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ComColormapTypes.ItemsSource = GetColormapDictionary();


            dataGrid1.ItemsSource = PseudoValues;
            Genera();
        }

        private void button_Create_Click(object sender, RoutedEventArgs e)
        {
            Genera();
            Close();
        }



        private void RangeSlider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            if (ActualWidth > 0)
            {
                RowDefinitionStart.Width = new GridLength(((ActualWidth - 100) / 255.0) * (255 - RangeSlider1.ValueEnd));
                RowDefinitionEnd.Width = new GridLength(((ActualWidth - 100) / 255.0) * RangeSlider1.ValueStart);
            }
            //获取当前的滚动条的start位置以及end位置
        }

        public void Genera()
        {
            double minLua = RangeSlider1.ValueStart;
            double maxLua = RangeSlider1.ValueEnd;
            ColorMap.buildCustomMap(Int32.Parse(textBox.Value.ToString()), minLua / MinMax, maxLua / MinMax);
            //colormapNum = Int32.Parse(this.textBox_level.DisPlayName);
            init();
            ColorMap.reMap();
        }

        //初始化伪彩色的这个表
        private void init()
        {
            PseudoValues.Clear();
            if (ColorMap != null)
            {
                int rows = ColorMap.srcColor.Rows;
                int cols = ColorMap.srcColor.Cols;
                OpenCvSharp.Mat cm = new(rows + 5, cols + 150, ColorMap.srcColor.Type(), OpenCvSharp.Scalar.All(255));
                OpenCvSharp.Mat cmRt = cm[new OpenCvSharp.Rect(0, 0, cols, rows)];
                ColorMap.srcColor.Clone().CopyTo(cmRt);
                cmRt = cm[new OpenCvSharp.Rect(cols, 0, 150, rows)];

                System.Drawing.Color[] clrMap = ColorMap.colorMap;
                double stepData = 0;
                double nowStep = MinData;
                for (int i = 0; i < clrMap.Length; i++)
                {
                    stepData = ColorMap.stepPer[i] * MinMax;
                    double nextStep = stepData + nowStep;
                    if (i == clrMap.Length - 1) //最后
                    {
                        nextStep = MaxData;
                    }

                    int colorcont = ColorMap.colorMap.Length - i  -1;

                    PseudoValues.Add(new PseudoValue() { ValText = $"{(int)nowStep}-{(int)nextStep}" , Color = new SolidColorBrush(Color.FromArgb(clrMap[colorcont].A, clrMap[colorcont].R, clrMap[colorcont].G, clrMap[colorcont].B))});
                    nowStep = nextStep;
                    cmRt.Line(0, ColorMap.colorMapIdx[i], 50, ColorMap.colorMapIdx[i], OpenCvSharp.Scalar.All(0));
                }
                _ = PseudoValues.Reverse();
            }
        }

        private void textBox_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            Genera();
        }


    }
}

