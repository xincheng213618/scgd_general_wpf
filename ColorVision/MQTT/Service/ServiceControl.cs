using ColorVision.MQTT.Camera;
using ColorVision.MQTT.PG;
using ColorVision.MQTT.Sensor;
using ColorVision.MQTT.SMU;
using ColorVision.MQTT.Spectrum;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using ColorVision.Template;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ColorVision.MQTT.Service
{
    public class ServiceControl
    {
        private static ServiceControl _instance;
        private static readonly object _locker = new();
        public static ServiceControl GetInstance() { lock (_locker) { return _instance ??= new ServiceControl(); } }

        public ObservableCollection<MQTTServiceKind> MQTTServices { get; set; }

        public SysResourceService ResourceService { get; set; }
        public SysDictionaryService DictionaryService { get; set; }

        public UserConfig UserConfig { get; set; }

        public ServiceControl()
        {
            ResourceService = new SysResourceService();
            DictionaryService = new SysDictionaryService();
            MQTTServices = new ObservableCollection<MQTTServiceKind>();
            UserConfig = GlobalSetting.GetInstance().SoftwareConfig.UserConfig;

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Reload();
            Reload();
        }


        public void GenContorl()
        {
            MQTTManager MQTTManager = MQTTManager.GetInstance();
            MQTTManager.MQTTStackPanel.Children.Clear();
            foreach (var mQTTServiceKind in MQTTServices)
            {
                foreach (var mQTTService in mQTTServiceKind.VisualChildren)
                {
                    foreach (var item in mQTTService.VisualChildren)
                    {
                        if (item is DeviceCamera deviceCamera)
                        {
                            MQTTManager.Services.Add(deviceCamera.CameraService);
                            MQTTCameraControl1 mQTTCameraControl = new MQTTCameraControl1(deviceCamera.CameraService);
                            deviceCamera.CameraService.FileHandler += (s, e) =>
                            {
                                MessageBox.Show(e);
                            };
                            MQTTManager.MQTTStackPanel.Children.Add(mQTTCameraControl);

                        }
                        else if (item is DevicePG devicePG)
                        {
                            MQTTManager.Services.Add(devicePG.PGService);
                            MQTTPGControl mQTTPGControl = new MQTTPGControl(devicePG.PGService);
                            MQTTManager.MQTTStackPanel.Children.Add(mQTTPGControl);
                        }
                        else if (item is DeviceSpectrum deviceSpectrum)
                        {
                            MQTTManager.Services.Add(deviceSpectrum.SpectrumService);
                            MQTTSpectrumControl mQTTSpectrumControl = new MQTTSpectrumControl(deviceSpectrum.SpectrumService);
                            MQTTManager.MQTTStackPanel.Children.Add(mQTTSpectrumControl);
                        }
                        else if (item is DeviceSMU smu)
                        {
                            MQTTManager.Services.Add(smu.SMUService);
                            MQTTSMUControl mQTTSMUControl = new MQTTSMUControl(smu.SMUService);
                            MQTTManager.MQTTStackPanel.Children.Add(mQTTSMUControl);
                        }
                        else if (item is DeviceSensor deviceSensor)
                        {
                            HandyControl.Controls.Growl.Info("SensorService开发中");
                        }

                    }
                }
            }
        }


        public void Reload()
        {
            MQTTServices.Clear();
            List<SysResourceModel> Services = ResourceService.GetAllServices(UserConfig.TenantId);
            List<SysResourceModel> devices = ResourceService.GetAllDevices(UserConfig.TenantId);

            foreach (var item in DictionaryService.GetAllServiceType())
            {
                MQTTServiceKind mQTTServicetype = new MQTTServiceKind();
                mQTTServicetype.Name = item.Name ?? string.Empty;
                mQTTServicetype.SysDictionaryModel = item;
                foreach (var service in Services)
                {
                    if (service.Type == item.Value)
                    {
                        MQTTService mQTTService = new MQTTService(service);

                        foreach (var device in devices)
                        {
                            if (device.Pid == service.Id)
                            {

                                switch ((MQTTDeviceType)device.Type)
                                {
                                    case MQTTDeviceType.Camera:
                                        mQTTService.AddChild(new DeviceCamera(device));
                                        break;
                                    case MQTTDeviceType.PG:
                                        mQTTService.AddChild(new DevicePG(device));
                                        break;
                                    case MQTTDeviceType.Spectum:
                                        mQTTService.AddChild(new DeviceSpectrum(device));
                                        break;
                                    case MQTTDeviceType.SMU:
                                        mQTTService.AddChild(new DeviceSMU(device));
                                        break;
                                    case MQTTDeviceType.Sensor:
                                        mQTTService.AddChild(new DeviceSensor(device));
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        mQTTServicetype.AddChild(mQTTService);
                    }
                }
                MQTTServices.Add(mQTTServicetype);
            }

        }


    }
}
