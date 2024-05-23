using ColorVision.UI.Configs;
using ColorVision.UI.HotKey;
using System.Collections.Generic;
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

namespace ColorVision.Services.Msg
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
