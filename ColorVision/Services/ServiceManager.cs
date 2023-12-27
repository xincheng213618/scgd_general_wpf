using ColorVision.Device.Camera;
using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.Flow;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Services.Algorithm;
using ColorVision.Services.Device;
using ColorVision.Services.Device.Calibration;
using ColorVision.Services.Device.CfwPort;
using ColorVision.Services.Device.Motor;
using ColorVision.Services.Device.Sensor;
using ColorVision.User;
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


        public ObservableCollection<ServiceKind> Services { get; set; }
        public ObservableCollection<BaseChannel> Devices { get; set; }

        public SysResourceService ResourceService { get; set; }

        public UserConfig UserConfig { get; set; }

        private ResultService resultService;

        public StackPanel StackPanel { get; set; }

        private Dictionary<string, List<BaseService>> svrDevices;
        public ServiceManager()
        {
            ResourceService = new SysResourceService();
            resultService = new ResultService();
            Services = new ObservableCollection<ServiceKind>();
            Devices = new ObservableCollection<BaseChannel>();
            svrDevices = new Dictionary<string, List<BaseService>>();

            UserConfig = GlobalSetting.GetInstance().SoftwareConfig.UserConfig;
            StackPanel = new StackPanel();

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Reload();


            Reload();
        }
        public ObservableCollection<BaseChannel> LastGenControl { get; set; }

        public void GenControl(ObservableCollection<BaseChannel> MQTTDevices)
        {
            LastGenControl = MQTTDevices;
            StackPanel.Children.Clear();
            foreach (var item in MQTTDevices)
            {
                if (item is BaseChannel device)
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
            LastGenControl = new ObservableCollection<BaseChannel>();
            StackPanel.Children.Clear();
            foreach (var serviceKind in Services)
            {
                foreach (var service in serviceKind.VisualChildren)
                {
                    foreach (var item in service.VisualChildren)  
                    {
                        if (item is BaseChannel device)
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
            this.Services.Clear();
            Devices.Clear();
            LastGenControl?.Clear();
            svrDevices?.Clear();
            List<SysResourceModel> Services = ResourceService.GetAllServices(UserConfig.TenantId);
            List<SysResourceModel> devices = ResourceService.GetAllDevices(UserConfig.TenantId);

            SysDictionaryService sysDictionaryService = new SysDictionaryService();

            var ServiceTypes = Enum.GetValues(typeof(ServiceType)).Cast<ServiceType>();

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

                ServiceKind mQTTServicetype = new ServiceKind();
                mQTTServicetype.Name = item.Name ?? string.Empty;
                mQTTServicetype.SysDictionaryModel = item;
                foreach (var service in Services)
                {
                    if (service.Type == item.Value)
                    {
                        ServiceTerminal mQTTService = new ServiceTerminal(service);
                        string svrKey = GetServiceKey(service.TypeCode ?? string.Empty, service.Code ?? string.Empty);

                        svrDevices ??= new Dictionary<string, List<BaseService>>();
                        if (!svrDevices.ContainsKey(svrKey))
                            svrDevices?.Add(svrKey, new List<BaseService>());

                        foreach (var device in devices)
                        {
                            BaseService svrObj = null;
                            if (device.Pid == service.Id)
                            {
                                switch ((ServiceType)device.Type)
                                {
                                    case ServiceType.Camera:

                                        if (mQTTService.BaseService is ServiceCamera cameraService)
                                        {
                                            DeviceCamera deviceCamera = new DeviceCamera(device, cameraService);
                                            svrObj = deviceCamera.DeviceService;
                                            mQTTService.AddChild(deviceCamera);
                                            Devices.Add(deviceCamera);
                                        }
                                        break;
                                    case ServiceType.PG:
                                        DevicePG devicePG = new DevicePG(device);
                                        svrObj = devicePG.DeviceService;
                                        mQTTService.AddChild(devicePG);
                                        Devices.Add(devicePG);
                                        break;
                                    case ServiceType.Spectum:
                                        DeviceSpectrum deviceSpectrum = new DeviceSpectrum(device);
                                        svrObj = deviceSpectrum.DeviceService;
                                        mQTTService.AddChild(deviceSpectrum);
                                        Devices.Add(deviceSpectrum);
                                        break;
                                    case ServiceType.SMU:
                                        DeviceSMU deviceSMU = new DeviceSMU(device);
                                        svrObj = deviceSMU.Service;
                                        mQTTService.AddChild(deviceSMU);
                                        Devices.Add(deviceSMU);
                                        break;
                                    case ServiceType.Sensor:
                                        DeviceSensor device1 = new DeviceSensor(device);
                                        svrObj = device1.DeviceService;
                                        mQTTService.AddChild(device1);
                                        Devices.Add(device1);
                                        break;
                                    case ServiceType.FileServer:
                                        DeviceFileServer img = new DeviceFileServer(device);
                                        svrObj = img.DeviceService;
                                        mQTTService.AddChild(img);
                                        Devices.Add(img);
                                        break;
                                    case ServiceType.Algorithm:
                                        DeviceAlgorithm alg = new DeviceAlgorithm(device);
                                        svrObj = alg.DeviceService;
                                        mQTTService.AddChild(alg);
                                        Devices.Add(alg);
                                        break;
                                    case ServiceType.Calibration:
                                        DeviceCalibration deviceCalibration = new DeviceCalibration(device);
                                        svrObj = deviceCalibration.DeviceService;
                                        mQTTService.AddChild(deviceCalibration);
                                        Devices.Add(deviceCalibration);
                                        break;
                                    case ServiceType.CfwPort:
                                        DeviceCfwPort deviceCfwPort = new DeviceCfwPort(device);
                                        svrObj = deviceCfwPort.DeviceService;
                                        mQTTService.AddChild(deviceCfwPort);
                                        Devices.Add(deviceCfwPort);
                                        break;
                                    case ServiceType.Motor:
                                        DeviceMotor deviceMotor = new DeviceMotor(device);
                                        svrObj = deviceMotor.DeviceService;
                                        mQTTService.AddChild(deviceMotor);
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
                        mQTTServicetype.AddChild(mQTTService);
                    }
                }
                this.Services.Add(mQTTServicetype);
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
                    fx = item.x,
                    fy = item.y,
                    fu = item.u,
                    fv = item.v,
                    fCCT = item.CCT,
                    dC = item.dC,
                    fLd = item.Ld,
                    fPur = item.Pur,
                    fLp = item.Lp,
                    fHW = item.HW,
                    fLav = item.Lav,
                    fRa = item.Ra,
                    fRR = item.RR,
                    fGR = item.GR,
                    fBR = item.BR,
                    fIp = item.Ip,
                    fPh = item.Ph,
                    fPhe = item.Phe,
                    fPlambda = item.Plambda,
                    fSpect1 = item.Spect1,
                    fSpect2 = item.Spect2,
                    fInterval = item.Interval,
                    fPL = JsonConvert.DeserializeObject<float[]>(item.PL ?? string.Empty) ?? System.Array.Empty<float>(),
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
                if (ctl is SpectrumDisplayControl spectrum)
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
