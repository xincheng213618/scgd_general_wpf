using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.FileServer;
using ColorVision.Engine.Services.Devices.FlowDevice;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services
{

    public class ServiceManager
    {
        private static ServiceManager _instance;
        private static readonly object _locker = new();
        public static ServiceManager GetInstance() { lock (_locker) { return _instance ??= new ServiceManager(); } }
        public static UserConfig UserConfig => UserConfig.Instance;

        public ObservableCollection<TypeService> TypeServices { get; set; } = new ObservableCollection<TypeService>();
        public ObservableCollection<TerminalService> TerminalServices { get; set; } = new ObservableCollection<TerminalService>();
        public ObservableCollection<DeviceService> DeviceServices { get; set; } = new ObservableCollection<DeviceService>();

        public IEnumerable<DeviceService> GetImageSourceServices() => DeviceServices.Where(item => item is DeviceCamera || item is DeviceCalibration|| item is DeviceAlgorithm);

        public ObservableCollection<GroupResource> GroupResources { get; set; } = new ObservableCollection<GroupResource>();
        public ObservableCollection<DeviceService> LastGenControl { get; set; } = new ObservableCollection<DeviceService>();

        public event EventHandler ServiceChanged;

        public ServiceManager()
        {
            if (MySqlControl.GetInstance().IsConnect)
                LoadServices();
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => LoadServices();
        }

        public void GenControl(ObservableCollection<DeviceService> MQTTDevices)
        {
            LastGenControl = MQTTDevices;
            var nameToIndexMap = DisPlayManagerConfig.Instance.PlayControls;

            DisPlayManager.GetInstance().IDisPlayControls.Clear();
            DisPlayManager.GetInstance().IDisPlayControls.Insert(0, DisplayFlow.GetInstance());
            foreach (var item in MQTTDevices)
            {
                if (item is DeviceService device)
                {
                    if (device.GetDisplayControl() is IDisPlayControl disPlayControl)
                    {
                        DisPlayManager.GetInstance().IDisPlayControls.Add(disPlayControl);
                    }
                }
            }

            if (ServicesConfig.Instance.IsRetorePlayControls)
            {
                DisPlayManager.GetInstance().IDisPlayControls.Sort((a, b) =>
                {
                    if (nameToIndexMap.TryGetValue(a.DisPlayName, out int indexA) && nameToIndexMap.TryGetValue(b.DisPlayName, out int indexB))
                    {
                        return indexA.CompareTo(indexB);
                    }
                    else if (nameToIndexMap.ContainsKey(a.DisPlayName))
                    {
                        return -1; // a should come before b
                    }
                    else if (nameToIndexMap.ContainsKey(b.DisPlayName))
                    {
                        return 1; // b should come before a
                    }
                    return 0; // keep original order if neither a nor b are in playControls
                });
            }
        }
        /// <summary>
        /// 生成显示空间
        /// </summary>
        public void GenDeviceDisplayControl()
        {
            var nameToIndexMap = DisPlayManagerConfig.Instance.PlayControls;

            LastGenControl = new ObservableCollection<DeviceService>();
            DisPlayManager.GetInstance().IDisPlayControls.Clear();
            DisPlayManager.GetInstance().IDisPlayControls.Insert(0, DisplayFlow.GetInstance());
            foreach (var serviceKind in TypeServices)
            {
                foreach (var service in serviceKind.VisualChildren)
                {
                    foreach (var item in service.VisualChildren)  
                    {
                        if (item is DeviceService device)
                        {
                            LastGenControl.Add(device);
                            if (device.GetDisplayControl() is IDisPlayControl disPlayControl)
                            {
                                DisPlayManager.GetInstance().IDisPlayControls.Add(disPlayControl);
                            }
                        }
                    }
                }
            }
            LastGenControl = DeviceServices;

            if (ServicesConfig.Instance.IsRetorePlayControls)
            {
                DisPlayManager.GetInstance().IDisPlayControls.Sort((a, b) =>
                {
                    if (nameToIndexMap.TryGetValue(a.DisPlayName, out int indexA) && nameToIndexMap.TryGetValue(b.DisPlayName, out int indexB))
                    {
                        return indexA.CompareTo(indexB);
                    }
                    else if (nameToIndexMap.ContainsKey(a.DisPlayName))
                    {
                        return -1; // a should come before b
                    }
                    else if (nameToIndexMap.ContainsKey(b.DisPlayName))
                    {
                        return 1; // b should come before a
                    }
                    return 0; // keep original order if neither a nor b are in playControls
                });
            }


            for (int i = 0; i < DisPlayManager.GetInstance().IDisPlayControls.Count; i++)
                DisPlayManagerConfig.Instance.PlayControls[DisPlayManager.GetInstance().IDisPlayControls[i].DisPlayName] = i;
        }
        private Dictionary<string, List<MQTTServiceBase>> svrDevices = new();

        public void LoadServices()
        {
            LastGenControl?.Clear();
            List<SysDictionaryModel> SysDictionaryModels = SysDictionaryDao.Instance.GetAllByPid(1);

            TypeServices.Clear();
            foreach (var sysDictionaryModel in SysDictionaryModels)
            {

                TypeService typeService = new();
                typeService.Name = sysDictionaryModel.Name ?? "未配置";
                typeService.SysDictionaryModel = sysDictionaryModel;
                TypeServices.Add(typeService);
            }


            TerminalServices.Clear();
            svrDevices.Clear();

            List<SysResourceModel> sysResourceModelServices = VSysResourceDao.Instance.GetServices(UserConfig.TenantId);
            foreach (var typeService1 in TypeServices)
            {
                var sysResourceModels = sysResourceModelServices.FindAll((x) => x.Type == (int)typeService1.ServiceTypes);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    TerminalService terminalService = new TerminalService(sysResourceModel);
                    string svrKey = GetServiceKey(sysResourceModel.TypeCode ?? string.Empty, sysResourceModel.Code ?? string.Empty);
                   
                    if (svrDevices.TryGetValue(svrKey, out var list ))
                    {
                        list.Clear();
                    }
                    else
                    {
                        svrDevices.Add(svrKey, new List<MQTTServiceBase>());
                    }

                    typeService1.AddChild(terminalService);
                    TerminalServices.Add(terminalService);
                }
            }

            List<SysDeviceModel> sysResourceModelDevices = VSysDeviceDao.Instance.GetAll(UserConfig.TenantId);
            DeviceServices.Clear();

            foreach (var terminalService in TerminalServices)
            {
                var sysResourceModels = sysResourceModelDevices.FindAll((x) => x.Pid == (int)terminalService.SysResourceModel.Id);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    DeviceService deviceService =null;

                    switch ((ServiceTypes)sysResourceModel.Type)
                    {
                        case ServiceTypes.Camera:
                            deviceService = new DeviceCamera(sysResourceModel);
                            break;
                        case ServiceTypes.PG:
                            deviceService = new DevicePG(sysResourceModel);
                            break;
                        case ServiceTypes.Spectrum:
                            deviceService = new DeviceSpectrum(sysResourceModel);
                            break;
                        case ServiceTypes.SMU:
                            deviceService = new DeviceSMU(sysResourceModel);
                            break;
                        case ServiceTypes.Sensor:
                            deviceService = new DeviceSensor(sysResourceModel);
                            break;
                        case ServiceTypes.FileServer:
                            deviceService = new DeviceFileServer(sysResourceModel);
                            break;
                        case ServiceTypes.Algorithm:
                            deviceService = new DeviceAlgorithm(sysResourceModel);
                            break;
                        case ServiceTypes.Calibration:
                            deviceService = new DeviceCalibration(sysResourceModel);
                            break;
                        case ServiceTypes.CfwPort:
                            deviceService = new DeviceCfwPort(sysResourceModel);
                            break;
                        case ServiceTypes.Motor:
                            deviceService = new DeviceMotor(sysResourceModel);
                            break;
                        case ServiceTypes.ThirdPartyAlgorithms:
                            deviceService = new DeviceThirdPartyAlgorithms(sysResourceModel);
                            break;
                        case ServiceTypes.Flow:
                            deviceService = new DeviceFlowDevice(sysResourceModel);
                            break;
                        default:
                            break;
                    }

                    if (deviceService != null  )
                    {
                        terminalService.AddChild(deviceService);
                        DeviceServices.Add(deviceService);

                        MQTTServiceBase svrObj = deviceService.GetMQTTService();

                        string svrKey = GetServiceKey(terminalService.SysResourceModel.TypeCode ?? string.Empty, terminalService.SysResourceModel.Code ?? string.Empty);
                        if (svrObj != null && svrDevices.TryGetValue(svrKey, out var list))
                        {
                            svrObj.ServiceName = terminalService.SysResourceModel.Code ?? string.Empty;
                            list.Add(svrObj);
                        }
                    }



                }
            }

            GroupResources.Clear();


            foreach (var deviceService in DeviceServices)
            {
                List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetResourceItems(deviceService.SysResourceModel.Id, UserConfig.TenantId);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ServiceTypes.Group)
                    {
                        GroupResource groupResource = new(sysResourceModel);
                        deviceService.AddChild(groupResource);
                        GroupResources.Add(groupResource);
                    }
                   else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 40)
                    {
                        CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                    else
                    {
                        ServiceFileBase calibrationResource = new(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                }
            }

            foreach (var groupResource in GroupResources)
            {
                LoadgroupResource(groupResource);
            }
            ServiceChanged?.Invoke(this, new EventArgs());
        }

        public void LoadgroupResource(GroupResource groupResource)
        {
            SysResourceDao.Instance.CreatResourceGroup();
            List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetGroupResourceItems(groupResource.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                if (sysResourceModel.Type == (int)ServiceTypes.Group)
                {
                    GroupResource groupResource1 = new(sysResourceModel);
                    LoadgroupResource(groupResource1);
                    groupResource.AddChild(groupResource);
                    GroupResources.Add(groupResource);
                }
                else if (30<=sysResourceModel.Type && sysResourceModel.Type <= 40)
                {
                    CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
                else
                {
                    ServiceBase calibrationResource = new(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
            }
        }

        private static string GetServiceKey(string svrType, string svrCode)
        {
            return svrType + ":" + svrCode;
        }

    }
}
