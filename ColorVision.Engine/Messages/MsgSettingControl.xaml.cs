using ColorVision.UI.Configs;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Messages
{
    public class MQTTMsgProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "MQTT",
                                Description =  "MQTT",
                                Type = ConfigSettingType.TabItem,
                                Source = MsgConfig.Instance,
                                UserControl = new MsgSettingControl()
                            }
            };
        }
    }


    /// <summary>
    /// MsgSettingControl.xaml 的交互逻辑
    /// </summary>
    public partial class MsgSettingControl : UserControl
    {
        public MsgSettingControl()
        {
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = MsgConfig.Instance;
        }

        private void Button_Click_1(object sender, System.Windows.RoutedEventArgs e)
        {
            MsgConfig.Instance.MsgRecords.Clear();
            MessageBox.Show("MQTT历史记录清理完毕", "ColorVision");
        }


    }
}
