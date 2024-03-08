using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using System.Collections.ObjectModel;

namespace ColorVision.Services.Devices.Algorithm.Views
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
            wpfplot1.Plot.SetAxisLimitsX(0, PoiResultCIExyuvData.Count);
            wpfplot1.Plot.SetAxisLimitsY(0, 255);
            wpfplot1.Plot.XAxis.SetBoundary(0, 1000);
            wpfplot1.Plot.YAxis.SetBoundary(0, 255);


            double[] x = new double[PoiResultCIExyuvData.Count];
            double[] y = new double[PoiResultCIExyuvData.Count];
            Random rd = new Random();
            for (int i = 0; i < PoiResultCIExyuvData.Count; i++)
            {
                x[i] = i;
                y[i] = PoiResultCIExyuvData[i].X;
            }
            scatterPlot = new ScatterPlot(x, y)
            {
                Color = Color.DarkGoldenrod,
                LineWidth = 1,
                MarkerSize = 1,
                Label = null,
                MarkerShape = MarkerShape.none,
                LineStyle = LineStyle.Solid
            };
            wpfplot1.Plot.Add(scatterPlot);
            wpfplot1.Refresh();
        }
        ScatterPlot? scatterPlot;
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
                scatterPlot = new ScatterPlot(x, y)
                {
                    Color = Color.DarkGoldenrod,
                    LineWidth = 1,
                    MarkerSize = 1,
                    Label = null,
                    MarkerShape = MarkerShape.none,
                    LineStyle = LineStyle.Solid
                };
                wpfplot1.Plot.Add(scatterPlot);
                wpfplot1.Refresh();
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            wpfplot1.SaveAsImage();
        }
    }
}
