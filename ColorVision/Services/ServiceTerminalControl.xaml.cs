using ColorVision.Device;
using ColorVision.Device.Camera;
using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm;
using ColorVision.Services.Device.Calibration;
using ColorVision.Services.Device.Camera;
using ColorVision.Services.Device.FilterWheel;
using ColorVision.Services.Device.Motor;
using ColorVision.Services.Device.Sensor;
using cvColorVision;
using HandyControl.Tools.Extension;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
                if (serviceTerminal.ServicesCodes.Contains(TextBox_Code.Text))
                {
                    MessageBox.Show("设备标识已存在,不允许重复添加");
                    return;
                }
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, serviceTerminal.SysResourceModel.Type, serviceTerminal.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);


                SysResourceModel sysResourceModel;
                BaseDeviceConfig deviceConfig;
                switch (serviceTerminal.Type)
                {   
                    case ServiceType.Camera:
                        ConfigCamera cameraConfig1 = new ConfigCamera
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            CameraType = CameraType.LV_Q,
                            TakeImageMode = TakeImageMode.Measure_Normal,
                            ImageBpp = ImageBpp.bpp8

                        };
                        sysResourceModel = saveConfigInfo(cameraConfig1, sysResource);
                        if (sysResourceModel != null)
                        {
                            if (serviceTerminal.BaseService is ServiceCamera cameraService)
                            {
                                serviceTerminal.AddChild(new DeviceCamera(sysResourceModel, cameraService));
                            }
                        }
                        break;
                    case ServiceType.PG:
                        PGConfig pGConfig = new PGConfig
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysResourceModel = saveConfigInfo(pGConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DevicePG(sysResourceModel));
                        break;
                    case ServiceType.Spectrum:
                        deviceConfig = new SpectrumConfig
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceSpectrum(sysResourceModel));
                        break;
                    case ServiceType.SMU:
                        deviceConfig = new SMUConfig
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        SysResourceModel model = saveConfigInfo(deviceConfig, sysResource);
                        if (model != null)
                            serviceTerminal.AddChild(new DeviceSMU(model));
                        break;
                    case ServiceType.Sensor:
                        deviceConfig = new ConfigSensor
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceSensor(sysResourceModel));
                        break;
                    case ServiceType.FileServer:
                        deviceConfig = new FileServerConfig
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            Endpoint = "tcp://127.0.0.1:" + (Math.Abs(new Random().Next()) % 99 + 6500),
                            FileBasePath = "F:/img",
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceFileServer(sysResourceModel));
                        break;
                    case ServiceType.Algorithm:
                        deviceConfig = new AlgorithmConfig
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                            Endpoint = "tcp://127.0.0.1:" + (Math.Abs(new Random().Next()) % 99 + 6600),
                            FileBasePath = "F:/img/cvcie",
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceAlgorithm(sysResourceModel));
                        break;
                    case ServiceType.FilterWheel:
                        deviceConfig = new ConfigFilterWheel { 
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceFilterWheel(sysResourceModel));
                        break;
                    case ServiceType.Calibration:
                        deviceConfig = new ConfigCalibration
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceCalibration(sysResourceModel));
                        break;
                    case ServiceType.Motor:
                        deviceConfig = new ConfigMotor
                        {
                            ID = TextBox_Code.Text,
                            Name = TextBox_Name.Text,
                        };
                        sysResourceModel = saveConfigInfo(deviceConfig, sysResource);
                        if (sysResourceModel != null)
                            serviceTerminal.AddChild(new DeviceMotor(sysResourceModel));
                        break;
                    case ServiceType.FocusRing:
                        break;
                    case ServiceType.Flowtime:
                        break;
                    default:
                        break;
                };
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
            if (MQTTService.BaseService is BaseServiceBase baseServiceBase)
            {
                TextBox_Code.ItemsSource = baseServiceBase.DevicesSN;
                TextBox_Name.ItemsSource = baseServiceBase.DevicesSN;
            }
        }

        private void ReFresh_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            popup.IsOpen = true;
        }

        private void popup_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Popup popup)
                popup.IsOpen = false;

        }
    }
}
