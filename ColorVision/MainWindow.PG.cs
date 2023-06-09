using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static ColorVision.MQTT.MQTTPG;

namespace ColorVision
{
    /// <summary>
    /// PG操作
    /// </summary>
    public partial class MainWindow
    {
        private MQTTPG MQTTPG { get; set; }

        private void StackPanelPG_Initialized(object sender, EventArgs e)
        {
            MQTTPG = new MQTTPG();

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

            ComboxPGType.ItemsSource = from e1 in Enum.GetValues(typeof(MQTTPG.PGType)).Cast<MQTTPG.PGType>()
                                                    select new KeyValuePair<string,MQTTPG.PGType >(e1.ToDescription(),e1);
            ComboxPGType.SelectedIndex = 0;


            ComboxPGCommunicateType.ItemsSource = from e1 in Enum.GetValues(typeof(MQTTPG.CommunicateType)).Cast<MQTTPG.CommunicateType>()
                                                       select new KeyValuePair<string,MQTTPG.CommunicateType>( e1.ToDescription(), e1);
            ComboxPGCommunicateType.SelectedIndex = 0;
            ComboxPGCommunicateType.SelectionChanged += (s, e) =>
            {
                if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, MQTTPG.CommunicateType> KeyValue && KeyValue.Value is MQTTPG.CommunicateType communicateType)
                {
                    switch (communicateType)
                    {
                        case CommunicateType.Tcp:
                            TextBlockPGIP.Text ="IP";
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
            if (ComboxPGType.SelectedItem is KeyValuePair<string, MQTTPG.PGType > KeyValue && KeyValue.Value is MQTTPG.PGType pGType)
            {
                if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, MQTTPG.CommunicateType> KeyValue1 && KeyValue1.Value is MQTTPG.CommunicateType communicateType)
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
            if (ComboxPGCommunicateType.SelectedItem is KeyValuePair<string, MQTTPG.CommunicateType> KeyValue1 && KeyValue1.Value is MQTTPG.CommunicateType communicateType)
            {
                int port;
                if (!int.TryParse(TextBoxPGPort.Text,out port))
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
