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
using ColorVision.Services.PhyCameras;
using ColorVision.Services.Terminal;
using ColorVision.Services.Types;
using ColorVision.UI;
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
        public static UserConfig UserConfig => UserConfig.Instance;

        public ObservableCollection<TypeService> TypeServices { get; set; } = new ObservableCollection<TypeService>();
        public ObservableCollection<TerminalService> TerminalServices { get; set; } = new ObservableCollection<TerminalService>();
        public ObservableCollection<DeviceService> DeviceServices { get; set; } = new ObservableCollection<DeviceService>();

        public ObservableCollection<GroupResource> GroupResources { get; set; } = new ObservableCollection<GroupResource>();
        public ObservableCollection<DeviceService> LastGenControl { get; set; } = new ObservableCollection<DeviceService>();

        public List<MQTTServiceInfo> ServiceTokens { get; set; }

        public ServiceManager()
        {
            svrDevices = new Dictionary<string, List<MQTTServiceBase>>();
            ServiceTokens = new List<MQTTServiceInfo>();
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => LoadServices();
            if (MySqlControl.GetInstance().IsConnect)
                LoadServices();
        }

        public void GenControl(ObservableCollection<DeviceService> MQTTDevices)
        {
            LastGenControl = MQTTDevices;
            DisPlayManager.GetInstance().IDisPlayControls.Clear();
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
            List<MQTTServiceInfo> result = new();
            foreach (var item in ServiceTokens)
            {
                MQTTServiceInfo serviceInfo = new() { PublishTopic = item.PublishTopic, ServiceCode = item.ServiceCode, ServiceType = item.ServiceType, Token = item.Token, SubscribeTopic = item.SubscribeTopic };
                result.Add(serviceInfo);
                foreach (var dev in item.Devices)
                {
                    MQTTDeviceInfo device = new() { ID = dev.Value.ID, DeviceCode = dev.Value.DeviceCode };
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
            DisPlayManager.GetInstance().IDisPlayControls.Clear();
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
        }
        private Dictionary<string, List<MQTTServiceBase>> svrDevices = new();

        public void LoadServices()
        {
            LastGenControl?.Clear();
            ServiceTokens.Clear();


            var ServiceTypess = Enum.GetValues(typeof(ServiceTypes)).Cast<ServiceTypes>();
            List<SysDictionaryModel> SysDictionaryModels = SysDictionaryDao.Instance.GetServiceTypes();

            TypeServices.Clear();
            foreach (var type in ServiceTypess)
            {
                TypeService typeService = new();
                var sysDictionaryModel = SysDictionaryModels.Find((x)=>x.Value ==(int)type);
                if (sysDictionaryModel == null) continue;
                typeService.Name = sysDictionaryModel.Name ?? type.ToString();
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

            List<SysDeviceModel> sysResourceModelDevices = VSysDeviceDao.Instance.GetAll(UserConfig.TenantId);
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
                                DeviceCamera deviceCamera = new(sysResourceModel, cameraService);
                                svrObj = deviceCamera.DeviceService;
                                terminalService.AddChild(deviceCamera);
                                DeviceServices.Add(deviceCamera);
                            }
                            break;
                        case ServiceTypes.PG:
                            DevicePG devicePG = new(sysResourceModel);
                            svrObj = devicePG.DeviceService;
                            terminalService.AddChild(devicePG);
                            DeviceServices.Add(devicePG);
                            break;
                        case ServiceTypes.Spectrum:
                            DeviceSpectrum deviceSpectrum = new(sysResourceModel);
                            svrObj = deviceSpectrum.DeviceService;
                            terminalService.AddChild(deviceSpectrum);
                            DeviceServices.Add(deviceSpectrum);
                            break;
                        case ServiceTypes.SMU:
                            DeviceSMU deviceSMU = new(sysResourceModel);
                            svrObj = deviceSMU.Service;
                            terminalService.AddChild(deviceSMU);
                            DeviceServices.Add(deviceSMU);
                            break;
                        case ServiceTypes.Sensor:
                            DeviceSensor device1 = new(sysResourceModel);
                            svrObj = device1.DeviceService;
                            terminalService.AddChild(device1);
                            DeviceServices.Add(device1);
                            break;
                        case ServiceTypes.FileServer:
                            DeviceFileServer img = new(sysResourceModel);
                            svrObj = img.MQTTFileServer;
                            terminalService.AddChild(img);
                            DeviceServices.Add(img);
                            break;
                        case ServiceTypes.Algorithm:
                            DeviceAlgorithm alg = new(sysResourceModel);
                            svrObj = alg.MQTTService;
                            terminalService.AddChild(alg);
                            DeviceServices.Add(alg);
                            break;
                        case ServiceTypes.Calibration:
                            DeviceCalibration deviceCalibration = new(sysResourceModel);
                            svrObj = deviceCalibration.DeviceService;
                            terminalService.AddChild(deviceCalibration);
                            DeviceServices.Add(deviceCalibration);
                            break;
                        case ServiceTypes.CfwPort:
                            DeviceCfwPort deviceCfwPort = new(sysResourceModel);
                            svrObj = deviceCfwPort.DeviceService;
                            terminalService.AddChild(deviceCfwPort);
                            DeviceServices.Add(deviceCfwPort);
                            break;
                        case ServiceTypes.Motor:
                            DeviceMotor deviceMotor = new(sysResourceModel);
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

            foreach (var phycamrea in PhyCameraManager.GetInstance().PhyCameras)
            {
                List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetResourceItems(phycamrea.SysResourceModel.Id, UserConfig.TenantId);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ServiceTypes.Group)
                    {
                        GroupResource groupResource = new(sysResourceModel);
                        phycamrea.AddChild(groupResource);
                        GroupResources.Add(groupResource);
                    }
                    else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 40)
                    {
                        CalibrationResource calibrationResource = new(sysResourceModel);
                        phycamrea.AddChild(calibrationResource);
                    }
                    else
                    {
                        BaseFileResource calibrationResource = new(sysResourceModel);
                        phycamrea.AddChild(calibrationResource);
                    }
                }
            }

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
                        BaseFileResource calibrationResource = new(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                }
            }

            foreach (var groupResource in GroupResources)
            {
                LoadgroupResource(groupResource);
            }
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
                    BaseResource calibrationResource = new(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
            }
        }

        public void ProcResult(FlowControlData flowControlData)
        {
            int totalTime = flowControlData.Params.TTL;
            BatchResultMasterDao.Instance.UpdateEnd(flowControlData.SerialNumber, totalTime, flowControlData.EventName);
            SpectrumDrawPlotFromDB(flowControlData.SerialNumber);
        }


        private SMUResultDao smuDao = new();

        public void SpectrumDrawPlotFromDB(string bid)
        {
            List<SpectrumData> datas = new();
            List<SpectumResultModel> resultSpec;
            List<SMUResultModel> resultSMU;
            BatchResultMasterModel batch = BatchResultMasterDao.Instance.GetByCode(bid);
            if (batch == null)
            {
                 resultSMU = smuDao.selectBySN(bid);
                 resultSpec = SpectumResultDao.Instance.selectBySN(bid);
            }
            else
            {
                resultSMU = smuDao.GetAllByPid(batch.Id);
                resultSpec = SpectumResultDao.Instance.GetAllByPid(batch.Id);
            }

            for (int i = 0; i < resultSpec.Count; i++)
            {
                var item = resultSpec[i];
                cvColorVision.GCSDLL.ColorParam param = new()
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
                SpectrumData data = new(item.Id, param);
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

            foreach (var ctl in DisPlayManager.GetInstance().IDisPlayControls)
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

        private static string GetServiceKey(string svrType, string svrCode)
        {
            return svrType + ":" + svrCode;
        }

        public static void BeginNewBatch(string sn, string name)
        {
            BatchResultMasterModel batch = new();
            batch.Name = string.IsNullOrEmpty(name) ? sn : name;
            batch.Code = sn;
            batch.CreateDate = DateTime.Now;
            batch.TenantId = 0;
            BatchResultMasterDao.Instance.Save(batch);
        }
    }
}
