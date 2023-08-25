using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.SMU
{
    /// <summary>
    /// MQTTSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTSMUControl : UserControl
    {

        public DeviceSMU DeviceSMU { get; set; }
        private SMUService SMUService { get => DeviceSMU.SMUService;  }
        public SMUView View { get => DeviceSMU.View; }

        PassSxSource passSxSource;

        public MQTTSMUControl(DeviceSMU deviceSMU)
        {
            this.DeviceSMU = deviceSMU;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SMUService;

            SMUService.HeartbeatHandlerEvent += (e) => SMUService_DeviceStatusHandler(e.DeviceStatus);
            SMUService.ScanResultHandlerEvent += SMUService_ScanResultHandler;
            SMUService.ResultHandlerEvent += SMUService_ResultHandler;


            passSxSource = new PassSxSource();
            passSxSource.IsNet = SMUService.Config.IsNet;
            passSxSource.DevName = SMUService.Config.ID;
            StackPanelVI.DataContext = passSxSource;

            StackPanelVITemplate.DataContext = passSxSource;

            ComboxVITemplate.ItemsSource = TemplateControl.GetInstance().SxParams;
            ComboxVITemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxVITemplate.SelectedItem is KeyValuePair<string, SxParam> KeyValue && KeyValue.Value is SxParam SxParm)
                {
                    passSxSource.StartMeasureVal = SxParm.StartMeasureVal;
                    passSxSource.StopMeasureVal = SxParm.StopMeasureVal;
                    passSxSource.IsSourceV = SxParm.IsSourceV;
                    passSxSource.LimitVal = SxParm.LmtVal;
                    passSxSource.Number = SxParm.Number;
                }
            };
            ComboxVITemplate.SelectedIndex = 0;

            ViewGridManager.GetInstance().AddView(View);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>("独立窗口", -2));
                KeyValues.Add(new KeyValuePair<string, int>("隐藏", -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i+1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                //ComboxView.SelectedIndex = View.View.ViewIndex + 2;
            };
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is  KeyValuePair<string, int> KeyValue)
                {
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);

                }
            };
        }

        private void SMUService_ResultHandler(SMUResultData data)
        {
            passSxSource.V = data.V;
            passSxSource.I = data.I;
        }

        private void SMUService_ScanResultHandler(SMUScanResultData data)
        {
            passSxSource.VList = data.VList;
            passSxSource.IList = data.IList;
            try
            {
                View.DrawPlot(passSxSource.IsSourceV, passSxSource.StopMeasureVal, passSxSource.VList, passSxSource.IList);
            }
            catch 
            {

            }
        }

        private  void SMUService_DeviceStatusHandler(DeviceStatus deviceStatus)
        {
            if (deviceStatus == DeviceStatus.Opened)
            {
                ButtonSourceMeter1.Content = "关闭";
            }
            else if (deviceStatus == DeviceStatus.Closed)
            {
                ButtonSourceMeter1.Content = "打开";
            }
            else if (deviceStatus == DeviceStatus.Opening)
            {
                ButtonSourceMeter1.Content = "打开中";
            }
            else if (deviceStatus == DeviceStatus.Closing)
            {
                ButtonSourceMeter1.Content = "关闭中";
            }
        }

        private void DoOpenByDll(Button button)
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

        private void DoOpenByMQTT(Button button)
        {
            string btnTitle = button.Content.ToString();
            if (btnTitle!=null && btnTitle.Equals("打开", StringComparison.Ordinal))
            {
                button.Content = "打开中";
                SMUService.Open(passSxSource.IsNet, passSxSource.DevName);
            }
            else
            {
                button.Content = "关闭中";
                SMUService.Close();
            }
        }


        private void ButtonSourceMeter1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                DoOpenByMQTT(button);
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            //double V = 0, I = 0;
            SMUService.GetData(passSxSource.IsSourceV, passSxSource.MeasureVal, passSxSource.LmtVal);
            //passSxSource.MeasureData(passSxSource.MeasureVal, passSxSource.LmtVal, ref V, ref I);
        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            SMUService.GetData(passSxSource.IsSourceV, passSxSource.MeasureVal, passSxSource.LmtVal);
        }
        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            SMUService.CloseOutput();
            passSxSource.V = null;
            passSxSource.I = null;
        }
        private void VIScan_Click(object sender, RoutedEventArgs e)
        {
            SMUService.Scan(passSxSource.IsSourceV, passSxSource.StartMeasureVal, passSxSource.StopMeasureVal, passSxSource.LimitVal, passSxSource.Number);
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


        private void StackPanelVI_Initialized(object sender, EventArgs e)
        {

        }

        private void MQTTVIOpen(object sender, RoutedEventArgs e)
        {
            SMUService.Open(passSxSource.IsNet, passSxSource.DevName);
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
            //SMUService.GetData();
        }


    }
}
