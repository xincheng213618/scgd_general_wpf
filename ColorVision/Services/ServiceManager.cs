using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Device.Spectrum;
using ColorVision.Device.Spectrum.Configs;
using ColorVision.Flow;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
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

namespace ColorVision.Services
{
    public class ServiceManager
    {
        private static ServiceManager _instance;
        private static readonly object _locker = new();
        public static ServiceManager GetInstance() { lock (_locker) { return _instance ??= new ServiceManager(); } }

        public ObservableCollection<TypeService> TypeServices { get; set; }
        public Dictionary<string,string> ServiceTokens { get; set; }
        public ObservableCollection<DeviceService> Devices { get; set; }

        public SysResourceService ResourceService { get; set; }

        public UserConfig UserConfig { get; set; }

        private ResultService resultService;

        public StackPanel StackPanel { get; set; }

        private Dictionary<string, List<MQTTServiceBase>> svrDevices;
        public ServiceManager()
        {
            ResourceService = new SysResourceService();
            resultService = new ResultService();
            TypeServices = new ObservableCollection<TypeService>();
            Devices = new ObservableCollection<DeviceService>();
            svrDevices = new Dictionary<string, List<MQTTServiceBase>>();
            ServiceTokens = new Dictionary<string,string>();

            UserConfig = ConfigHandler.GetInstance().SoftwareConfig.UserConfig;
            StackPanel = new StackPanel();

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Reload();

            Reload();
        }
        public ObservableCollection<DeviceService> LastGenControl { get; set; }

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
            LastGenControl = Devices;
        }
        public void Reload()
        {
            this.TypeServices.Clear();
            Devices.Clear();
            LastGenControl?.Clear();
            svrDevices?.Clear();

            List<SysResourceModel> Services = ResourceService.GetAllServices(UserConfig.TenantId);
            List<SysResourceModel> devices = ResourceService.GetAllDevices(UserConfig.TenantId);

            SysDictionaryService sysDictionaryService = new SysDictionaryService();

            var ServiceTypes = Enum.GetValues(typeof(ServiceTypes)).Cast<ServiceTypes>();

            foreach (var item in sysDictionaryService.GetAllServiceType())
            {
                bool IsserviceType = false;
                foreach (var serviceType in ServiceTypes)
                {
                    if (item.Value == (int)serviceType)
                    {
                        IsserviceType = true;
                        break;
                    }
                }
                if (!IsserviceType)
                    continue;

                TypeService mQTTServicetype = new TypeService();
                mQTTServicetype.Name = item.Name ?? string.Empty;
                mQTTServicetype.SysDictionaryModel = item;
                foreach (var service in Services)
                {
                    if (service.Type == item.Value)
                    {
                        TerminalService terminalService = new TerminalService(service);
                        string svrKey = GetServiceKey(service.TypeCode ?? string.Empty, service.Code ?? string.Empty);

                        svrDevices ??= new Dictionary<string, List<MQTTServiceBase>>();
                        if (!svrDevices.ContainsKey(svrKey))
                        {
                            svrDevices?.Add(svrKey, new List<MQTTServiceBase>());
                            if (service.Code != null)
                            {
                                ServiceTokens.Add(service.Code, string.Empty);
                            }
                        }

                        foreach (var device in devices)
                        {
                            MQTTServiceBase svrObj = null;
                            if (device.Pid == service.Id)
                            {
                                switch ((ServiceTypes)device.Type)
                                {
                                    case ColorVision.Services.ServiceTypes.camera:

                                        if (terminalService.MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                                        {
                                            DeviceCamera deviceCamera = new DeviceCamera(device, cameraService);
                                            svrObj = deviceCamera.DService;
                                            terminalService.AddChild(deviceCamera);
                                            Devices.Add(deviceCamera);
                                        }
                                        break;
                                    case ColorVision.Services.ServiceTypes.pg:
                                        DevicePG devicePG = new DevicePG(device);
                                        svrObj = devicePG.DeviceService;
                                        terminalService.AddChild(devicePG);
                                        Devices.Add(devicePG);
                                        break;
                                    case ColorVision.Services.ServiceTypes.Spectum:
                                        DeviceSpectrum deviceSpectrum = new DeviceSpectrum(device);
                                        svrObj = deviceSpectrum.DeviceService;
                                        terminalService.AddChild(deviceSpectrum);
                                        Devices.Add(deviceSpectrum);
                                        break;
                                    case ColorVision.Services.ServiceTypes.SMU:
                                        DeviceSMU deviceSMU = new DeviceSMU(device);
                                        svrObj = deviceSMU.Service;
                                        terminalService.AddChild(deviceSMU);
                                        Devices.Add(deviceSMU);
                                        break;
                                    case ColorVision.Services.ServiceTypes.Sensor:
                                        DeviceSensor device1 = new DeviceSensor(device);
                                        svrObj = device1.DeviceService;
                                        terminalService.AddChild(device1);
                                        Devices.Add(device1);
                                        break;
                                    case ColorVision.Services.ServiceTypes.FileServer:
                                        DeviceFileServer img = new DeviceFileServer(device);
                                        svrObj = img.DeviceService;
                                        terminalService.AddChild(img);
                                        Devices.Add(img);
                                        break;
                                    case ColorVision.Services.ServiceTypes.Algorithm:
                                        DeviceAlgorithm alg = new DeviceAlgorithm(device);
                                        svrObj = alg.MQTTService;
                                        terminalService.AddChild(alg);
                                        Devices.Add(alg);
                                        break;
                                    case ColorVision.Services.ServiceTypes.Calibration:
                                        DeviceCalibration deviceCalibration = new DeviceCalibration(device);
                                        svrObj = deviceCalibration.DeviceService;
                                        terminalService.AddChild(deviceCalibration);
                                        Devices.Add(deviceCalibration);
                                        break;
                                    case ColorVision.Services.ServiceTypes.CfwPort:
                                        DeviceCfwPort deviceCfwPort = new DeviceCfwPort(device);
                                        svrObj = deviceCfwPort.DeviceService;
                                        terminalService.AddChild(deviceCfwPort);
                                        Devices.Add(deviceCfwPort);
                                        break;
                                    case ColorVision.Services.ServiceTypes.Motor:
                                        DeviceMotor deviceMotor = new DeviceMotor(device);
                                        svrObj = deviceMotor.DeviceService;
                                        terminalService.AddChild(deviceMotor);
                                        Devices.Add(deviceMotor);
                                        break;
                                    default:
                                        break;
                                }
                            }

                            if (svrObj != null&& svrDevices!=null)
                            {
                                svrObj.ServiceName = service.Code ?? string.Empty;
                                svrDevices[svrKey].Add(svrObj);
                            }
                        }
                        mQTTServicetype.AddChild(terminalService);
                    }
                }
                this.TypeServices.Add(mQTTServicetype);
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
