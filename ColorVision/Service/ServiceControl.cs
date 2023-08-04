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

            List<SysResourceModel> Services = ResourceService.GetAllServices(UserConfig.TenantId);
            List<SysResourceModel> devices = ResourceService.GetAllDevices(UserConfig.TenantId);

            foreach (var item in DictionaryService.GetAllServiceType())
            {
                MQTTServiceKind mQTTServicetype = new MQTTServiceKind();
                mQTTServicetype.Name = item.Name ?? string.Empty;
                mQTTServicetype.SysDictionaryModel = item;
                foreach (var item1 in Services)
                {
                    if (item1.Type == item.Value)
                    {
                        MQTTService mQTTService = new MQTTService(item1);
                        foreach (var item2 in devices)
                        {
                            if (item2.Pid == item1.Id)
                            {
                                if (item2.Type == 1)
                                {
                                    MQTTDeviceCamera device = new MQTTDeviceCamera(mQTTService.ServiceConfig, item2);
                                    mQTTService.AddChild(device);
                                }
                                else
                                {
                                    MQTTDevice device = new MQTTDevice(item2);
                                    mQTTService.AddChild(device);
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
