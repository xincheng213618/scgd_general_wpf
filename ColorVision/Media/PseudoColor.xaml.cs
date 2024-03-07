using ColorVision.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Media
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
            this.MinData = mindata;
            this.MaxData = maxdata;
            MinMax = (maxdata - MinMax) / 255;
            InitializeComponent();

        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            dataGrid1.ItemsSource = PseudoValues;
            Genera();
        }

        private void button_Create_Click(object sender, RoutedEventArgs e)
        {
            Genera();
            this.Close();
        }



        private void RangeSlider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            RowDefinitionStart.Height = new GridLength((370.0 / 255.0) * (255 - RangeSlider1.ValueEnd));
            RowDefinitionEnd.Height = new GridLength((370.0 / 255.0) * RangeSlider1.ValueStart);
            //获取当前的滚动条的start位置以及end位置
        }

        public void Genera()
        {
            double minLua = RangeSlider1.ValueStart;
            double maxLua = RangeSlider1.ValueEnd;
            ColorMap.buildCustomMap(Int32.Parse(this.textBox.Value.ToString()), minLua / MinMax, maxLua / MinMax);
            //colormapNum = Int32.Parse(this.textBox_level.Name);
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
                OpenCvSharp.Mat cm = new OpenCvSharp.Mat(rows + 5, cols + 150, ColorMap.srcColor.Type(), OpenCvSharp.Scalar.All(255));
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
                PseudoValues.Reverse();
            }
        }

        private void textBox_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            Genera();
        }
    }
}

