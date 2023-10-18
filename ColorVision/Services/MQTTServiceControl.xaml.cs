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

        private SysResourceModel? saveConfigInfo(BaseDeviceConfig deviceConfig, SysResourceModel sysResource)
        {
            deviceConfig.Name = TextBox_Name.Text;
            deviceConfig.Code = TextBox_Code.Text;

            deviceConfig.SendTopic = MQTTService.ServiceConfig.SendTopic;
            deviceConfig.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
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


            if (TextBox_Type.SelectedItem is MQTTService mQTTService)
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTService.SysResourceModel.Type, mQTTService.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                if (mQTTService.Type == DeviceType.Camera)
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
                        mQTTService.AddChild(new DeviceCamera(model));
                }
                else if (mQTTService.Type == DeviceType.PG)
                {
                    PGConfig pGConfig = new PGConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text
                    };
                    SysResourceModel model = saveConfigInfo(pGConfig, sysResource);
                    if (model != null)
                        mQTTService.AddChild(new DevicePG(model));
                }
                else if (mQTTService.Type == DeviceType.Spectum)
                {
                    SpectrumConfig config = new SpectrumConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text
                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        mQTTService.AddChild(new DeviceSpectrum(model));
                }

                else if (mQTTService.Type == DeviceType.SMU)
                {
                    SMUConfig config = new SMUConfig
                    {
                        ID = TextBox_Code.Text,
                        Name = TextBox_Name.Text
                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        mQTTService.AddChild(new DeviceSMU(model));
                }
                else if (mQTTService.Type == DeviceType.Sensor)
                {
                    SensorConfig config = new SensorConfig
                    {
                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        mQTTService.AddChild(new DeviceSpectrum(model));
                }
                else if (mQTTService.Type == DeviceType.Image)
                {
                    FileServerConfig config = new FileServerConfig
                    {

                    };
                    SysResourceModel model = saveConfigInfo(config, sysResource);
                    if (model != null)
                        mQTTService.AddChild(new DeviceFileServer(model));
                }

                MessageBox.Show("添加资源成功");
                MQTTCreate.Visibility = Visibility.Collapsed;
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTService.ServiceConfig.SubscribeTopic = MQTTService.SysResourceModel.TypeCode + "/STATUS/" + MQTTService.SysResourceModel.Code;
            MQTTService.ServiceConfig.SendTopic = MQTTService.SysResourceModel.TypeCode + "/CMD/" + MQTTService.SysResourceModel.Code;

            foreach (var item in MQTTService.VisualChildren)
            {
                if(item is BaseChannel mQTTDevice)
                {
                    mQTTDevice.SendTopic = MQTTService.ServiceConfig.SendTopic;
                    mQTTDevice.SubscribeTopic = MQTTService.ServiceConfig.SubscribeTopic;
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
        }
    }
}
