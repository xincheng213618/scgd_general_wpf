using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.MySql;
using ColorVision.MySql.Service;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.SMU.Dao;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Devices.Spectrum.Dao;
using ColorVision.Users;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows.Controls;
using static cvColorVision.GCSDLL;
using ColorVision.Services.Dao;
using ColorVision.Services.DAO;
using ColorVision.Services.Flow;
using ColorVision.SettingUp;
using System.Security.Cryptography.X509Certificates;
using System.Net.WebSockets;
using System.Globalization;
using ColorVision.Services.Interfaces;

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

        public ObservableCollection<GroupService> GroupServices { get; set; } = new ObservableCollection<GroupService>();
        public ObservableCollection<DeviceService> LastGenControl { get; set; } = new ObservableCollection<DeviceService>();

        public Dictionary<string, string> ServiceTokens { get; set; } = new Dictionary<string, string>();


        public SysResourceService ResourceService { get; set; }


        private ResultService resultService;

        public StackPanel StackPanel { get; set; } = new StackPanel();



        public ServiceManager()
        {
            UserConfig = ConfigHandler.GetInstance().SoftwareConfig.UserConfig;

            ResourceService = new SysResourceService();
            resultService = new ResultService();

            svrDevices = new Dictionary<string, List<MQTTServiceBase>>();
            ServiceTokens = new Dictionary<string,string>();

            StackPanel = new StackPanel();

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => LoadServices();
            LoadServices();
        }

        public void GenControl(ObservableCollection<DeviceService> MQTTDevices)
        {
            LastGenControl = MQTTDevices;
            StackPanel.Children.Clear();
            foreach (var item in MQTTDevices)
            {
                if (item is DeviceService device)
                {
                    StackPanel.Children.Add(device.GetDisplayControl());
                }
            }
        }

        /// <summary>
        /// 生成显示空间
        /// </summary>
        public void GenDeviceDisplayControl()
        {
            LastGenControl = new ObservableCollection<DeviceService>();
            StackPanel.Children.Clear();
            foreach (var serviceKind in TypeServices)
            {
                foreach (var service in serviceKind.VisualChildren)
                {
                    foreach (var item in service.VisualChildren)  
                    {
                        if (item is DeviceService device)
                        {
                            LastGenControl.Add(device);
                            StackPanel.Children.Add(device.GetDisplayControl());
                        }
                    }
                }
            }
            LastGenControl = DeviceServices;
        }
        public VSysResourceDao SysResourceDao { get; set; } = new VSysResourceDao();
        SysDictionaryService sysDictionaryService = new SysDictionaryService();
        private Dictionary<string, List<MQTTServiceBase>> svrDevices = new Dictionary<string, List<MQTTServiceBase>>();


        public void LoadServices()
        {
            LastGenControl?.Clear();
            ServiceTokens.Clear();


            var ServiceTypes = Enum.GetValues(typeof(ServiceTypes)).Cast<ServiceTypes>();
            List<SysDictionaryModel> SysDictionaryModels = sysDictionaryService.GetAllServiceType();

            TypeServices.Clear();
            foreach (var type in ServiceTypes)
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
            List<SysResourceModel> sysResourceModelServices = ResourceService.GetAllServices(UserConfig.TenantId);
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

                    if (sysResourceModel.Code != null && !ServiceTokens.ContainsKey(sysResourceModel.Code))
                        ServiceTokens.TryAdd(sysResourceModel.Code, string.Empty);

                    typeService1.AddChild(terminalService);
                    TerminalServices.Add(terminalService);
                }
            }

            List<SysResourceModel> sysResourceModelDevices = ResourceService.GetAllDevices(UserConfig.TenantId);
            DeviceServices.Clear();

            foreach (var terminalService in TerminalServices)
            {
                var sysResourceModels = sysResourceModelDevices.FindAll((x) => x.Pid == (int)terminalService.SysResourceModel.Id);

                foreach (var sysResourceModel in sysResourceModels)
                {
                    MQTTServiceBase svrObj  = null;

                    switch ((ServiceTypes)sysResourceModel.Type)
                    {
                        case ColorVision.Services.ServiceTypes.camera:

                            if (terminalService.MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                            {
                                DeviceCamera deviceCamera = new DeviceCamera(sysResourceModel, cameraService);
                                svrObj = deviceCamera.DeviceService;
                                terminalService.AddChild(deviceCamera);
                                DeviceServices.Add(deviceCamera);
                            }
                            break;
                        case ColorVision.Services.ServiceTypes.pg:
                            DevicePG devicePG = new DevicePG(sysResourceModel);
                            svrObj = devicePG.DeviceService;
                            terminalService.AddChild(devicePG);
                            DeviceServices.Add(devicePG);
                            break;
                        case ColorVision.Services.ServiceTypes.Spectum:
                            DeviceSpectrum deviceSpectrum = new DeviceSpectrum(sysResourceModel);
                            svrObj = deviceSpectrum.DeviceService;
                            terminalService.AddChild(deviceSpectrum);
                            DeviceServices.Add(deviceSpectrum);
                            break;
                        case ColorVision.Services.ServiceTypes.SMU:
                            DeviceSMU deviceSMU = new DeviceSMU(sysResourceModel);
                            svrObj = deviceSMU.Service;
                            terminalService.AddChild(deviceSMU);
                            DeviceServices.Add(deviceSMU);
                            break;
                        case ColorVision.Services.ServiceTypes.Sensor:
                            DeviceSensor device1 = new DeviceSensor(sysResourceModel);
                            svrObj = device1.DeviceService;
                            terminalService.AddChild(device1);
                            DeviceServices.Add(device1);
                            break;
                        case ColorVision.Services.ServiceTypes.FileServer:
                            DeviceFileServer img = new DeviceFileServer(sysResourceModel);
                            svrObj = img.DeviceService;
                            terminalService.AddChild(img);
                            DeviceServices.Add(img);
                            break;
                        case ColorVision.Services.ServiceTypes.Algorithm:
                            DeviceAlgorithm alg = new DeviceAlgorithm(sysResourceModel);
                            svrObj = alg.MQTTService;
                            terminalService.AddChild(alg);
                            DeviceServices.Add(alg);
                            break;
                        case ColorVision.Services.ServiceTypes.Calibration:
                            DeviceCalibration deviceCalibration = new DeviceCalibration(sysResourceModel);
                            svrObj = deviceCalibration.DeviceService;
                            terminalService.AddChild(deviceCalibration);
                            DeviceServices.Add(deviceCalibration);
                            break;
                        case ColorVision.Services.ServiceTypes.CfwPort:
                            DeviceCfwPort deviceCfwPort = new DeviceCfwPort(sysResourceModel);
                            svrObj = deviceCfwPort.DeviceService;
                            terminalService.AddChild(deviceCfwPort);
                            DeviceServices.Add(deviceCfwPort);
                            break;
                        case ColorVision.Services.ServiceTypes.Motor:
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

            GroupServices.Clear();
            foreach (var deviceService in DeviceServices)
            {
                List<SysResourceModel> sysResourceModels = sysResourceDao1.GetResourceItems(deviceService.SysResourceModel.Id, UserConfig.TenantId);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ResourceType.Group)
                    {
                        GroupService groupService = new GroupService(sysResourceModel);
                        deviceService.AddChild(groupService);
                        GroupServices.Add(groupService);
                    }
                    else
                    {
                        CalibrationResource calibrationResource = new CalibrationResource(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                }
            }

            foreach (var groupService in GroupServices)
            {
                LoadGroupService(groupService);
            }
        }
        SysResourceDao sysResourceDao1 = new SysResourceDao();

        public void LoadGroupService(GroupService groupService)
        {
            sysResourceDao1.CreatResourceGroup();
            List<SysResourceModel> sysResourceModels = sysResourceDao1.GetGroupResourceItems(groupService.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                if (sysResourceModel.Type == (int)ResourceType.Group)
                {
                    GroupService groupService1 = new GroupService(sysResourceModel);

                    LoadGroupService(groupService1);
                    groupService.AddChild(groupService);
                    GroupServices.Add(groupService);
                }
                else
                {
                    CalibrationResource calibrationResource = new CalibrationResource(sysResourceModel);
                    groupService.AddChild(calibrationResource);
                }
            }
        }




        public void ProcResult(FlowControlData flowControlData)
        {
            int totalTime = flowControlData.Params.TTL;
            resultService.BatchUpdateEnd(flowControlData.SerialNumber, totalTime, flowControlData.EventName);

            SpectrumDrawPlotFromDB(flowControlData.SerialNumber);
        }

        public void SpectrumDrawPlotFromDB(string bid)
        {
            List<SpectumData> datas = new List<SpectumData>();
            List<SpectumResultModel> resultSpec = resultService.SpectumSelectBySN(bid);
            List<SMUResultModel> resultSMU = resultService.SMUSelectBySN(bid);
            for (int i = 0; i < resultSpec.Count; i++)
            {
                var item = resultSpec[i];
                ColorParam param = new ColorParam()
                {
                    fx = item.fx??0,
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
                SpectumData data = new SpectumData(item.Id, param);
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

            foreach (UserControl ctl in StackPanel.Children)
            {
                if (ctl is DisplaySpectrumControl spectrum)
                {
                    spectrum.SpectrumClear();
                    foreach (SpectumData data in datas)
                    {
                        spectrum.SpectrumDrawPlot(data);
                    }
                }
            }
        }


        public BatchResultMasterModel BatchSave(string sn)
        {
            BatchResultMasterModel model = new BatchResultMasterModel(sn, UserConfig.TenantId);
            resultService.BatchSave(model);
            return model;
        }

        public int ResultBatchSave(string sn)
        {
            BatchResultMasterModel model = new BatchResultMasterModel(sn, UserConfig.TenantId);
            return resultService.BatchSave(model);
        }

        private static string GetServiceKey(string svrType, string svrCode)
        {
            return svrType + ":" + svrCode;
        }
    }
}
