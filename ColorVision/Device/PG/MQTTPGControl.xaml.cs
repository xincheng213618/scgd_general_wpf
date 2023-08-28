using ColorVision.Device.SMU;
using ColorVision.Extension;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.PG
{
    /// <summary>
    /// MQTTPGControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTPGControl : UserControl
    {
        private PGService PGService { get; set; }

        public MQTTPGControl(PGService pg)
        {
            PGService = pg;
            InitializeComponent();
            this.DataContext = PGService;
        }


        private void StackPanelPG_Initialized(object sender, EventArgs e)
        {
            StackPanelPG.DataContext = PGService;

            this.PGService.HeartbeatEvent += (e) => PGService_DeviceStatusHandler(e.DeviceStatus);

            if (this.PGService.Config.IsTCPIP)
            {
                TextBlockPGIP.Text = "IP地址";
                TextBlockPGPort.Text = "端口";
            }
            else
            {
                TextBlockPGIP.Text = "串口";
                TextBlockPGPort.Text = "波特率";
            }
        }

        private void PGService_DeviceStatusHandler(DeviceStatus deviceStatus)
        {
            if (deviceStatus == DeviceStatus.Opened)
            {
                btn_open.Content = "关闭";
            }
            else if (deviceStatus == DeviceStatus.Closed)
            {
                btn_open.Content = "打开";
            }
            else if (deviceStatus == DeviceStatus.Opening)
            {
                btn_open.Content = "打开中";
            }
            else if (deviceStatus == DeviceStatus.Closing)
            {
                btn_open.Content = "关闭中";
            }
        }

        private void DoOpen(Button button)
        {
            string btnTitle = button.Content.ToString();
            if (btnTitle != null && btnTitle.Equals("打开", StringComparison.Ordinal))
            {
                button.Content = "打开中";
                int port;
                if (!int.TryParse(TextBoxPGPort.Text, out port))
                {
                    MessageBox.Show("端口配置错误");
                    return;
                }
                if (this.PGService.Config.IsTCPIP) PGService.Open(CommunicateType.Tcp, TextBoxPGIP.Text, port);
                else PGService.Open(CommunicateType.Serial, TextBoxPGIP.Text, port);
            }
            else
            {
                button.Content = "关闭中";
                this.PGService.Close();
            }
        }

        private void PGOpen(object sender, RoutedEventArgs e)
        {
            if (sender is Button button) DoOpen(button);
        }

        private void PGStartPG(object sender, RoutedEventArgs e) => PGService.PGStartPG();

        private void PGStopPG(object sender, RoutedEventArgs e) => PGService.PGStopPG();

        private void PGReSetPG(object sender, RoutedEventArgs e) => PGService.PGReSetPG();
        private void PGSwitchUpPG(object sender, RoutedEventArgs e) => PGService.PGSwitchUpPG();
        private void PGSwitchDownPG(object sender, RoutedEventArgs e) => PGService.PGSwitchDownPG();

        private void PGSwitchFramePG(object sender, RoutedEventArgs e) => PGService.PGSwitchFramePG(int.Parse(PGFrameText.Text));

        private void PGSendCmd(object sender, RoutedEventArgs e)
        {
            PGService.CustomPG(PGCmdMsg.Text);
        }
    }
}
