using ColorVision.MySql;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.DAO;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.FileServer;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.SMU.Dao;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum.Dao;
using ColorVision.Services.Flow;
using ColorVision.Services.Terminal;
using ColorVision.Services.Type;
using ColorVision.Settings;
using ColorVision.UserSpace;
using FlowEngineLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace ColorVision.Services
{
    public class ServiceManager
    {
        private static ServiceManager _instance;
        private static readonly object _locker = new();
        public static ServiceManager GetInstance() { lock (_locker) { return _instance ??= new ServiceManager(); } }
        public UserConfig UserConfig { get; set; }


        public ObservableCollection<TypeService> TypeServices { get; set; } = new ObservableCollection<TypeService>();
        public ObservableCollection<TerminalService> TerminalServices { get; set; } = new ObservableCollection<TerminalService>();
        public ObservableCollection<DeviceService> DeviceServices { get; set; } = new ObservableCollection<DeviceService>();

        public ObservableCollection<GroupResource> GroupResources { get; set; } = new ObservableCollection<GroupResource>();
        public ObservableCollection<DeviceService> LastGenControl { get; set; } = new ObservableCollection<DeviceService>();

        public List<MQTTServiceInfo> ServiceTokens { get; set; }


        public ObservableCollection<IDisPlayControl> DisPlayControls { get; set; } = new ObservableCollection<IDisPlayControl>();


        public VSysResourceDao VSysResourceDao { get; set; } = new VSysResourceDao();
        public VSysDeviceDao VSysDeviceDao { get; set; } = new VSysDeviceDao();

        public ServiceManager()
        {
            UserConfig = ConfigHandler.GetInstance().SoftwareConfig.UserConfig;

            svrDevices = new Dictionary<string, List<MQTTServiceBase>>();
            ServiceTokens = new List<MQTTServiceInfo>();
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => LoadServices();
            LoadServices();
        }

        public void GenControl(ObservableCollection<DeviceService> MQTTDevices)
        {
            LastGenControl = MQTTDevices;
            DisPlayControls.Clear();
            foreach (var item in MQTTDevices)
            {
                if (item is DeviceService device)
                {
                    if (device.GetDisplayControl() is IDisPlayControl disPlayControl)
                    {
                        DisPlayControls.Add(disPlayControl);
                    }
                }
            }
        }
        public MQTTServiceInfo? GetServiceInfo(ServiceTypes serviceType,string serviceCode)
        {
            foreach (var item in ServiceTokens)
            {
                if(item.ServiceType == serviceType.ToString())
                {
                    if(string.IsNullOrEmpty(serviceCode)) return item;
                    else if(serviceCode == item.ServiceCode) return item;
                }
            }
            return null;
        }
        public List<MQTTServiceInfo> GetServiceJsonList()
        {
            List<MQTTServiceInfo> result = new List<MQTTServiceInfo>();
            foreach (var item in ServiceTokens)
            {
                MQTTServiceInfo serviceInfo = new MQTTServiceInfo() { PublishTopic = item.PublishTopic, ServiceCode = item.ServiceCode, ServiceType = item.ServiceType, Token = item.Token, SubscribeTopic = item.SubscribeTopic };
                result.Add(serviceInfo);
                foreach (var dev in item.Devices)
                {
                    MQTTDeviceInfo device = new MQTTDeviceInfo() { ID = dev.Value.ID, DeviceCode = dev.Value.DeviceCode };
                    serviceInfo.Devices.Add(dev.Key, device);
                }
            }
            return result;
        }
        /// <summary>
        /// 生成显示空间
        /// </summary>
        public void GenDeviceDisplayControl()
        {
            LastGenControl = new ObservableCollection<DeviceService>();
            DisPlayControls.Clear();
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
                                DisPlayControls.Add(disPlayControl);
                            }
                        }
                    }
                }
            }
            LastGenControl = DeviceServices;
        }
        public VSysResourceDao SysResourceDao { get; set; } = new VSysResourceDao();
        private Dictionary<string, List<MQTTServiceBase>> svrDevices = new Dictionary<string, List<MQTTServiceBase>>();
        private SysDictionaryDao sysDictionaryDao = new SysDictionaryDao();

        public void LoadServices()
        {
            LastGenControl?.Clear();
            ServiceTokens.Clear();


            var ServiceTypess = Enum.GetValues(typeof(ServiceTypes)).Cast<ServiceTypes>();
            List<SysDictionaryModel> SysDictionaryModels = sysDictionaryDao.GetAllByPcode("service_type");

            TypeServices.Clear();
            foreach (var type in ServiceTypess)
            {
                TypeService typeService = new TypeService();
                var sysDictionaryModel = SysDictionaryModels.Find((x)=>x.Value ==(int)type);
                if (sysDictionaryModel == null) continue;
                typeService.Name = sysDictionaryModel.Name ?? type.ToString();
                typeService.SysDictionaryModel = sysDictionaryModel;
                TypeServices.Add(typeService);
            }


            TerminalServices.Clear();
            svrDevices.Clear();
            List<SysResourceModel> sysResourceModelServices = SysResourceDao.GetServices(UserConfig.TenantId);
            foreach (var typeService1 in TypeServices)
            {
                var sysResourceModels = sysResourceModelServices.FindAll((x) => x.Type == (int)typeService1.ServiceTypes);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    TerminalService terminalService = typeService1.ServiceTypes switch
                    {
                        ServiceTypes.Camera => new TerminalCamera(sysResourceModel),
                        _ => new TerminalService(sysResourceModel),
                    };
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

            List<SysDeviceModel> sysResourceModelDevices = VSysDeviceDao.GetAll(UserConfig.TenantId);
            DeviceServices.Clear();

            foreach (var terminalService in TerminalServices)
            {
                var sysResourceModels = sysResourceModelDevices.FindAll((x) => x.Pid == (int)terminalService.SysResourceModel.Id);

                foreach (var sysResourceModel in sysResourceModels)
                {
                    MQTTServiceBase svrObj  = null;

                    switch ((ServiceTypes)sysResourceModel.Type)
                    {
                        case ServiceTypes.Camera:

                            if (terminalService.MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                            {
                                DeviceCamera deviceCamera = new DeviceCamera(sysResourceModel, cameraService);
                                svrObj = deviceCamera.DeviceService;
                                terminalService.AddChild(deviceCamera);
                                DeviceServices.Add(deviceCamera);
                            }
                            break;
                        case ServiceTypes.PG:
                            DevicePG devicePG = new DevicePG(sysResourceModel);
                            svrObj = devicePG.DeviceService;
                            terminalService.AddChild(devicePG);
                            DeviceServices.Add(devicePG);
                            break;
                        case ServiceTypes.Spectrum:
                            DeviceSpectrum deviceSpectrum = new DeviceSpectrum(sysResourceModel);
                            svrObj = deviceSpectrum.DeviceService;
                            terminalService.AddChild(deviceSpectrum);
                            DeviceServices.Add(deviceSpectrum);
                            break;
                        case ServiceTypes.SMU:
                            DeviceSMU deviceSMU = new DeviceSMU(sysResourceModel);
                            svrObj = deviceSMU.Service;
                            terminalService.AddChild(deviceSMU);
                            DeviceServices.Add(deviceSMU);
                            break;
                        case ServiceTypes.Sensor:
                            DeviceSensor device1 = new DeviceSensor(sysResourceModel);
                            svrObj = device1.DeviceService;
                            terminalService.AddChild(device1);
                            DeviceServices.Add(device1);
                            break;
                        case ServiceTypes.FileServer:
                            DeviceFileServer img = new DeviceFileServer(sysResourceModel);
                            svrObj = img.MQTTFileServer;
                            terminalService.AddChild(img);
                            DeviceServices.Add(img);
                            break;
                        case ServiceTypes.Algorithm:
                            DeviceAlgorithm alg = new DeviceAlgorithm(sysResourceModel);
                            svrObj = alg.MQTTService;
                            terminalService.AddChild(alg);
                            DeviceServices.Add(alg);
                            break;
                        case ServiceTypes.Calibration:
                            DeviceCalibration deviceCalibration = new DeviceCalibration(sysResourceModel);
                            svrObj = deviceCalibration.DeviceService;
                            terminalService.AddChild(deviceCalibration);
                            DeviceServices.Add(deviceCalibration);
                            break;
                        case ServiceTypes.CfwPort:
                            DeviceCfwPort deviceCfwPort = new DeviceCfwPort(sysResourceModel);
                            svrObj = deviceCfwPort.DeviceService;
                            terminalService.AddChild(deviceCfwPort);
                            DeviceServices.Add(deviceCfwPort);
                            break;
                        case ServiceTypes.Motor:
                            DeviceMotor deviceMotor = new DeviceMotor(sysResourceModel);
                            svrObj = deviceMotor.DeviceService;
                            terminalService.AddChild(deviceMotor);
                            DeviceServices.Add(deviceMotor);
                            break;
                        default:
                            break;
                    }


                    string svrKey = GetServiceKey(terminalService.SysResourceModel.TypeCode ?? string.Empty, terminalService.SysResourceModel.Code ?? string.Empty);

                    if (svrObj != null && svrDevices.TryGetValue(svrKey,out var list))
                    {
                        svrObj.ServiceName = terminalService.SysResourceModel.Code ?? string.Empty;
                        list.Add(svrObj);
                    }
                }
            }

            GroupResources.Clear();
            foreach (var deviceService in DeviceServices)
            {
                List<SysResourceModel> sysResourceModels = sysResourceDao1.GetResourceItems(deviceService.SysResourceModel.Id, UserConfig.TenantId);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ResourceType.Group)
                    {
                        GroupResource groupResource = new GroupResource(sysResourceModel);
                        deviceService.AddChild(groupResource);
                        GroupResources.Add(groupResource);
                    }
                   else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 40)
                    {
                        CalibrationResource calibrationResource = new CalibrationResource(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                    else
                    {
                        BaseResource calibrationResource = new BaseResource(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                }
            }

            foreach (var groupResource in GroupResources)
            {
                LoadgroupResource(groupResource);
            }
        }
        SysResourceDao sysResourceDao1 = new SysResourceDao();

        public void LoadgroupResource(GroupResource groupResource)
        {
            sysResourceDao1.CreatResourceGroup();
            List<SysResourceModel> sysResourceModels = sysResourceDao1.GetGroupResourceItems(groupResource.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                if (sysResourceModel.Type == (int)ResourceType.Group)
                {
                    GroupResource groupResource1 = new GroupResource(sysResourceModel);
                    LoadgroupResource(groupResource1);
                    groupResource.AddChild(groupResource);
                    GroupResources.Add(groupResource);
                }
                else if (30<=sysResourceModel.Type && sysResourceModel.Type <= 40)
                {
                    CalibrationResource calibrationResource = new CalibrationResource(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
                else
                {
                    BaseResource calibrationResource = new BaseResource(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
            }
        }
        private BatchResultMasterDao batchDao = new BatchResultMasterDao();

        public void ProcResult(FlowControlData flowControlData)
        {
            int totalTime = flowControlData.Params.TTL;
            batchDao.UpdateEnd(flowControlData.SerialNumber, totalTime, flowControlData.EventName);
            SpectrumDrawPlotFromDB(flowControlData.SerialNumber);
        }


        private SpectumResultDao spectumDao = new SpectumResultDao();
        private SMUResultDao smuDao = new SMUResultDao();

        public void SpectrumDrawPlotFromDB(string bid)
        {
            List<SpectrumData> datas = new List<SpectrumData>();
            List<SpectumResultModel> resultSpec;
            List<SMUResultModel> resultSMU;
            BatchResultMasterModel batch = batchDao.GetByCode(bid);
            if (batch == null)
            {
                 resultSMU = smuDao.selectBySN(bid);
                 resultSpec = spectumDao.selectBySN(bid);
            }
            else
            {
                resultSMU = smuDao.GetAllByPid(batch.Id);
                resultSpec = spectumDao.GetAllByPid(batch.Id);
            }

            for (int i = 0; i < resultSpec.Count; i++)
            {
                var item = resultSpec[i];
                cvColorVision.GCSDLL.ColorParam param = new cvColorVision.GCSDLL.ColorParam()
                {
                    fx = item.fx ?? 0,
                    fy = item.fy ?? 0,
                    fu = item.fu ?? 0,
                    fv = item.fv ?? 0,
                    fCCT = item.fCCT ?? 0,
                    dC = item.dC ?? 0,
                    fLd = item.fLd ?? 0,
                    fPur = item.fPur ?? 0,
                    fLp = item.fLp ?? 0,
                    fHW = item.fHW ?? 0,
                    fLav = item.fLav ?? 0,
                    fRa = item.fRa ?? 0,
                    fRR = item.fRR ?? 0,
                    fGR = item.fGR ?? 0,
                    fBR = item.fBR ?? 0,
                    fIp = item.fIp ?? 0,
                    fPh = item.fPh ?? 0,
                    fPhe = item.fPhe ?? 0,
                    fPlambda = item.fPlambda ?? 0,
                    fSpect1 = item.fSpect1 ?? 0,
                    fSpect2 = item.fSpect2 ?? 0,
                    fInterval = item.fInterval ?? 0,
                    fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? Array.Empty<float>(),
                    fRi = JsonConvert.DeserializeObject<float[]>(item.fRi ?? string.Empty) ?? Array.Empty<float>(),
                };
                SpectrumData data = new SpectrumData(item.Id, param);
                if (i < resultSMU.Count)
                {
                    data.V = resultSMU[i].VResult;
                    data.I = resultSMU[i].IResult;
                }
                else
                {
                    data.V = float.NaN;
                    data.I = float.NaN;
                }

                datas.Add(data);
            }

            foreach (var ctl in DisPlayControls)
            {
                if (ctl is DisplaySpectrumControl spectrum)
                {
                    spectrum.SpectrumClear();
                    foreach (SpectrumData data in datas)
                    {
                        spectrum.SpectrumDrawPlot(data);
                    }
                }
            }
        }

        public BatchResultMasterModel BatchSave(string sn)
        {
            BatchResultMasterModel model = new BatchResultMasterModel(sn, UserConfig.TenantId);
            batchDao.Save(model);
            return model;
        }

        public int ResultBatchSave(string sn)
        {
            BatchResultMasterModel model = new BatchResultMasterModel(sn, UserConfig.TenantId);
            return batchDao.Save(model);
        }

        private static string GetServiceKey(string svrType, string svrCode)
        {
            return svrType + ":" + svrCode;
        }
    }
}
