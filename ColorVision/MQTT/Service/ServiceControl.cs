using ColorVision.MQTT.Camera;
using ColorVision.MQTT.PG;
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
using System.Windows.Automation;
using System.Windows.Media.Media3D;

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
            foreach (var item in MQTTManager.MQTTCameras)
                item.Dispose();
            MQTTManager.MQTTCameras.Clear();

            foreach (var item in MQTTManager.MQTTSpectrums)
                item.Dispose();
            MQTTManager.MQTTSpectrums.Clear();


            foreach (var item in MQTTManager.MQTTVISources)
                item.Dispose();
            MQTTManager.MQTTVISources.Clear();


            foreach (var item in MQTTManager.MQTTPGs)
                item.Dispose();
            MQTTManager.MQTTPGs.Clear();

            MQTTManager.ServiceHeartbeats.Clear();

            foreach (var mQTTServiceKind in MQTTServices)
            {
                foreach (var mQTTService in mQTTServiceKind.VisualChildren)
                {
                    foreach (var item in mQTTService.VisualChildren)
                    {
                        if (item is MQTTDeviceCamera deviceCamera)
                        {
                            MQTTCamera Camera1 = new MQTTCamera(deviceCamera.Config);
                            MQTTManager.MQTTCameras.Add(Camera1);
                            MQTTManager.ServiceHeartbeats.Add(Camera1);
                        }
                        else if (item is MQTTDevicePG devicePG)
                        {
                            MQTTPG mQTTPG = new MQTTPG(devicePG.Config);
                            MQTTManager.MQTTPGs.Add(mQTTPG);
                            MQTTManager.ServiceHeartbeats.Add(mQTTPG);
                        }
                        else if (item is MQTTDeviceSpectrum deviceSpectrum)
                        {
                            SpectrumService mQTTSpectrum = new SpectrumService(deviceSpectrum);
                            MQTTManager.MQTTSpectrums.Add(mQTTSpectrum);
                            MQTTManager.ServiceHeartbeats.Add(mQTTSpectrum);
                        }
                        else if (item is MQTTDeviceSMU smu)
                        {
                            SMUService mQTTVISource = new SMUService(smu);
                            MQTTManager.MQTTVISources.Add(mQTTVISource);
                        }

                    }
                }
            }
            MQTTManager.Reload();
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
                                if (device.Type == (int)MQTTDeviceType.Camera)
                                {
                                    MQTTDeviceCamera camera = new MQTTDeviceCamera(device);
                                    mQTTService.AddChild(camera);
                                }
                                else if (device.Type == (int)MQTTDeviceType.PG)
                                {
                                    MQTTDevicePG pg = new MQTTDevicePG(device);
                                    mQTTService.AddChild(pg);
                                }
                                else if (device.Type == (int)MQTTDeviceType.Spectum)
                                {
                                    MQTTDeviceSpectrum spectrum = new MQTTDeviceSpectrum(device);
                                    mQTTService.AddChild(spectrum);
                                }
                                else if (device.Type == (int)MQTTDeviceType.SMU)
                                {
                                    MQTTDeviceSMU smu = new MQTTDeviceSMU(device);
                                    mQTTService.AddChild(smu);
                                }
                                else
                                {
                                    MQTTDevice other_device = new MQTTDevice(device);
                                    mQTTService.AddChild(other_device);
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
