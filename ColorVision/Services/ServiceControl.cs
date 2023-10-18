using ColorVision.Device.Camera;
using ColorVision.Device.FileServer;
using ColorVision.Device.PG;
using ColorVision.Device.Sensor;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.Flow;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.RC;
using ColorVision.SettingUp;
using MQTTMessageLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using static cvColorVision.GCSDLL;

namespace ColorVision.Services
{
    public class ServiceControl
    {
        private static ServiceControl _instance;
        private static readonly object _locker = new();
        public static ServiceControl GetInstance() { lock (_locker) { return _instance ??= new ServiceControl(); } }



        public ObservableCollection<MQTTServiceKind> MQTTServices { get; set; }

        public ObservableCollection<BaseChannel> MQTTDevices { get; set; }


        public SysResourceService ResourceService { get; set; }
        public SysDictionaryService DictionaryService { get; set; }

        public UserConfig UserConfig { get; set; }

        private ResultService resultService;

        public StackPanel StackPanel { get; set; }

        private Dictionary<string, List<BaseService>> svrDevices;
        public RCService rcService { get;}
        public ServiceControl()
        {
            ResourceService = new SysResourceService();
            DictionaryService = new SysDictionaryService();
            resultService = new ResultService();
            MQTTServices = new ObservableCollection<MQTTServiceKind>();
            MQTTDevices = new ObservableCollection<BaseChannel>();
            svrDevices = new Dictionary<string, List<BaseService>>();
            rcService = new RCService(new RCConfig());


            int heartbeatTime = 10 * 1000;
            System.Timers.Timer hbTimer = new System.Timers.Timer(heartbeatTime);
            hbTimer.Elapsed += (s,e) => rcService.KeepLive(heartbeatTime);
            hbTimer.Enabled = true;

            GC.KeepAlive(hbTimer);

            UserConfig = GlobalSetting.GetInstance().SoftwareConfig.UserConfig;
            StackPanel = new StackPanel();

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Reload();

            Task.Run(() => rcService.Regist());
            MQTTControl.GetInstance().MQTTConnectChanged += (s, e) =>
            {
                Task.Run(() => rcService.Regist());
            };

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

        public void GenContorl()
        {
            LastGenControl = new ObservableCollection<BaseChannel>();
            StackPanel.Children.Clear();
            foreach (var mQTTServiceKind in MQTTServices)
            {
                foreach (var mQTTService in mQTTServiceKind.VisualChildren)
                {
                    foreach (var item in mQTTService.VisualChildren)
                    {
                        if (item is BaseChannel device)
                        {
                            LastGenControl.Add(device);
                            StackPanel.Children.Add(device.GetDisplayControl());
                        }
                    }
                }
            }
            LastGenControl = MQTTDevices;
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

        public BatchResultMasterModel GetResultBatch(string sn)
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

        private  static string GetServiceKey(string svrType,string svrCode)
        {
            return svrType +":"+ svrCode;
        }

        public void Reload()
        {
            MQTTServices.Clear();
            MQTTDevices.Clear();
            LastGenControl?.Clear();
            svrDevices?.Clear();
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
                        string svrKey = GetServiceKey(service.TypeCode, service.Code);
                        svrDevices?.Add(svrKey, new List<BaseService>());
                        foreach (var device in devices)
                        {
                            BaseService svrObj = null;
                            if (device.Pid == service.Id)
                            {
                                switch ((DeviceType)device.Type)
                                {
                                    case DeviceType.Camera:
                                        DeviceCamera deviceCamera = new DeviceCamera(device);
                                        svrObj = deviceCamera.Service;
                                        mQTTService.AddChild(deviceCamera);
                                        MQTTDevices.Add(deviceCamera);
                                        break;
                                    case DeviceType.PG:
                                        DevicePG devicePG = new DevicePG(device);
                                        svrObj = devicePG.PGService;
                                        mQTTService.AddChild(devicePG);
                                        MQTTDevices.Add(devicePG);
                                        break;
                                    case DeviceType.Spectum:
                                        DeviceSpectrum deviceSpectrum = new DeviceSpectrum(device);
                                        svrObj = deviceSpectrum.Service;
                                        mQTTService.AddChild(deviceSpectrum);
                                        MQTTDevices.Add(deviceSpectrum);
                                        break;
                                    case DeviceType.SMU:
                                        DeviceSMU deviceSMU = new DeviceSMU(device);
                                        svrObj = deviceSMU.Service;
                                        mQTTService.AddChild(deviceSMU);
                                        MQTTDevices.Add(deviceSMU);
                                        break;
                                    case DeviceType.Sensor:
                                        DeviceSensor device1 = new DeviceSensor(device);
                                        svrObj = device1.Service;
                                        mQTTService.AddChild(device1);
                                        MQTTDevices.Add(device1);
                                        break;
                                    case DeviceType.Image:
                                        DeviceFileServer img = new DeviceFileServer(device);
                                        svrObj = img.Service;
                                        mQTTService.AddChild(img);
                                        MQTTDevices.Add(img);
                                        break;
                                    default:
                                        break;
                                }
                            }

                            if (svrObj != null)
                            {
                                svrObj.ServiceName = service.Code;
                                svrDevices[svrKey].Add(svrObj);
                            }
                        }
                        mQTTServicetype.AddChild(mQTTService);
                    }
                }
                MQTTServices.Add(mQTTServicetype);
            }

        }

        public void ProcResult(FlowControlData flowControlData)
        {
            int totalTime = flowControlData.Params.TTL;
            resultService.BatchUpdateEnd(flowControlData.SerialNumber, totalTime, flowControlData.EventName);

            SpectrumDrawPlotFromDB(flowControlData.SerialNumber);
        }


        public void UpdateStatus(Dictionary<string, List<MQTTNodeService>> data)
        {
            foreach (var item in data)
            {
                foreach(var nodeService in item.Value)
                {
                    string svrKey = GetServiceKey(nodeService.ServiceType, nodeService.ServiceName);
                    if (svrDevices.ContainsKey(svrKey))
                    {
                        foreach (BaseService svr in svrDevices[svrKey])
                        {
                            svr.UpdateStatus(nodeService);
                        }
                    }
                }
            }
        }

        public void UpdateServiceStatus(Dictionary<string, List<MQTTNodeService>> data)
        {
            foreach (var item in data.Values)
            {
                foreach (var svr in item)
                {
                    UpdateServiceStatus(svr);
                }
            }
        }

        private void UpdateServiceStatus(MQTTNodeService svr)
        {
            UpdateServiceStatus(svr.ServiceName, svr.LiveTime, svr.OverTime);
        }

        public void UpdateServiceStatus(string serviceName, DateTime liveTime, int overTime)
        {
            foreach (var item in MQTTServices)
            {
                foreach (var svr in item.VisualChildren)
                {
                    if (svr is MQTTService service)
                    {
                        if (serviceName.Equals(service.Config.Code, StringComparison.Ordinal))
                        {
                            service.Config.SetLiveTime(liveTime, overTime, true);
                        }
                    }

                }
            }
        }
        public void UpdateServiceStatus(string serviceName, string liveTime, int overTime)
        {
            DateTime lvTime = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(liveTime))
            {
                if (!DateTime.TryParse(liveTime, out lvTime))
                {

                }
            }
            UpdateServiceStatus(serviceName, lvTime, overTime);
        }
    }
}
