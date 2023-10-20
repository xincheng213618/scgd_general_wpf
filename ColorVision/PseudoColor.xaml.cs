using ColorVision.MVVM;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.ApplicationServices;
using NPOI.Util;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static ColorVision.PseudoColor;

namespace ColorVision
{
    public class DataGridValue : ViewModelBase
    {
        public string ValText { get => _ValText; set { _ValText = value; NotifyPropertyChanged(); } }
        private string _ValText;

        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _Color;
    }


    /// <summary>
    /// PseudoColor.xaml 的交互逻辑
    /// </summary>
    public partial class PseudoColor : System.Windows.Controls.UserControl
    {
        public ColorMap colorMap { get; set; }
        double min_max = 0;
        double mindata = 0;
        double maxdata = 255;
        public ObservableCollection<DataGridValue> DataGridValues { get; set; }

        public PseudoColor(double mindata = 0, double maxdata = 255)
        {
            InitializeComponent();
            colorMap = new ColorMap("colormap.png");
            this.mindata = mindata;
            this.maxdata = maxdata;
            min_max = (maxdata - min_max) / 255;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataGridValues = new ObservableCollection<DataGridValue>();
            dataGrid1.ItemsSource = DataGridValues;
        }



        //根据分级生成伪彩色
        private void button_Create_Click(object sender, RoutedEventArgs e)
        {
            //DataGridValues.Clear();

            //for (int i = 0; i < 10; i++)
            //{
            //    DataGridValues.Add(new DataGridValue() { Color = new SolidColorBrush(Colors.Aqua), ValText = "i" });
            //}


            //return;

            double minLua = 0;
            double maxLua = 0;
            minLua = RangeSlider1.ValueStart;
            maxLua = RangeSlider1.ValueEnd;
            //if (!double.TryParse(this.tb_Dnlimit.Text, out minLua))
            //{
            //    MessageBox.Show("错误的下限格式，请输入数值");
            //    return;
            //}
            //else if (minLua < mindata)
            //{
            //    MessageBox.Show("指定下限不能低于图像的下限:" + mindata.ToString() + "，请重新输入数值");
            //    return;
            //}
            //if (!double.TryParse(this.tb_Uplimit.Text, out maxLua))
            //{
            //    MessageBox.Show("错误的上限格式，请输入数值");
            //    return;
            //}
            //else if (maxLua > maxdata)
            //{
            //    MessageBox.Show("指定上限不能高于图像的上限:" + maxdata.ToString() + "，请重新输入数值");
            //    return;
            //}
            //if (double.Parse(this.tb_Dnlimit.Text) >= double.Parse(this.tb_Uplimit.Text))
            //{
            //    MessageBox.Show("下限不能>=上限，请重新输入");
            //    return;
            //}
            //if (Int32.Parse(this.tb_Dnlimit.Text)+ (100- Int32.Parse(this.tb_Uplimit.Text)) >=100)
            //{
            //    MessageBox.Show("输入的上限与下限之和>=100，请重新输入");
            //    return;
            //}
            colorMap.buildCustomMap(Int32.Parse(this.textBox.Text), minLua / min_max, maxLua / min_max);
            //colormapNum = Int32.Parse(this.textBox_level.Text);
            init();
            colorMap.reMap();
            //trackBar1.Value = (int)(maxLua / min_max);
            //trackBar2.Value = (int)(minLua / min_max);
            //formMain.OnDraw();
        }

        private void RangeSlider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            RowDefinitionStart.Height = new GridLength((370.0 / 255.0) * (255 - RangeSlider1.ValueEnd));
            RowDefinitionEnd.Height = new GridLength((370.0 / 255.0) * RangeSlider1.ValueStart);
            //获取当前的滚动条的start位置以及end位置

        }

        //初始化伪彩色的这个表
        private void init()
        {
            DataGridValues.Clear();
            if (colorMap != null)
            {
                int rows = colorMap.srcColor.Rows;
                int cols = colorMap.srcColor.Cols;
                Mat cm = new Mat(rows + 5, cols + 150, colorMap.srcColor.Type(), Scalar.All(255));
                Mat cmRt = cm[new OpenCvSharp.Rect(0, 0, cols, rows)];
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
                    DataGridValues.Add(new DataGridValue() { Color = new SolidColorBrush(System.Windows.Media.Color.FromArgb(clrMap[i].A, clrMap[i].R, clrMap[i].G, clrMap[i].B)), ValText = valText });
                    //this.dataGridView1.Rows[clrMap.Length - 1 - i].Cells[0].Value = valText;
                    //this.dataGridView1.Rows[clrMap.Length - 1 - i].Cells[1].Value = clrMap[i];

                    cmRt.Line(0, colorMap.colorMapIdx[i], 50, colorMap.colorMapIdx[i], Scalar.All(0));
                    valText = String.Format("{0}", i + 1);
                    if (i == 0)
                        cmRt.PutText(valText, new OpenCvSharp.Point(60, colorMap.colorMapIdx[i] + 20), HersheyFonts.HersheyDuplex, 1, Scalar.All(0));
                    else
                        cmRt.PutText(valText, new OpenCvSharp.Point(60, colorMap.colorMapIdx[i]), HersheyFonts.HersheyDuplex, 1, Scalar.All(0));
                }
                //for (int i = this.dataGridView1.Rows.Count - 1; i >= 0; i--)
                //{
                //    DataGridViewRow dataGridViewRow = this.dataGridView1.Rows[i];

                //}

                //this.pictureBox1.Image = cm.ToBitmap();
            }
        }
    }
}

