using ColorVision.MQTT.Service;
using ColorVision.MQTT.Spectrum;
using ColorVision.MySql.Service;
using ColorVision.Template;
using NPOI.SS.Formula.Eval;
using NPOI.SS.Formula.Functions;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT.SMU
{
    /// <summary>
    /// SpectrumView.xaml 的交互逻辑
    /// </summary>
    public partial class SMUView : UserControl,IView
    {
        public View View { get; set; }
        private ResultService spectumResult;
        public SMUView()
        {
            spectumResult = new ResultService();
            InitializeComponent();
            View = new View();
        }

        static int ResultNum;
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ContextMenu ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "设为主窗口" };
            menuItem.Click += (s, e) =>
            {
                ViewGridManager.GetInstance().SetOneView(this);
            };
            ContextMenu.Items.Add(menuItem);

            MenuItem menuItem1 = new MenuItem() { Header = "展示全部窗口" };
            menuItem1.Click += (s, e) =>
            {
                ViewGridManager.GetInstance().SetViewNum(-1);
            };
            ContextMenu.Items.Add(menuItem1);

            MenuItem menuItem2 = new MenuItem() { Header = "独立窗口中显示" };
            menuItem2.Click += (s, e) =>
            {
                ViewGridManager.GetInstance().SetSingleWindowView(this);
            };
            ContextMenu.Items.Add(menuItem2);

            this.ContextMenu = ContextMenu;

            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "测量时间"};
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }
            listView1.View = gridView;

            List<string> headers2 = new List<string> { "波长", "相对光谱", "绝对光谱" };

            GridView gridView2 = new GridView();
            for (int i = 0; i < headers2.Count; i++)
            {
                gridView2.Columns.Add(new GridViewColumn() { Header = headers2[i], DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }

            listView2.View = gridView2;

            wpfplot1.Plot.Clear();

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
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using StreamWriter file = new StreamWriter(dialog.FileName, true, Encoding.UTF8);
                if (listView1.View is GridView gridView1)
                {
                    string headers = "";
                    foreach (var item in gridView1.Columns)
                    {
                        headers += item.Header.ToString() + ",";
                    }
                    file.WriteLine(headers);
                }
                string value = "";
                foreach (var item in ListContents[listView1.SelectedIndex])
                {
                    value += item + ",";
                }
                file.WriteLine(value);
            }

        }

        private List<List<string>> ListContents { get; set; } = new List<List<string>>() { };

        public List<ColorParam> colorParams { get; set; } = new List<ColorParam>() { };

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                //listView2.ItemsSource = colorParams[listview.SelectedIndex].fPL;

            }
        }

        ScatterPlot scatterPlot;

        private void DrawPlotCore(ColorParam colorParam, Color color, float lineWidth = 3)
        {
            double[] x = new double[colorParam.fPL.Length];
            double[] y = new double[colorParam.fPL.Length];
            for (int i = 0; i < colorParam.fPL.Length; i++)
            {
                x[i] = ((double)colorParam.fSpect1 + Math.Round(colorParam.fInterval, 1) * i);
                y[i] = colorParam.fPL[i];
            }
            wpfplot1.Plot.AddScatter(x, y, color, lineWidth, 3, 0);
        }


        bool MulComparison;
        ScatterPlot? LastMulSelectComparsion;

        private void DrawPlot()
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
            wpfplot1.Refresh();
        }

        public void DrawPlot(bool isSourceV, double endVal,double[] VList, double[] IList)
        {


            ListViewItem listViewItem = new ListViewItem();


            List<double> listV = new List<double>();
            List<double> listI = new List<double>();
            double VMax = 0, IMax = 0, VMin = 10000, IMin = 10000;
            for (int i = 0; i < VList.Length; i++)
            {
                if (VList[i] > VMax) VMax = VList[i];
                if (IList[i] > IMax) IMax = IList[i];
                if (VList[i] < VMin) VMin = VList[i];
                if (IList[i] < IMin) IMin = IList[i];

                listV.Add(VList[i]);
                listI.Add(IList[i]);
            }
            int step = 10;
            double xMin = 0;
            double xMax = VMax + VMax / step;
            double yMin = 0 - IMax / step;
            double yMax = IMax + IMax / step;
            double[] xs, ys;
            if (isSourceV)
            {
                xMin = VMin - VMin / step;
                xMax = endVal + VMax / step;
                yMin = IMin - IMin / step;
                yMax = IMax + IMax / step;
                if (VMax < endVal)
                {
                    double addPointStep = (endVal - VMax) / 2.0;
                    listV.Add(VMax + addPointStep);
                    listV.Add(endVal);
                    listI.Add(IMax);
                    listI.Add(IMax);
                }
                xs = listV.ToArray();
                ys = listI.ToArray();
            }
            else
            {
                endVal = endVal / 1000;
                xMin = IMin - IMin / step;
                xMax = endVal + IMax / step;
                yMin = VMin - VMin / step;
                yMax = VMax + VMax / step;
                if (IMax < endVal)
                {
                    double addPointStep = (endVal - IMax) / 2.0;
                    listI.Add(IMax + addPointStep);
                    listI.Add(endVal);
                    listV.Add(VMax);
                    listV.Add(VMax);
                }
                xs = listV.ToArray();
                ys = listI.ToArray();
            }
            if (isSourceV)
            {
                wpfplot1.Plot.XLabel("电压(V)");
                wpfplot1.Plot.YLabel("电流(A)");
                wpfplot1.Plot.Title("电压曲线");
            }
            else
            {
                wpfplot1.Plot.XLabel("电流(A)");
                wpfplot1.Plot.YLabel("电压(V)");
                wpfplot1.Plot.Title("电流曲线");
            }
            wpfplot1.Plot.SetAxisLimitsX(xMin, xMax);
            wpfplot1.Plot.SetAxisLimitsY(yMin, yMax);

            ScatterPlot scatterPlot = new ScatterPlot(xs, ys)
            {
                Color = Color.DarkGoldenrod,
                LineWidth = 1,
                MarkerSize = 1,
                Label = null,
                MarkerShape = MarkerShape.none,
                LineStyle = LineStyle.Solid
            };


            ScatterPlots.Add(scatterPlot);
            //listViewItem.Content = Contents;
            listView1.Items.Add(listViewItem);
            listView1.SelectedIndex = colorParams.Count - 1;
            listView1.ScrollIntoView(listViewItem);
            wpfplot1.Plot.Add(scatterPlot);
            wpfplot1.Refresh();
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
            wpfplot1.Plot.SetAxisLimitsX(380, 810);
            wpfplot1.Plot.SetAxisLimitsY(0, 1);
            wpfplot1.Plot.XAxis.SetBoundary(370, 850);
            wpfplot1.Plot.YAxis.SetBoundary(0, 1);
            LastMulSelectComparsion = null;
            if (MulComparison)
            {
                listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < colorParams.Count; i++)
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
            if (listView1.SelectedIndex < 0 || colorParams.Count <= 0)
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
                    colorParams.RemoveAt(index);

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
                colorParams.RemoveAt(listView1.SelectedIndex);
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
                    Y = colorParams[listView1.SelectedIndex].fPL[listView2.SelectedIndex * 10],
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
            listView1.Height = ListRow2.ActualHeight -38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listView2.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }
    }
}
