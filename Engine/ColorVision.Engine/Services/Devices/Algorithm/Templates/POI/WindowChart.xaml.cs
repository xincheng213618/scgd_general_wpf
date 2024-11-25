#pragma warning disable CS8604

using Microsoft.Win32;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// WindowChart.xaml 的交互逻辑
    /// </summary>
    public partial class WindowChart : Window
    {
        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvData { get; set; } = new ObservableCollection<PoiResultCIExyuvData>();

        public WindowChart(ObservableCollection<PoiResultCIExyuvData> poiResultCIExyuvData)
        {
            PoiResultCIExyuvData = poiResultCIExyuvData;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            wpfplot1.Plot.Title("图表");
            wpfplot1.Plot.XLabel("点");
            wpfplot1.Plot.YLabel("U");
            wpfplot1.Plot.Clear();
            wpfplot1.Plot.Axes.SetLimitsX(0, PoiResultCIExyuvData.Count);
            wpfplot1.Plot.Axes.SetLimitsY(0, 255);
            wpfplot1.Plot.Axes.Bottom.MinimumSize = 0;
            wpfplot1.Plot.Axes.Bottom.MaximumSize = 1000;
            wpfplot1.Plot.Axes.Left.MinimumSize = 0;
            wpfplot1.Plot.Axes.Left.MaximumSize = 255;

            double[] x = new double[PoiResultCIExyuvData.Count];
            double[] y = new double[PoiResultCIExyuvData.Count];
            Random rd = new();
            for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
            {
                x[i] = i;
                y[i] = PoiResultCIExyuvData[i].X;
            }
            scatterPlot = new Scatter(new ScatterSourceDoubleArray(x, y))
            {
                Color = Color.FromColor( System.Drawing.Color.DarkGoldenrod),
                LineWidth = 1,
                MarkerSize = 1,
                MarkerShape = MarkerShape.None,
            };
            wpfplot1.Plot.PlottableList.Add(scatterPlot);
            wpfplot1.Refresh();
        }
        Scatter? scatterPlot;
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && wpfplot1!=null && comboBox.SelectedItem is  ComboBoxItem comboBoxItem)
            {
                wpfplot1.Plot.YLabel(comboBoxItem.Content.ToString());
                wpfplot1.Plot.Remove(scatterPlot);
                double[] x = new double[PoiResultCIExyuvData.Count];
                double[] y = new double[PoiResultCIExyuvData.Count];
                switch (comboBoxItem.Content.ToString())
                {
                    case "X":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].X;
                        }
                        break;
                    case "Y":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].Y;
                        }
                        break;
                    case "Z":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].Z;
                        }
                        break;
                    case "u":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].u;
                        }
                        break;
                    case "v":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].v;
                        }
                        break;
                    case "x":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].x;
                        }
                        break;
                    case "y":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].y;
                        }
                        break;
                    case "CCT":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].CCT;
                        }
                        break;
                    case "Wave":
                        for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
                        {
                            x[i] = i;
                            y[i] = PoiResultCIExyuvData[i].Wave;
                        }
                        break;
                    default:
                        break;
                }
                scatterPlot = new Scatter(new ScatterSourceDoubleArray(x, y))
                {
                    Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod),
                    LineWidth = 1,
                    MarkerSize = 1,
                    MarkerShape = MarkerShape.None,
                };
                wpfplot1.Plot.PlottableList.Add(scatterPlot);
                wpfplot1.Refresh();
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Files|*.png|JPEG Files|*.jpg|BMP Files|*.bmp";  // 可以设置保存的格式
            saveFileDialog.Title = "Save Plot Image";

            // 如果用户选择了保存路径
            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;  // 获取文件路径
                wpfplot1.Plot.Save(filePath,400, 300);  // 保存图像
            }
        }
    }
}
