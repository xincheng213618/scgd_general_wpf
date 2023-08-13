using ColorVision.Extension;
using ColorVision.MQTT.PG;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.MQTT.Control
{
    /// <summary>
    /// MQTTPGControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTPGControl : UserControl
    {
        private MQTTPG MQTTPG { get; set; }

        public MQTTPGControl(MQTTPG pg)
        {
            MQTTPG = pg;
            InitializeComponent();
            this.DataContext = MQTTPG;
        }
        private void StackPanelPG_Initialized(object sender, EventArgs e)
        {
            StackPanelPG.DataContext = MQTTPG;
            ComboxPGTemplate.ItemsSource = TemplateControl.GetInstance().PGParams;
            ComboxPGTemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxPGTemplate.SelectedItem is KeyValuePair<string, PGParam> KeyValue && KeyValue.Value is PGParam pGParam)
                {
                    PG1.PGParam = pGParam;
                    PG1.DataContext = pGParam;
                }
            };
            ComboxPGTemplate.SelectedIndex = 0;

            ComboxPGType.ItemsSource = from e1 in Enum.GetValues(typeof(PGType)).Cast<PGType>()
                                       select new KeyValuePair<string, PGType>(e1.ToDescription(), e1);
            ComboxPGType.SelectedIndex = 0;


            ComboxPGCommunicateType.ItemsSource = from e1 in Enum.GetValues(typeof(CommunicateType)).Cast<CommunicateType>()
                                                  select new KeyValuePair<string, CommunicateType>(e1.ToDescription(), e1);
            ComboxPGCommunicateType.SelectedIndex = 0;
            ComboxPGCommunicateType.SelectionChanged += (s, e) =>
            {
                if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, CommunicateType> KeyValue && KeyValue.Value is CommunicateType communicateType)
                {
                    switch (communicateType)
                    {
                        case CommunicateType.Tcp:
                            TextBlockPGIP.Text = "IP";
                            TextBlockPGPort.Text = "Port";
                            break;
                        case CommunicateType.Serial:
                            TextBlockPGIP.Text = "ComName";
                            TextBlockPGPort.Text = "BaudRate"; break;
                    }

                }
            };

        }


        private void PGInit(object sender, RoutedEventArgs e)
        {
            if (ComboxPGType.SelectedItem is KeyValuePair<string, PGType> KeyValue && KeyValue.Value is PGType pGType)
            {
                if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, CommunicateType> KeyValue1 && KeyValue1.Value is CommunicateType communicateType)
                {
                    MQTTPG.Init(pGType, communicateType);
                }
            }
        }
        private void PGUnInit(object sender, RoutedEventArgs e)
        {
            MQTTPG.UnInit();
        }
        private void PGOpen(object sender, RoutedEventArgs e)
        {
            if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, CommunicateType> KeyValue1 && KeyValue1.Value is CommunicateType communicateType)
            {
                int port;
                if (!int.TryParse(TextBoxPGPort.Text, out port))
                {
                    MessageBox.Show("端口配置错误");
                    return;
                }

                MQTTPG.Open(communicateType, TextBoxPGIP.Text, port);
            }
        }
        private void PGClose(object sender, RoutedEventArgs e)
        {
            MQTTPG.Close();
        }
        private void PGStartPG(object sender, RoutedEventArgs e) => MQTTPG.PGStartPG();

        private void PGStopPG(object sender, RoutedEventArgs e) => MQTTPG.PGStopPG();

        private void PGReSetPG(object sender, RoutedEventArgs e) => MQTTPG.PGReSetPG();
        private void PGSwitchUpPG(object sender, RoutedEventArgs e) => MQTTPG.PGSwitchUpPG();
        private void PGSwitchDownPG(object sender, RoutedEventArgs e) => MQTTPG.PGSwitchDownPG();

        private void PGSwitchFramePG(object sender, RoutedEventArgs e) => MQTTPG.PGSwitchFramePG(int.Parse(PGFrameText.Text));

    }
}
