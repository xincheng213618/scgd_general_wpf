using ColorVision.MQTT.SMU;
using ColorVision.Template;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.MQTT.SMU
{
    /// <summary>
    /// MQTTSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTSMUControl : UserControl
    {
        private SMUService SMUService { get; set; }

        public MQTTSMUControl(SMUService Source)
        {
            this.SMUService = Source;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SMUService;
        }

        private void ButtonSourceMeter1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (!passSxSource.IsOpen)
                {
                    button.Content = "打开中";
                    Task.Run(() =>
                    {
                        if (passSxSource.Open(passSxSource.IsNet, passSxSource.DevName))
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                button.Content = "关闭";
                            }));
                        }
                        else
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                button.Content = "打开失败";
                            }));
                        }
                    });
                }
                else
                {
                    passSxSource.Close();
                    button.Content = "打开";
                }
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            double V = 0, I = 0;
            passSxSource.MeasureData(passSxSource.MeasureVal, passSxSource.LmtVal, ref V, ref I);
        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            double V = 0, I = 0;
            passSxSource.MeasureData(passSxSource.MeasureVal, passSxSource.LmtVal, ref V, ref I);
        }

        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            passSxSource.CloseOutput();
        }

        private void VIScan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxVITemplate.SelectedItem is KeyValuePair<string, SxParam> KeyValue && KeyValue.Value is SxParam SxParm)
                {
                    button.Content = "扫描中";
                    passSxSource.SetSource(SxParm.IsSourceV);
                    Task.Run(() =>
                    {
                        passSxSource.Scan(SxParm.StartMeasureVal, SxParm.StopMeasureVal, SxParm.LmtVal, SxParm.Number);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            button.Content = "扫描";
                            showPxResult(SxParm.IsSourceV, SxParm.StopMeasureVal);
                        });
                    });
                }
            }

        }

        private void showPxResult(bool isSourceV, double endVal)
        {
            var plt = new ScottPlot.Plot(400, 300);

            plt.Title("电压曲线");
            if (isSourceV)
            {
                plt.XLabel("电压(V)");
                plt.YLabel("电流(A)");
            }
            else
            {
                plt.XLabel("电流(A)");
                plt.YLabel("电压(V)");
            }
            ArrayList listV = new ArrayList();
            ArrayList listI = new ArrayList();
            double VMax = 0, IMax = 0, VMin = 10000, IMin = 10000;
            for (int i = 0; i < passSxSource.VList.Length; i++)
            {
                if (passSxSource.VList[i] > VMax) VMax = passSxSource.VList[i];
                if (passSxSource.IList[i] > IMax) IMax = passSxSource.IList[i];
                if (passSxSource.VList[i] < VMin) VMin = passSxSource.VList[i];
                if (passSxSource.IList[i] < IMin) IMin = passSxSource.IList[i];

                listV.Add(passSxSource.VList[i]);
                listI.Add(passSxSource.IList[i]);
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
                xs = (double[])listV.ToArray(typeof(double));
                ys = (double[])listI.ToArray(typeof(double));
            }
            else
            {
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
                xs = (double[])listI.ToArray(typeof(double));
                ys = (double[])listV.ToArray(typeof(double));
            }

            plt.AddScatter(xs, ys, System.Drawing.Color.DarkGoldenrod, 3, 3, 0);
            try
            {
                plt.SetAxisLimitsX(xMin, xMax);
                plt.SetAxisLimitsY(yMin, yMax);
            }
            catch { }
            new ScottPlot.WpfPlotViewer(plt).Show();
        }


        private void VIExport_Click(object sender, RoutedEventArgs e)
        {
            if (passSxSource.VList.Length == 0)
            {
                MessageBox.Show("导出前，请先测量");
                return;
            }

            System.Windows.Forms.SaveFileDialog saveFile = new System.Windows.Forms.SaveFileDialog();
            saveFile.Filter = "CSV文档.csv|*.csv|所有文档|*.*";
            if (saveFile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string csvTxt = "电压(V),电流(mA)\r\n";
                for (int i = 0; i < passSxSource.VList.Length; i++)
                {
                    csvTxt += passSxSource.VList[i].ToString() + "," + passSxSource.IList[i].ToString() + "\r\n";
                }

                string fName = saveFile.FileName;
                FileStream fs = File.Open(fName, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, UnicodeEncoding.UTF8);
                sw.Write(csvTxt);
                sw.Close();
                fs.Close();
            }
        }

        PassSxSource passSxSource;

        private void StackPanelVI_Initialized(object sender, EventArgs e)
        {
            //SMUService = new SMUService();
            passSxSource = new PassSxSource();
            passSxSource.IsNet = SMUService.Device.Config.IsNet;
            passSxSource.DevName = SMUService.Device.Config.ID;
            StackPanelVI.DataContext = passSxSource;


            ComboxVITemplate.ItemsSource = TemplateControl.GetInstance().SxParams;
            ComboxVITemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxVITemplate.SelectedItem is KeyValuePair<string, SxParam> KeyValue && KeyValue.Value is SxParam SxParm)
                {
                    StackPanelVITemplate.DataContext = SxParm;
                }
            };
            ComboxVITemplate.SelectedIndex = 0;
        }

        private void MQTTVIOpen(object sender, RoutedEventArgs e)
        {
            SMUService.Open();
        }
        private void MQTTVIClose(object sender, RoutedEventArgs e)
        {
            SMUService.Close();
        }

        private void MQTTVISetParam(object sender, RoutedEventArgs e)
        {
            SMUService.SetParam();
        }


        private void MQTTVIGetData(object sender, RoutedEventArgs e)
        {
            SMUService.GetData();
        }


    }
}
