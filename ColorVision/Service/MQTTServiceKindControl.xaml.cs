using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using ColorVision.Template;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace ColorVision.Service
{
    /// <summary>
    /// MQTTServiceKindControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTServiceKindControl : UserControl
    {
        public MQTTServiceKind MQTTServiceKind { get; set; }
        public ServiceControl ServiceControl { get; set; }
        public MQTTServiceKindControl(MQTTServiceKind mQTTServiceKind)
        {
            this.MQTTServiceKind = mQTTServiceKind;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceControl.GetInstance();
            this.DataContext = MQTTServiceKind;
            TextBox_Type.ItemsSource = ServiceControl.MQTTServices;
            TextBox_Type.SelectedItem = MQTTServiceKind;
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Type.SelectedItem is MQTTServiceKind mQTTServiceKind)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTServiceKind.SysDictionaryModel.Value, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                ServiceConfig serviceConfig = new ServiceConfig
                {
                    SendTopic = SendTopicAdd.Text,
                    SubscribeTopic = SubscribeTopicAdd.Text
                };
                sysResource.Value = JsonConvert.SerializeObject(serviceConfig);
                ServiceControl.ResourceService.Save(sysResource);
                int pkId = sysResource.GetPK();
                if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                    mQTTServiceKind.AddChild(new MQTTService(model));
            }

        }
    }
}
