using ColorVision.MQTT;
using ColorVision.MQTT.Config;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using ColorVision.Template;
using HslCommunication.MQTT;
using Newtonsoft.Json;
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

            if (MQTTService.VisualChildren.Count == 0)
                ListViewService.Visibility = Visibility.Collapsed;
            ListViewService.ItemsSource = MQTTService.VisualChildren;

            MQTTService.VisualChildren.CollectionChanged += (s, e) =>
            {
                if (MQTTService.VisualChildren.Count == 0)
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

            if (!Util.IsInvalidPath(TextBox_Name.Text, "资源名称") || !Util.IsInvalidPath(TextBox_Code.Text, "资源标识"))
                return;


            if (TextBox_Type.SelectedItem is MQTTService mQTTService)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTService.SysResourceModel.Type, mQTTService.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                
                CameraConfig cameraConfig1 = new CameraConfig
                {
                    ID = "e29b14429bc375b1",
                    CameraType = CameraType.LVQ,
                    TakeImageMode = TakeImageMode.Normal,
                    ImageBpp = 8
                };
                cameraConfig1.Name = TextBox_Name.Text;
                cameraConfig1.SendTopic = MQTTService.ServiceConfig.SendTopic;
                cameraConfig1.SendTopic = MQTTService.ServiceConfig.SubscribeTopic;

                sysResource.Value = JsonConvert.SerializeObject(cameraConfig1);
                ServiceControl.ResourceService.Save(sysResource);
                int pkId = sysResource.GetPK();
                if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                    mQTTService.AddChild(new MQTTDeviceCamera(model));


                MessageBox.Show("添加资源成功");
                MQTTCreate.Visibility = Visibility.Collapsed;
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in MQTTService.VisualChildren)
            {
                if(item is MQTTDeviceCamera mQTTDeviceCamera)
                {
                    mQTTDeviceCamera.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    mQTTDeviceCamera.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
                }
            }

            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTCreate.Visibility = MQTTCreate.Visibility == Visibility.Visible? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
