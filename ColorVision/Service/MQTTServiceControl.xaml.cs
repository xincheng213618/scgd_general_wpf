using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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

namespace ColorVision.Service
{
    /// <summary>
    /// MQTTServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTServiceControl : UserControl
    {
        public MQTTService MQTTService { get; set; }
        public ServiceControl ServiceControl { get; set; }

        public MQTTServiceControl(MQTTService mQTTService)
        {
            this.MQTTService = mQTTService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceControl.GetInstance();
            this.DataContext = MQTTService;
            TextBox_Type.ItemsSource = MQTTService.Parent.VisualChildren;
            TextBox_Type.SelectedItem = MQTTService;
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Type.SelectedItem is MQTTService mQTTService)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTService.SysResourceModel.Type, mQTTService.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                ServiceControl.ResourceService.Save(sysResource);
                int pkId = sysResource.GetPK();
                if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                    mQTTService.AddChild(new MQTTDevice(model));

            }
        }
    }
}
