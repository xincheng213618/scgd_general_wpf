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
            //ComboxPGTemplate.ItemsSource = TemplateControl.GetInstance().PGParams;
            //ComboxPGTemplate.SelectionChanged += (s, e) =>
            //{
            //    if (ComboxPGTemplate.SelectedItem is KeyValuePair<string, PGParam> KeyValue && KeyValue.Value is PGParam pGParam)
            //    {
            //        PG1.PGParam = pGParam;
            //        PG1.DataContext = pGParam;
            //    }
            //};
            //ComboxPGTemplate.SelectedIndex = 0;

            //ComboxPGType.ItemsSource = from e1 in Enum.GetValues(typeof(PGType)).Cast<PGType>()
            //                           select new KeyValuePair<string, PGType>(e1.ToDescription(), e1);
            //ComboxPGType.SelectedIndex = 0;

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

            //ComboxPGCommunicateType.ItemsSource = from e1 in Enum.GetValues(typeof(CommunicateType)).Cast<CommunicateType>()
            //                                      select new KeyValuePair<string, CommunicateType>(e1.ToDescription(), e1);
            //ComboxPGCommunicateType.SelectedIndex = 0;
            //ComboxPGCommunicateType.SelectionChanged += (s, e) =>
            //{
            //    if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, CommunicateType> KeyValue && KeyValue.Value is CommunicateType communicateType)
            //    {
            //        switch (communicateType)
            //        {
            //            case CommunicateType.Tcp:
            //                TextBlockPGIP.Text = "IP";
            //                TextBlockPGPort.Text = "Port";
            //                break;
            //            case CommunicateType.Serial:
            //                TextBlockPGIP.Text = "ComName";
            //                TextBlockPGPort.Text = "BaudRate"; break;
            //        }

            //    }
            //};

        }


        //private void PGInit(object sender, RoutedEventArgs e)
        //{
        //    if (ComboxPGType.SelectedItem is KeyValuePair<string, PGType> KeyValue && KeyValue.Value is PGType pGType)
        //    {
        //        if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, CommunicateType> KeyValue1 && KeyValue1.Value is CommunicateType communicateType)
        //        {
        //            PGService.Init(pGType, communicateType);
        //        }
        //    }
        //}
        //private void PGUnInit(object sender, RoutedEventArgs e)
        //{
        //    PGService.UnInit();
        //}
        private void PGOpen(object sender, RoutedEventArgs e)
        {
            //if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, CommunicateType> KeyValue1 && KeyValue1.Value is CommunicateType communicateType)
            {
                int port;
                if (!int.TryParse(TextBoxPGPort.Text, out port))
                {
                    MessageBox.Show("端口配置错误");
                    return;
                }
                if(this.PGService.Config.IsTCPIP) PGService.Open(CommunicateType.Tcp, TextBoxPGIP.Text, port);
                else PGService.Open(CommunicateType.Serial, TextBoxPGIP.Text, port);
            }
        }
        private void PGClose(object sender, RoutedEventArgs e)
        {
            PGService.Close();
        }
        private void PGStartPG(object sender, RoutedEventArgs e) => PGService.PGStartPG();

        private void PGStopPG(object sender, RoutedEventArgs e) => PGService.PGStopPG();

        private void PGReSetPG(object sender, RoutedEventArgs e) => PGService.PGReSetPG();
        private void PGSwitchUpPG(object sender, RoutedEventArgs e) => PGService.PGSwitchUpPG();
        private void PGSwitchDownPG(object sender, RoutedEventArgs e) => PGService.PGSwitchDownPG();

        private void PGSwitchFramePG(object sender, RoutedEventArgs e) => PGService.PGSwitchFramePG(int.Parse(PGFrameText.Text));

        private void PGSendCmd(object sender, RoutedEventArgs e)
        {

        }
    }
}
