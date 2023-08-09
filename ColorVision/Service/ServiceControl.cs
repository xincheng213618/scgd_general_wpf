using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using ColorVision.Template;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;

namespace ColorVision.Service
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

            MySqlControl.GetInstance().MySqlConnectChanged +=(s,e)=> Reload();
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
                            MQTTCamera Camera1 = new MQTTCamera(deviceCamera.CameraConfig);
                            MQTTManager.MQTTCameras.Add(Camera1);
                            MQTTManager.ServiceHeartbeats.Add(Camera1);
                        }

                        if (mQTTServiceKind.SysDictionaryModel.Value == 3)
                        {
                            MQTTSpectrum mQTTSpectrum = new MQTTSpectrum();
                            MQTTManager.MQTTSpectrums.Add(mQTTSpectrum);
                        }
                        else if (mQTTServiceKind.SysDictionaryModel.Value == 4)
                        {
                            MQTTVISource mQTTVISource = new MQTTVISource();
                            MQTTManager.MQTTVISources.Add(mQTTVISource);
                        }
                        else if (mQTTServiceKind.SysDictionaryModel.Value == 2)
                        {
                            MQTTPG mQTTPG = new MQTTPG();
                            MQTTManager.MQTTPGs.Add(mQTTPG);
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
                        MQTTManager.GetInstance().ServiceHeartbeats.Add(new HeartbeatService(mQTTService.ServiceConfig));

                        foreach (var device in devices)
                        {
                            if (device.Pid == service.Id)
                            {
                                if (device.Type == 1)
                                {
                                    MQTTDeviceCamera camera = new MQTTDeviceCamera(device);
                                    mQTTService.AddChild(camera);
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
