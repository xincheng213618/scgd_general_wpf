using ColorVision.MQTT;
using ColorVision.MQTT.Camera;
using ColorVision.MQTT.PG;
using ColorVision.MQTT.Sensor;
using ColorVision.MQTT.Service;
using ColorVision.MQTT.SMU;
using ColorVision.MQTT.Spectrum;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.MQTT.Service
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

            if (MQTTService.Type == DeviceType.Camera)
            {
                if (HeartbeatService.ServicesDevices.TryGetValue(MQTTService.ServiceConfig.SubscribeTopic, out ObservableCollection<string> list))
                {
                    foreach (var item in list)
                    {
                        MQTTCreateStackPanel.Children.Add(new TextBox() { Text =item ,IsReadOnly =true});
                    }
                }
            }

        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {

            if (!MQTT.Util.IsInvalidPath(TextBox_Name.Text, "资源名称") || !MQTT.Util.IsInvalidPath(TextBox_Code.Text, "资源标识"))
                return;


            if (TextBox_Type.SelectedItem is MQTTService mQTTService)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTService.SysResourceModel.Type, mQTTService.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                
                if (mQTTService.Type == DeviceType.Camera)
                {
                    CameraConfig cameraConfig1 = new CameraConfig
                    {
                        ID = "e29b14429bc375b1",
                        CameraType = CameraType.LVQ,
                        TakeImageMode = TakeImageMode.Normal,
                        ImageBpp = 8
                    };
                    cameraConfig1.Name = TextBox_Name.Text;
                    cameraConfig1.Code = TextBox_Code.Text;

                    cameraConfig1.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    cameraConfig1.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
                    sysResource.Value = JsonConvert.SerializeObject(cameraConfig1);
                    ServiceControl.ResourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                        mQTTService.AddChild(new DeviceCamera(model));
                }
                else if (mQTTService.Type == DeviceType.PG)
                {
                    PGConfig pGConfig = new PGConfig
                    {
                        ID = "e29b14429bc375b1",
                    };
                    pGConfig.Name = TextBox_Name.Text;
                    pGConfig.Code = TextBox_Code.Text;

                    pGConfig.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    pGConfig.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
                    sysResource.Value = JsonConvert.SerializeObject(pGConfig);
                    ServiceControl.ResourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                        mQTTService.AddChild(new DevicePG(model));
                }
                else if (mQTTService.Type == DeviceType.Spectum)
                {
                    SpectrumConfig config = new SpectrumConfig
                    {
                        ID = "e29b14429bc375b1",
                    };
                    config.Name = TextBox_Name.Text;
                    config.Code = TextBox_Code.Text;
                    config.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    config.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
                    sysResource.Value = JsonConvert.SerializeObject(config);
                    ServiceControl.ResourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                        mQTTService.AddChild(new DeviceSpectrum(model));
                }

                else if (mQTTService.Type == DeviceType.SMU)
                {
                    SMUConfig config = new SMUConfig
                    {
                        ID = "e29b14429bc375b1",
                    };
                    config.Name = TextBox_Name.Text;
                    config.Code = TextBox_Code.Text;
                    config.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    config.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
                    sysResource.Value = JsonConvert.SerializeObject(config);
                    ServiceControl.ResourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                        mQTTService.AddChild(new DeviceSpectrum(model));
                }else if (mQTTService.Type == DeviceType.Sensor)
                {
                    SensorConfig config = new SensorConfig
                    {
                        ID = "e29b14429bc375b1",
                    };
                    config.Name = TextBox_Name.Text;
                    config.Code = TextBox_Code.Text;
                    config.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    config.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
                    sysResource.Value = JsonConvert.SerializeObject(config);
                    ServiceControl.ResourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model)
                        mQTTService.AddChild(new DeviceSpectrum(model));
                }

                MessageBox.Show("添加资源成功");
                MQTTCreate.Visibility = Visibility.Collapsed;
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in MQTTService.VisualChildren)
            {
                if(item is MQTTDevice mQTTDevice)
                {
                    mQTTDevice.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    mQTTDevice.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
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
