using ColorVision.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Media
{
    public class DataGridValue : ViewModelBase
    {
        public string ValText { get => _ValText; set { _ValText = value; NotifyPropertyChanged(); } }
        private string _ValText;

        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _Color;
    }


    public class PseudoColorConfig:ViewModelBase
    {
        public PseudoColorConfig() { }

        public double Max { get; set; }

        public double Min { get; set; }


    }



    /// <summary>
    /// PseudoColor.xaml 的交互逻辑
    /// </summary>
    public partial class PseudoColor : Window
    {
        public ColorMap colorMap { get; set; }
        double min_max { get; set; } 
        double mindata { get; set; }
        double maxdata { get; set; }= 255;
        public ObservableCollection<DataGridValue> DataGridValues { get; set; }

        public PseudoColor(double mindata = 0, double maxdata = 255)
        {
            DataGridValues = new ObservableCollection<DataGridValue>();
            colorMap = new ColorMap();
            this.mindata = mindata;
            this.maxdata = maxdata;
            min_max = (maxdata - min_max) / 255;
            InitializeComponent();

        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            dataGrid1.ItemsSource = DataGridValues;
            Genera();
        }



        //根据分级生成伪彩色
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
            colorMap.buildCustomMap(Int32.Parse(this.textBox.Value.ToString()), minLua / min_max, maxLua / min_max);
            //colormapNum = Int32.Parse(this.textBox_level.Name);
            init();
            colorMap.reMap();
        }

        //初始化伪彩色的这个表
        private void init()
        {
            DataGridValues.Clear();
            if (colorMap != null)
            {
                int rows = colorMap.srcColor.Rows;
                int cols = colorMap.srcColor.Cols;
                OpenCvSharp.Mat cm = new OpenCvSharp.Mat(rows + 5, cols + 150, colorMap.srcColor.Type(), OpenCvSharp.Scalar.All(255));
                OpenCvSharp.Mat cmRt = cm[new OpenCvSharp.Rect(0, 0, cols, rows)];
                colorMap.srcColor.Clone().CopyTo(cmRt);
                cmRt = cm[new OpenCvSharp.Rect(cols, 0, 150, rows)];

                System.Drawing.Color[] clrMap = colorMap.colorMap;
                //for (int i = 0; i < clrMap.Length; i++)
                //{
                //    DataGridValues.Add(new DataGridValue() { Color = new SolidColorBrush(Colors.Aqua), ValText = "i" });
                //    //this.dataGridView1.Rows.Add();
                //}
                double stepData = 0;
                double nowStep = mindata;
                for (int i = 0; i < clrMap.Length; i++)
                {
                    stepData = colorMap.stepPer[i] * min_max;
                    double nextStep = stepData + nowStep;
                    if (i == clrMap.Length - 1) //最后
                    {
                        nextStep = maxdata;
                    }
                    String valText = String.Format("{0}-{1}", (int)nowStep, (int)nextStep);
                    nowStep = nextStep;
                    //int idx = this.dataGridView1.Rows.Add();
                    DataGridValues.Add(new DataGridValue() { Color = new SolidColorBrush(Color.FromArgb(clrMap[i].A, clrMap[i].R, clrMap[i].G, clrMap[i].B)), ValText = valText });
                    //this.dataGridView1.Rows[clrMap.Length - 1 - i].Cells[0].Value = valText;
                    //this.dataGridView1.Rows[clrMap.Length - 1 - i].Cells[1].Value = clrMap[i];

                    cmRt.Line(0, colorMap.colorMapIdx[i], 50, colorMap.colorMapIdx[i], OpenCvSharp.Scalar.All(0));
                    valText = String.Format("{0}", i + 1);
                    if (i == 0)
                        cmRt.PutText(valText, new OpenCvSharp.Point(60, colorMap.colorMapIdx[i] + 20), OpenCvSharp.HersheyFonts.HersheyDuplex, 1, OpenCvSharp.Scalar.All(0));
                    else
                        cmRt.PutText(valText, new OpenCvSharp.Point(60, colorMap.colorMapIdx[i]), OpenCvSharp.HersheyFonts.HersheyDuplex, 1, OpenCvSharp.Scalar.All(0));
                }
                //for (int i = this.dataGridView1.Rows.Count - 1; i >= 0; i--)
                //{
                //    DataGridViewRow dataGridViewRow = this.dataGridView1.Rows[i];

                //}

                //this.pictureBox1.Image = cm.ToBitmap();
            }
        }

        private void textBox_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            Genera();
        }
    }
}

