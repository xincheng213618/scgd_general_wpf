using ColorVision.MQTT.Spectrum;
using ColorVision.MySql.Service;
using Microsoft.VisualBasic;
using OpenCvSharp.Flann;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static cvColorVision.GCSDLL;

namespace ColorVision.Template
{
    /// <summary>
    /// SpectrumResult.xaml 的交互逻辑
    /// </summary>
    public partial class SpectrumResult : UserControl
    {
        private ResultService spectumResult;
        public SpectrumResult()
        {
            InitializeComponent();
            spectumResult = new ResultService();
        }
        static int ResultNum ;
        private void UserControl_Initialized(object sender, EventArgs e)
        {

            wpfplot1.Plot.Title("相对光谱曲线");
            wpfplot1.Plot.XLabel("波长[nm]");
            wpfplot1.Plot.YLabel("相对光谱");

            GridView gridView = new GridView();

            List<string> headers = new List<string> { "序号", "测量时间", "IP", "亮度Lv(cd/m2)", "蓝光", "色度x", "色度y", "色度u", "色度v", "相关色温(K)", "主波长Ld(nm)", "色纯度(%)", "峰值波长Lp(nm)", "显色性指数Ra", "半波宽" };

            GridViewColumn gridViewColumn = new GridViewColumn();
            for (int i = 380; i < 781; i++)
            {
                headers.Add(i.ToString());
            }
            //headers.Add("电压(V)");
            //headers.Add("电流(mA)");

            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }
            listView1.View = gridView;
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
                DrawPlot(colorParams[listview.SelectedIndex]);
            }
        }


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

        private void DrawPlot(ColorParam colorParam)
        {
            wpfplot1.Plot.Clear();
            wpfplot1.Plot.SetAxisLimitsX(380, 810);
            wpfplot1.Plot.SetAxisLimitsY(0, 1);
            wpfplot1.Plot.XAxis.SetBoundary(370, 850);
            wpfplot1.Plot.YAxis.SetBoundary(0, 1);
            if (MulComparison)
            {
                listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < colorParams.Count; i++)
                {
                    if (i == listView1.SelectedIndex)
                        continue;
                    DrawPlotCore(colorParams[i], Color.DarkGoldenrod, 1);
                }
                DrawPlotCore(colorParams[listView1.SelectedIndex], Color.Red);
            }
            else
            {
                DrawPlotCore(colorParam, Color.DarkGoldenrod);
            }
            wpfplot1.Refresh();
        }


        public void SpectrumDrawPlot(SpectumData data)
        {
            ColorParam colorParam = data.Data;
            colorParams.Add(colorParam);

            ListViewItem listViewItem = new ListViewItem();

            double sum1 = 0, sum2 = 0;
            for (int i = 35; i <= 75; i++)
                sum1 += colorParam.fPL[i * 10];
            for (int i = 20; i <= 120; i++)
                sum2 += colorParam.fPL[i * 10];
            ResultNum++;
            List<string> Contents = new List<string>() { ResultNum.ToString(), DateTime.Now.ToString(), Math.Round(colorParam.fIp / 65535 * 100, 2).ToString() + "%", (colorParam.fPh / 1).ToString() };
            Contents.Add(Math.Round(sum1 / sum2 * 100, 2).ToString());
            Contents.Add(Convert.ToString(Math.Round(colorParam.fx, 4)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fy, 4)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fu, 4)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fv, 4)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fCCT, 1)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fLd, 1)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fPur, 2)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fLp, 1)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fRa, 2)));
            Contents.Add(Convert.ToString(Math.Round(colorParam.fHW, 4)));
            for (int i = 0; i < 4000; i += 10)
            {
                Contents.Add(colorParam.fPL[i].ToString());
            }
            Contents.Add(colorParam.fPL[3998].ToString());

            //Contents.Add("NaN");
            //Contents.Add("NaN");
            Contents.Add(data.ID.ToString());


            listViewItem.Content = Contents;
            listView1.Items.Add(listViewItem);
            listView1.SelectedIndex = colorParams.Count - 1;
            DrawPlot(colorParam);
            listView1.ScrollIntoView(listViewItem);
        }



        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MulComparison = !MulComparison;
            if (listView1.SelectedIndex <= -1)
            {
                if (listView1.Items.Count == 0)
                    return;
                listView1.SelectedIndex = 0;
            }
            DrawPlot(colorParams[listView1.SelectedIndex]);
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
                    listView1.Items.RemoveAt(index);
                    List<string> Contents = (List<string>)item.Content;
                    int id = int.Parse(Contents[Contents.Count - 1]);
                    if( id > 0 )
                    {
                        spectumResult.SpectumDeleteById(id);
                    }
                }
            }


            if (listView1.Items.Count > 0)
            {
                listView1.SelectedIndex = 0;
                DrawPlot(colorParams[listView1.SelectedIndex]);
            }
            else
            {
                wpfplot1.Plot.Clear();
                wpfplot1.Refresh();
            }

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
            if (e.Key == Key.Delete&&listView1.SelectedIndex>-1)
            {
                int temp = listView1.SelectedIndex;
                colorParams.RemoveAt(listView1.SelectedIndex);
                listView1.Items.RemoveAt(listView1.SelectedIndex);


                if (listView1.Items.Count > 0)
                {
                    listView1.SelectedIndex = temp - 1; ;
                    DrawPlot(colorParams[listView1.SelectedIndex]);
                }
                else
                {
                    wpfplot1.Plot.Clear();
                    wpfplot1.Refresh();
                }
            }

        }
    }
}
