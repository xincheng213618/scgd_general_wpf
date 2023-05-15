using Microsoft.VisualBasic;
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
        public SpectrumResult()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
                return;

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

        private List<List<string>> ListContents { get;  set; }  =new List<List<string>>() { };



        public void SpectrumDrawPlot(ColorParam colorParam)
        {
            double[] x = new double[colorParam.fPL.Length];
            double[] y = new double[colorParam.fPL.Length];
            for (int i = 0; i < colorParam.fPL.Length; i++)
            {
                x[i] = ((double)colorParam.fSpect1 + Math.Round(colorParam.fInterval, 1) * i);
                y[i] = colorParam.fPL[i];
            }

            var plt = new ScottPlot.Plot(400, 300);
            plt.Title("相对光谱曲线");
            plt.XLabel("波长[nm]");
            plt.YLabel("相对光谱");

            plt.AddScatter(x, y, Color.DarkGoldenrod, 3, 3, 0);
            plt.SetAxisLimitsX(380, 780);
            plt.SetAxisLimitsY(0, 1);
            plt.XAxis.SetBoundary(370, 810);
            plt.YAxis.SetBoundary(0, 1);

            new ScottPlot.WpfPlotViewer(plt).Show();


            ListView listView = new ListView();
            GridView gridView = new GridView();
            listView.View = gridView;

            List<string> headers = new List<string> { "序号", "测量时间", "IP", "亮度Lv(cd/m2)", "蓝光", "色度x", "色度y", "色度u", "色度v", "相关色温(K)", "主波长Ld(nm)", "色纯度(%)", "峰值波长Lp(nm)", "显色性指数Ra", "半波宽" };

            GridViewColumn gridViewColumn = new GridViewColumn();
            for (int i = 380; i < 781; i++)
            {
                headers.Add(i.ToString());
            }
            headers.Add("电压(V)");
            headers.Add("电流(mA)");

            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }
            ListViewItem listViewItem = new ListViewItem();


            List<string> strings = new List<string>();
            double sum1 = 0, sum2 = 0;
            for (int i = 35; i <= 75; i++)
                sum1 += colorParam.fPL[i * 10];
            for (int i = 20; i <= 120; i++)
                sum2 += colorParam.fPL[i * 10];


            List<string> Contents = new List<string>() { "1", DateTime.Now.ToString(), Math.Round(colorParam.fIp / 65535 * 100, 2).ToString() + "%", (colorParam.fPh / 1).ToString() };
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

            Contents.Add("NaN");
            Contents.Add("NaN");


            listViewItem.Content = Contents;
            listView.Items.Add(listViewItem);

            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(listView);

            Window window = new Window();
            window.Content = stackPanel;
            window.Show();
        }



    }
}
