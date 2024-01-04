using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using NPOI.XWPF.UserModel;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using ColorVision.Device.Spectrum.Views;
using static cvColorVision.GCSDLL;
using Newtonsoft.Json;
using System.Linq;
using ColorVision.Util;
using ColorVision.Sort;
using ColorVision.Services.Algorithm;
using MQTTMessageLib.Algorithm;
using System.Windows.Documents;

namespace ColorVision.Device.Spectrum
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSpectrum : UserControl,IView
    {
        private ResultService spectumResult = new ResultService();
        public ObservableCollection<ViewResultSpectrum> ViewResultSpectrums { get; set; } = new ObservableCollection<ViewResultSpectrum>();

        public View View { get; set; }

        public ViewSpectrum()
        {
            InitializeComponent();
        }

        static int ResultNum;
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new TextBox() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            this.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };
            View = new View();


            listView1.ItemsSource = ViewResultSpectrums;



            wpfplot1.Plot.Title("相对光谱曲线");
            wpfplot1.Plot.XLabel("波长[nm]");
            wpfplot1.Plot.YLabel("相对光谱");
            wpfplot1.Plot.Clear();
            wpfplot1.Plot.SetAxisLimitsX(380, 810);
            wpfplot1.Plot.SetAxisLimitsY(0, 1);
            wpfplot1.Plot.XAxis.SetBoundary(370, 850);
            wpfplot1.Plot.YAxis.SetBoundary(0, 1);

            listView1.Visibility = Visibility.Collapsed;
            listView2.Visibility = Visibility.Collapsed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show("您需要先选择数据");
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            CsvWriter.WriteToCsv(ViewResultSpectrums[listView1.SelectedIndex], dialog.FileName);
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResultSpectrums[listview.SelectedIndex].SpectralDatas;
            }
        }

        bool MulComparison;
        ScatterPlot? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (ScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.DarkGoldenrod;
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = ScatterPlots[listView1.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.Red;
                    wpfplot1.Plot.Add(LastMulSelectComparsion);

                }
                else
                {
                    var temp = ScatterPlots[listView1.SelectedIndex];
                    temp.Color = Color.DarkGoldenrod;
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot1.Plot.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot1.Refresh();
        }

        bool First;

        public void SpectrumDrawPlot(SpectumData data)
        {
            if (!First)
            {
                listView1.Visibility = Visibility.Visible;
                First = true;
            }

            ColorParam colorParam = data.Data;

            ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(colorParam);

            viewResultSpectrum.V = data.V;
            viewResultSpectrum.I = data.I;
            ViewResultSpectrums.Add(viewResultSpectrum);


            ListViewItem listViewItem = new ListViewItem();

            double sum1 = 0, sum2 = 0;
            for (int i = 35; i <= 75; i++)
                sum1 += colorParam.fPL[i * 10];
            for (int i = 20; i <= 120; i++)
                sum2 += colorParam.fPL[i * 10];
            ResultNum++;
            List<string> Contents = new List<string>
            {
                ResultNum.ToString(),
                Math.Round(colorParam.fIp / 65535 * 100, 2).ToString() + "%",
                (colorParam.fPh / 1).ToString(),
                Math.Round(sum1 / sum2 * 100, 2).ToString(),

                string.Format("{0:0.0000}",data.V),
                string.Format("{0:0.0000}",data.I),
            };

            ScatterPlots.Add(viewResultSpectrum.ScatterPlot);

            listView1.SelectedIndex = ViewResultSpectrums.Count - 1;
            listView1.ScrollIntoView(listViewItem);
        }

        private List<ScatterPlot> ScatterPlots { get; set; } = new List<ScatterPlot>();

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MulComparison = !MulComparison;
            if (listView1.SelectedIndex <= -1)
            {
                if (listView1.Items.Count == 0)
                    return;
                listView1.SelectedIndex = 0;
            }
            ReDrawPlot();
        }


        private void ReDrawPlot()
        {
            wpfplot1.Plot.Clear();
            wpfplot1.Plot.SetAxisLimitsX(ViewResultSpectrums[listView1.SelectedIndex].fSpect1, ViewResultSpectrums[listView1.SelectedIndex].fSpect2);
            wpfplot1.Plot.SetAxisLimitsY(0, 1);
            wpfplot1.Plot.XAxis.SetBoundary(ViewResultSpectrums[listView1.SelectedIndex].fSpect1-10, ViewResultSpectrums[listView1.SelectedIndex].fSpect2+10);
            wpfplot1.Plot.YAxis.SetBoundary(0, 1);
            LastMulSelectComparsion = null;
            if (MulComparison)
            {
                listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < ViewResultSpectrums.Count; i++)
                {
                    if (i == listView1.SelectedIndex)
                        continue;
                    var plot = ScatterPlots[i];
                    plot.Color = Color.DarkGoldenrod;
                    plot.LineWidth = 1;
                    plot.MarkerSize = 1;

                    wpfplot1.Plot.Add(plot);
                }
            }
            DrawPlot();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show("您需要先选择数据");
                return;
            }
            List<ListViewItem> lists = new List<ListViewItem>();
            foreach (var item in listView1.Items)
            {
                if (item is ListViewItem listViewItem)
                {
                    lists.Add(listViewItem);
                }
            }
            foreach (var item in lists)
            {
                if (item.IsSelected)
                {
                    int index = listView1.Items.IndexOf(item);
                    ViewResultSpectrums.RemoveAt(index);

                    ScatterPlots.RemoveAt(index);
                    listView1.Items.RemoveAt(index);
                    List<string> Contents = (List<string>)item.Content;
                    int id = int.Parse(Contents[Contents.Count - 1]);
                    if (id > 0)
                    {
                        spectumResult.SpectumDeleteById(id);
                    }
                }
            }


            if (listView1.Items.Count > 0)
            {
                listView1.SelectedIndex = 0;
            }
            ReDrawPlot();
        }

        private void ToolBar1_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ToolBar toolBar)
            {
                if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
                    overflowGrid.Visibility = Visibility.Collapsed;
                if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
                    mainPanelBorder.Margin = new Thickness(0);
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResultSpectrums.RemoveAt(listView1.SelectedIndex);
                listView1.Items.RemoveAt(listView1.SelectedIndex);


                if (listView1.Items.Count > 0)
                {
                    listView1.SelectedIndex = temp - 1; ;
                    DrawPlot();
                }
                else
                {
                    wpfplot1.Plot.Clear();
                    wpfplot1.Refresh();
                }
            }

        }

        MarkerPlot markerPlot1;

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            wpfplot1.Plot.Remove(markerPlot1);
            if (listView2.SelectedIndex > -1)
            {
                markerPlot1 = new MarkerPlot
                {
                    X = listView2.SelectedIndex + 380,
                    Y = ViewResultSpectrums[listView1.SelectedIndex].fPL[listView2.SelectedIndex * 10],
                    MarkerShape = MarkerShape.filledCircle,
                    MarkerSize = 10f,
                    Color = Color.Orange,
                    Label = null
                };
                wpfplot1.Plot.Add(markerPlot1);
            }
            wpfplot1.Refresh();

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView1.Visibility = visibility;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView2.Visibility = visibility;
            }
        }
        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = ListRow2.ActualHeight - 38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listView2.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }

        internal void Clear()
        {
            wpfplot1.Plot.Clear();
            listView1.Items.Clear();
            listView2.Items.Clear();
            ResultNum = 0;
        }

        SpectumResultDao spectumResultDao = new SpectumResultDao();


        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSpectrums.Clear();
            ScatterPlots.Clear();


            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text))
            {
                var list = spectumResultDao.GetAll();
                List<SpectumData> SpectumDatas = new List<SpectumData>();

                foreach (var item in list)
                {
                    SpectumData spectumData = new SpectumData();
                    ColorParam param = new ColorParam()
                    {
                        fx = item.fx ?? 0,
                        fy = item.fy ?? 0,
                        fu = item.fu ?? 0,
                        fv = item.fv ?? 0,
                        fCCT = item.fCCT ?? 0,
                        dC = item.dC ?? 0,
                        fLd = item.fLd ?? 0,
                        fPur = item.fPur ?? 0,
                        fLp = item.fLp ?? 0,
                        fHW = item.fHW ?? 0,
                        fLav = item.fLav ?? 0,
                        fRa = item.fRa ?? 0,
                        fRR = item.fRR ?? 0,
                        fGR = item.fGR ?? 0,
                        fBR = item.fBR ?? 0,
                        fIp = item.fIp ?? 0,
                        fPh = item.fPh ?? 0,
                        fPhe = item.fPhe ?? 0,
                        fPlambda = item.fPlambda ?? 0,
                        fSpect1 = item.fSpect1 ?? 0,
                        fSpect2 = item.fSpect2 ?? 0,
                        fInterval = item.fInterval ?? 0,
                        fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? System.Array.Empty<float>(),
                        fRi = JsonConvert.DeserializeObject<float[]>(item.fRi ?? string.Empty) ?? System.Array.Empty<float>(),
                    };

                    spectumData.Data = param;
                    SpectumDatas.Add(spectumData);
                    SpectrumDrawPlot(SpectumDatas.Last());

                }

            }
            else
            {

                var list = spectumResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text);
                List<SpectumData> SpectumDatas = new List<SpectumData>();

                foreach (var item in list)
                {
                    SpectumData spectumData = new SpectumData();
                    ColorParam param = new ColorParam()
                    {
                        fx = item.fx ?? 0,
                        fy = item.fy ?? 0,
                        fu = item.fu ?? 0,
                        fv = item.fv ?? 0,
                        fCCT = item.fCCT ?? 0,
                        dC = item.dC ?? 0,
                        fLd = item.fLd ?? 0,
                        fPur = item.fPur ?? 0,
                        fLp = item.fLp ?? 0,
                        fHW = item.fHW ?? 0,
                        fLav = item.fLav ?? 0,
                        fRa = item.fRa ?? 0,
                        fRR = item.fRR ?? 0,
                        fGR = item.fGR ?? 0,
                        fBR = item.fBR ?? 0,
                        fIp = item.fIp ?? 0,
                        fPh = item.fPh ?? 0,
                        fPhe = item.fPhe ?? 0,
                        fPlambda = item.fPlambda ?? 0,
                        fSpect1 = item.fSpect1 ?? 0,
                        fSpect2 = item.fSpect2 ?? 0,
                        fInterval = item.fInterval ?? 0,
                        fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? System.Array.Empty<float>(),
                        fRi = JsonConvert.DeserializeObject<float[]>(item.fRi ?? string.Empty) ?? System.Array.Empty<float>(),
                    };

                    spectumData.Data = param;
                    SpectumDatas.Add(spectumData);
                    SpectrumDrawPlot(SpectumDatas.Last());
                }
            }

        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxId.Text = string.Empty;
        }

        private void Order_Click(object sender, RoutedEventArgs e)
        {
            OrderPopup.IsOpen = true;
        }
        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioID?.IsChecked == true)
            {
                if (RadioUp?.IsChecked ==true)
                {
                    ViewResultSpectrums.SortById();
                }
                if (RadioDown?.IsChecked == true)
                {
                    ViewResultSpectrums.SortById(true);
                }

            }
            OrderPopup.IsOpen = false;
        }
    }
}
