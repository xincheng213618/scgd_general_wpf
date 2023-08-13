using ColorVision.MQTT;
using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;

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

            if (MQTTServiceKind.VisualChildren.Count == 0)
                ListViewService.Visibility = Visibility.Collapsed;
            ListViewService.ItemsSource = MQTTServiceKind.VisualChildren;

            MQTTServiceKind.VisualChildren.CollectionChanged += (s, e) =>
            {
                if (MQTTServiceKind.VisualChildren.Count == 0)
                {
                    ListViewService.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ListViewService.Visibility = Visibility.Visible;
                }
            };
        }


        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (!MQTT.Util.IsInvalidPath(TextBox_Name.Text, "服务名称") || !MQTT.Util.IsInvalidPath(TextBox_Code.Text, "服务标识"))
                return;

            if (TextBox_Type.SelectedItem is MQTTServiceKind mQTTServiceKind)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTServiceKind.SysDictionaryModel.Value, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                ServiceConfig serviceConfig = new ServiceConfig
                {
                    SendTopic = mQTTServiceKind.SysDictionaryModel.Code + "/" + "CMD/" + sysResource.Code,
                    SubscribeTopic = mQTTServiceKind.SysDictionaryModel.Code + "/" + "STATUS/" + sysResource.Code
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
 