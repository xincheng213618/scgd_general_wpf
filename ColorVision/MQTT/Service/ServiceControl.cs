using ColorVision.Device.Camera;
using ColorVision.Device.PG;
using ColorVision.Device.Sensor;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT.Service
{
    public class ServiceControl
    {
        private static ServiceControl _instance;
        private static readonly object _locker = new();
        public static ServiceControl GetInstance() { lock (_locker) { return _instance ??= new ServiceControl(); } }

        public ObservableCollection<MQTTServiceKind> MQTTServices { get; set; }

        public ObservableCollection<MQTTDevice> MQTTDevices { get; set; }


        public SysResourceService ResourceService { get; set; }
        public SysDictionaryService DictionaryService { get; set; }

        public UserConfig UserConfig { get; set; }

        private ResultService resultService;

        public StackPanel MQTTStackPanel { get; set; }


        public ServiceControl()
        {
            ResourceService = new SysResourceService();
            DictionaryService = new SysDictionaryService();
            resultService = new ResultService();
            MQTTServices = new ObservableCollection<MQTTServiceKind>();
            MQTTDevices = new ObservableCollection<MQTTDevice>();
            UserConfig = GlobalSetting.GetInstance().SoftwareConfig.UserConfig;
            MQTTStackPanel = new StackPanel();
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Reload();
            Reload();
        }


        public void GenControl(ObservableCollection<MQTTDevice> MQTTDevices)
        {
            MQTTStackPanel.Children.Clear();
            foreach (var item in MQTTDevices)
            {
                if (item is DeviceCamera deviceCamera)
                {
                    MQTTCameraControl1 mQTTCameraControl = new MQTTCameraControl1(deviceCamera.CameraService);
                    deviceCamera.CameraService.FileHandler += (s, e) =>
                    {
                        if (ViewGridManager.GetInstance().Views[1] is ImageView imageView)
                        {
                            imageView.OpenImage(e);
                        }
                    };
                    MQTTStackPanel.Children.Add(mQTTCameraControl);

                }
                else if (item is DevicePG devicePG)
                {
                    MQTTPGControl mQTTPGControl = new MQTTPGControl(devicePG.PGService);
                    MQTTStackPanel.Children.Add(mQTTPGControl);
                }
                else if (item is DeviceSpectrum deviceSpectrum)
                {
                    MQTTSpectrumControl mQTTSpectrumControl = new MQTTSpectrumControl(deviceSpectrum);
                    MQTTStackPanel.Children.Add(mQTTSpectrumControl);
                }
                else if (item is DeviceSMU smu)
                {
                    MQTTSMUControl mQTTSMUControl = new MQTTSMUControl(smu);
                    MQTTStackPanel.Children.Add(mQTTSMUControl);
                }
                else if (item is DeviceSensor deviceSensor)
                {
                    HandyControl.Controls.Growl.Info("SensorService开发中");
                }

            }
        }

        public void GenContorl()
        {
            MQTTStackPanel.Children.Clear();
            foreach (var mQTTServiceKind in MQTTServices)
            {
                foreach (var mQTTService in mQTTServiceKind.VisualChildren)
                {
                    foreach (var item in mQTTService.VisualChildren)
                    {
                        if (item is DeviceCamera deviceCamera)
                        {
                            MQTTCameraControl1 mQTTCameraControl = new MQTTCameraControl1(deviceCamera.CameraService);
                            deviceCamera.CameraService.FileHandler += (s, e) =>
                            {
                                if (ViewGridManager.GetInstance().Views[1] is ImageView imageView)
                                {
                                    imageView.OpenImage(e);
                                }
                            };
                            MQTTStackPanel.Children.Add(mQTTCameraControl);

                        }
                        else if (item is DevicePG devicePG)
                        {
                            MQTTPGControl mQTTPGControl = new MQTTPGControl(devicePG.PGService);
                            MQTTStackPanel.Children.Add(mQTTPGControl);
                        }
                        else if (item is DeviceSpectrum deviceSpectrum)
                        {
                            MQTTSpectrumControl mQTTSpectrumControl = new MQTTSpectrumControl(deviceSpectrum);
                            MQTTStackPanel.Children.Add(mQTTSpectrumControl);
                        }
                        else if (item is DeviceSMU smu)
                        {
                            MQTTSMUControl mQTTSMUControl = new MQTTSMUControl(smu);
                            MQTTStackPanel.Children.Add(mQTTSMUControl);
                        }
                        else if (item is DeviceSensor deviceSensor)
                        {
                            HandyControl.Controls.Growl.Info("SensorService开发中");
                        }

                    }
                }
            }
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

            foreach (UserControl ctl in MQTTStackPanel.Children)
            {
                if (ctl is MQTTSpectrumControl spectrum)
                {
                    spectrum.SpectrumClear();
                    foreach (SpectumData data in datas)
                    {
                        spectrum.SpectrumDrawPlot(data);
                    }
                }
            }
        }


        public int ResultBatchSave(string sn)
        {
            BatchResultMasterModel model = new BatchResultMasterModel(sn, UserConfig.TenantId);
            return resultService.BatchSave(model);
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

                        foreach (var device in devices)
                        {
                            if (device.Pid == service.Id)
                            {

                                switch ((DeviceType)device.Type)
                                {
                                    case DeviceType.Camera:
                                        DeviceCamera deviceCamera = new DeviceCamera(device);
                                        mQTTService.AddChild(deviceCamera);
                                        MQTTDevices.Add(deviceCamera);
                                        break;
                                    case DeviceType.PG:
                                        DevicePG devicePG = new DevicePG(device);
                                        mQTTService.AddChild(devicePG);
                                        MQTTDevices.Add(devicePG);
                                        break;
                                    case DeviceType.Spectum:
                                        DeviceSpectrum deviceSpectrum = new DeviceSpectrum(device);
                                        mQTTService.AddChild(deviceSpectrum);
                                        MQTTDevices.Add(deviceSpectrum);
                                        break;
                                    case DeviceType.SMU:
                                        DeviceSMU deviceSMU = new DeviceSMU(device);
                                        mQTTService.AddChild(deviceSMU);
                                        MQTTDevices.Add(deviceSMU);
                                        break;
                                    case DeviceType.Sensor:
                                        DeviceSensor device1 = new DeviceSensor(device);
                                        mQTTService.AddChild(device1);
                                        MQTTDevices.Add(device1);
                                        break;
                                    default:
                                        break;
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
