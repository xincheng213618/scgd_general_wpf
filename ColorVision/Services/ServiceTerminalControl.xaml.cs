using ColorVision.Device;
using ColorVision.Device.Camera;
using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Device.Sensor;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services
{
    /// <summary>
    /// ServiceTerminalControl.xaml 的交互逻辑
    /// </summary>
    public partial class ServiceTerminalControl : UserControl
    {
        public ServiceTerminal MQTTService { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public ServiceTerminalControl(ServiceTerminal mQTTService)
        {
            this.MQTTService = mQTTService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceManager.GetInstance();
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

        private SysResourceModel? saveConfigInfo(BaseDeviceConfig deviceConfig, SysResourceModel sysResource)
        {
            deviceConfig.Name = TextBox_Name.Text;
            deviceConfig.Code = TextBox_Code.Text;

            deviceConfig.SendTopic = MQTTService.Config.SendTopic;
            deviceConfig.SubscribeTopic = MQTTService.Config.SubscribeTopic;
            sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
            ServiceControl.ResourceService.Save(sysResource);
            int pkId = sysResource.GetPK();
            if (pkId > 0 && ServiceControl.ResourceService.GetMasterById(pkId) is SysResourceModel model) return model;
            else return null;
        }
        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (!MQTT.Util.IsInvalidPath(TextBox_Name.Text, "资源名称") || !MQTT.Util.IsInvalidPath(TextBox_Code.Text, "资源标识"))
                return;

            


            if (TextBox_Type.SelectedItem is ServiceTerminal serviceTerminal)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, serviceTerminal.SysResourceModel.Type, serviceTerminal.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                if (serviceTerminal.Type == ServiceType.Camera)
                {
                    CameraConfig cameraConfig1 = new CameraConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text,
                        CameraType = CameraType.LV_Q,
                        TakeImageMode = TakeImageMode.Measure_Normal,
                        ImageBpp = ImageBpp.bpp8
                        
                    };
                    SysResourceModel model = saveConfigInfo(cameraConfig1, sysResource);
                    if (model != null)
                    {
                        if (serviceTerminal.BaseService is CameraService cameraService)
                        {
                            serviceTerminal.AddChild(new DeviceCamera(model, cameraService));
                        }
                    }
                }
                else if (serviceTerminal.Type == ServiceType.PG)
                {
                    PGConfig pGConfig = new PGConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text
                    };
                    SysResourceModel model = saveConfigInfo(pGConfig, sysResource);
                    if (model != null)
                        serviceTerminal.AddChild(new DevicePG(model));
                }
                else if (serviceTerminal.Type == ServiceType.Spectum)
                {
                    SpectrumConfig config = new SpectrumConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text
                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        serviceTerminal.AddChild(new DeviceSpectrum(model));
                }

                else if (serviceTerminal.Type == ServiceType.SMU)
                {
                    SMUConfig config = new SMUConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text
                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        serviceTerminal.AddChild(new DeviceSMU(model));
                }
                else if (serviceTerminal.Type == ServiceType.Sensor)
                {
                    SensorConfig config = new SensorConfig
                    {
                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        serviceTerminal.AddChild(new DeviceSpectrum(model));
                }
                else if (serviceTerminal.Type == ServiceType.FileServer)
                {
                    FileServerConfig config = new FileServerConfig
                    {

                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        serviceTerminal.AddChild(new DeviceFileServer(model));
                }

                MessageBox.Show("添加资源成功");
                MQTTCreate.Visibility = Visibility.Collapsed;
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTService.Config.SubscribeTopic = MQTTService.SysResourceModel.TypeCode + "/STATUS/" + MQTTService.SysResourceModel.Code;
            MQTTService.Config.SendTopic = MQTTService.SysResourceModel.TypeCode + "/CMD/" + MQTTService.SysResourceModel.Code;

            foreach (var item in MQTTService.VisualChildren)
            {
                if(item is BaseChannel mQTTDevice)
                {
                    mQTTDevice.SendTopic = MQTTService.Config.SendTopic;
                    mQTTDevice.SubscribeTopic = MQTTService.Config.SubscribeTopic;
                    mQTTDevice.Save();
                }
            }
            MQTTService.Save();

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
            if (MQTTService.BaseService is CameraService cameraService)
            {
                TextBox_Code.ItemsSource = cameraService.DevicesSN;
                TextBox_Name.ItemsSource = cameraService.DevicesSN;
            }
        }

        private void ReFresh_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
