using ColorVision.Database;
using ColorVision.Engine.Rbac;
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
using ColorVision.UI;
using SqlSugar;
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
        public static RbacManagerConfig UserConfig => RbacManagerConfig.Instance;

        public static SqlSugarClient Db => MySqlControl.GetInstance().DB;

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
            var nameToIndexMap = DisPlayManagerConfig.Instance.StoreIndex;

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

            DisPlayManager.GetInstance().RestoreControl();
        }
        /// <summary>
        /// 生成显示空间
        /// </summary>
        public void GenDeviceDisplayControl()
        {
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

            DisPlayManager.GetInstance().RestoreControl();
        }


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

            foreach (var typeService1 in TypeServices)
            {
#pragma warning disable CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
                List<SysResourceModel> sysResourceModelServices = SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type",(int)typeService1.ServiceTypes }, { "pid",null} ,{ "tenant_id", UserConfig.TenantId }, { "is_delete", 0} });
#pragma warning restore CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
                foreach (var sysResourceModel in sysResourceModelServices)
                {
                    TerminalService terminalService = new TerminalService(sysResourceModel);
                    typeService1.AddChild(terminalService);
                    TerminalServices.Add(terminalService);
                }
            }

            DeviceServices.Clear();

            foreach (var terminalService in TerminalServices)
            {
                var sysResourceModels = Db.Queryable<SysResourceModel>().Where(it => it.Pid == terminalService.SysResourceModel.Id && it.TenantId == UserConfig.TenantId && it.IsEnable == true && it.IsDelete == false).ToList();
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
                        case ServiceTypes.FilterWheel:
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
                    }
                }
            }

            GroupResources.Clear();


            foreach (var deviceService in DeviceServices)
            {
                List<SysResourceModel> sysResourceModels = Db.Queryable<SysResourceModel>().Where(it => it.Pid == deviceService.SysResourceModel.Id && it.IsDelete == false && it.IsEnable == true).ToList();
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ServiceTypes.Group)
                    {
                        GroupResource groupResource = new(sysResourceModel);
                        deviceService.AddChild(groupResource);
                        GroupResources.Add(groupResource);
                    }
                   else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 50)
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
            Db.CodeFirst.InitTables<SysResourceGoupModel>();

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
                else if (30<=sysResourceModel.Type && sysResourceModel.Type <= 50)
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
