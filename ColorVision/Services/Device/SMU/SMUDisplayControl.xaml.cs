using ColorVision.Services.Device;
using ColorVision.Services.Device.SMU.Configs;
using ColorVision.Services.Device.SMU.Views;
using ColorVision.Templates;
using ColorVision.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.SMU
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class SMUDisplayControl : UserControl
    {

        public DeviceSMU Device { get; set; }
        private SMUService DService { get => Device.Service;  }
        private ConfigSMU Config { get => Device.Config; }

        public ViewSMU View { get => Device.View; }


        public SMUDisplayControl(DeviceSMU deviceSMU)
        {
            this.Device = deviceSMU;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

            DService.HeartbeatEvent += (e) => SMUService_DeviceStatusHandler(e.DeviceStatus);
            DService.ScanResultEvent += SMUService_ScanResultHandler;
            DService.ResultEvent += SMUService_ResultHandler;

            ComboxVITemplate.ItemsSource = TemplateControl.GetInstance().SMUParams;
            ComboxVITemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxVITemplate.SelectedItem is TemplateModel<SMUParam> KeyValue && KeyValue.Value is SMUParam SxParm)
                {
                    Config.StartMeasureVal = SxParm.StartMeasureVal;
                    Config.StopMeasureVal = SxParm.StopMeasureVal;
                    Config.IsSourceV = SxParm.IsSourceV;
                    Config.LimitVal = SxParm.LmtVal;
                    Config.Number = SxParm.Number;
                }
            };
            ComboxVITemplate.SelectedIndex = 0;


            ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

            void ViewMaxChangedEvent(int max)
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < max; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            }
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
            View.View.ViewIndex = -1;
        }

        private void SMUService_ResultHandler(SMUResultData data)
        {
            Config.V = data.V;
            Config.I = data.I;
        }

        private void SMUService_ScanResultHandler(SMUScanResultData data)
        {
            try
            {
                View.DrawPlot(Config.IsSourceV, Config.StopMeasureVal, data.VList, data.IList);
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

        PassSxSource passSxSource = new PassSxSource();

        private void DoOpenByDll(Button button)
        {
            if (!passSxSource.IsOpen)
            {
                button.Content = "打开中";
                Task.Run(() =>
                {
                    if (passSxSource.Open(Config.IsNet, Config.DevName))
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




        private void ButtonSourceMeter1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (Config.DeviceStatus != DeviceStatus.Opening)
                {
                    Helpers.SendCommand(button, DService.Open(Config.IsNet, Config.DevName));
                }
                else
                {
                    Helpers.SendCommand(button, DService.Close());
                }
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            //double V = 0, I = 0;
            DService.GetData(Config.IsSourceV, Config.MeasureVal, Config.LmtVal);
        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            DService.GetData(Config.IsSourceV, Config.MeasureVal, Config.LmtVal);
        }
        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            DService.CloseOutput();
            Config.V = null;
            Config.I = null;
        }
        private void VIScan_Click(object sender, RoutedEventArgs e)
        {
            DService.Scan(Config.IsSourceV, Config.StartMeasureVal, Config.StopMeasureVal, Config.LimitVal, Config.Number);
        }

        private void StackPanelVI_Initialized(object sender, EventArgs e)
        {

        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (control.Tag?.ToString() ?? string.Empty)
                {

                    case "SMUParam":
                        windowTemplate = new WindowTemplate(TemplateType.SMUParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }
    }
}
