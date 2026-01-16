using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Templates.Flow;
using MQTTnet.Client;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class MQTTSMU : MQTTDeviceService<ConfigSMU>
    {
        public DeviceSMU Device { get; set; }
        public MQTTSMU(DeviceSMU deviceSMU, ConfigSMU sMUConfig) : base(sMUConfig)
        {
            Device = deviceSMU;
            Config = sMUConfig;

            SendTopic = sMUConfig.SendTopic;
            SubscribeTopic = sMUConfig.SubscribeTopic;

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                //Msg = "{\"ServiceVersion\":\"1.0\",\"EventName\":\"Scan\",\"ServiceName\":\"RC_local/SMU/SVR.SMU.Default/CMD\",\"DeviceName\":null,\"DeviceCode\":\"DEV.SMU.Default\",\"SerialNumber\":\"20251027T150635.2185024\",\"Code\":0,\"MsgID\":\"4504c606-531e-47ab-96d2-4b1bdb8d8ef5\",\"Data\":{\"VList\":[1.520087,2.581165,2.620323,2.648037,2.672302,2.693535,2.713528,2.73214,2.749928,2.767574],\"IList\":[0.0,0.011115,0.02222721,0.03333874,0.04445305,0.0555611,0.06666915,0.07778762,0.08889776,0.1000051],\"ScanList\":[0.0,0.011111111111111112,0.022222222222222223,0.033333333333333326,0.044444444444444446,0.05555555555555556,0.06666666666666665,0.07777777777777777,0.08888888888888889,0.1]}}";
                
                try
                {
                    MsgReturn msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (Config.Code != null && msg.DeviceCode != Config.Code) return Task.CompletedTask;

                    if (msg == null)
                        return Task.CompletedTask;
                    if (msg.Code != 0)
                    {
                        return Task.CompletedTask;
                    }

                    if (msg.EventName == "GetData")
                    {
                        if (msg != null && msg.Data != null && msg.Data.MasterId != null && msg.Data.MasterId > 0)
                        {
                            int masterId = msg.Data.MasterId;
                            var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
                            SMUResultModel model = DB.Queryable<SMUResultModel>().Where(x => x.Id == masterId).First();
                            DB.Dispose();
                            if (model != null)
                            {
                                ViewResultSMU viewResultSpectrum = new ViewResultSMU(model);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Device.View.AddViewResultSMU(viewResultSpectrum);
                                    Config.I = model.IResult;
                                    Config.V = model.VResult;

                                    foreach (var item in ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>())
                                    {
                                        item.DisplayConfig.V = Config.V ??0;
                                        item.DisplayConfig.I = Config.I ?? 0;
                                    }
                                });
                            }
                        }


                    }
                    else if (msg.EventName == "Scan")
                    {
                        Configs.SMUScanResultData data = JsonConvert.DeserializeObject<Configs.SMUScanResultData>(JsonConvert.SerializeObject(msg.Data));

                        //未来全面启用4.0之后移除
                        log.Info(FlowEngineManager.GetInstance().ServiceVersion);
                        if (FlowEngineManager.GetInstance().ServiceVersion >= new Version(4, 0, 2, 115))
                        {
                            if (msg != null && msg.Data != null && msg?.Data?.MasterId != null && msg?.Data?.MasterId > 0)
                            {
                                int masterId = msg.Data?.MasterId;
                                using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                                SmuScanModel model = DB.Queryable<SmuScanModel>().Where(x => x.Id == masterId).First();
                                log.Info($"GetData MasterId:{masterId} ");
                                if (model != null)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        ViewResultSMU viewResultSpectrum = new ViewResultSMU(model);
                                        Device.View.AddViewResultSMU(viewResultSpectrum);
                                    });
                                }


                            }
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ViewResultSMU viewResultSMU = new ViewResultSMU(Config.IsSourceV ? MeasurementType.Voltage : MeasurementType.Current, (float)Config.StopMeasureVal, data.VList, data.IList);
                                viewResultSMU.CreateTime = DateTime.Now;
                                viewResultSMU.Id = -1;
                                Device.View.AddViewResultSMU(viewResultSMU);
                            });
                        }

                    }
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }


        public bool SetParam()
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord Open(bool isNet, string devName)
        {
            var Params = new Dictionary<string, object>();

            MsgSend msg = new()
            {
                EventName = "Open",
                Params = Params
            };
            Params.Add("DevName",devName);
            Params.Add("IsNet", isNet);
            Params.Add("Channel", Config.Channel);
            return PublishAsyncClient(msg);
        }


        public MsgRecord? GetData(bool isSourceV, double measureVal, double lmtVal, SMUChannelType channel)
        {
            var Params = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "GetData",
                Params = Params
            };

            Params.Add("IsSourceV", isSourceV);
            Params.Add("MeasureValue", measureVal);
            Params.Add("LimitValue", lmtVal);
            Params.Add("Channel", channel);



            double V = isSourceV ? measureVal : lmtVal;
            double I = isSourceV ? lmtVal : measureVal;
            I /= 1000;

            V = Math.Abs(V);
            I = Math.Abs(I);

            if (Device.Config.DevType == "Keithley_2400")
            {
                if (V > 200)
                {
                    MessageBox.Show("Keithley 2450最大输出电压为200V，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if ((V > 20  && V <= 200) && (I > 0.1))
                {
                    MessageBox.Show("Keithley 2450在输出电压大于20V时，最大输出电流为100mA，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if (V <= 20 && I > 1)
                {
                    MessageBox.Show("Keithley 2450在输出电压小于20V时，最大输出电流为1A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }
            else if (Device.Config.DevType == "Keithley_2600")
            {
                if (V > 40)
                {
                    MessageBox.Show("Keithley 2604B最大输出电压为40V，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if ((V > 6 && V <= 40) && (I > 1))
                {
                    MessageBox.Show("Keithley 2604B在输出电压大于6V时，最大输出电流为1A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if (V <= 6 && I > 3)
                {
                    MessageBox.Show("Keithley 2604B在输出电压小于6V时，最大输出电流为3A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }
            else if (Device.Config.DevType == "Precise_S100")
            {
                if (V > 30)
                {
                    MessageBox.Show("Precise_S100最大输出电压为30V，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if (V <= 30 && I > 1)
                {
                    MessageBox.Show("Precise_S100在输出电压小于30V时，最大输出电流为1A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }

            return PublishAsyncClient(msg);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };
            return PublishAsyncClient(msg);
        }

        public class SMUScanParam
        {
            public bool IsSourceV { set; get; }
            public double BeginValue { set; get; }
            public double EndValue { set; get; }
            public double LimitValue { set; get; }
            public int Points { set; get; }

            public SMUChannelType Channel { get; set; }

        }
        public MsgRecord? Scan(bool isSourceV, double startMeasureVal, double stopMeasureVal, double lmtVal, int number, SMUChannelType channel)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var Params = new Dictionary<string, object>();
            Params.Add("DeviceParam", new SMUScanParam() { IsSourceV = isSourceV, BeginValue = startMeasureVal, EndValue = stopMeasureVal, LimitValue = lmtVal, Points = number, Channel = channel });
            MsgSend msg = new()
            {
                EventName = "Scan",
                SerialNumber = sn,
                Params = Params,
            };

            double V = isSourceV ? stopMeasureVal : lmtVal;
            double I = isSourceV ? lmtVal : stopMeasureVal;
            I /= 1000;

            V = Math.Abs(V);
            I = Math.Abs(I);

            if (Device.Config.DevType == "Keithley_2400")
            {
                if (V > 200)
                {
                    MessageBox.Show("Keithley 2450最大输出电压为200V，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if ((V > 20 && V <= 200) && (I > 0.1))
                {
                    MessageBox.Show("Keithley 2450在输出电压大于20V时，最大输出电流为100mA，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if (V <= 20 && I > 1)
                {
                    MessageBox.Show("Keithley 2450在输出电压小于20V时，最大输出电流为1A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }
            else if (Device.Config.DevType == "Keithley_2600")
            {
                if (V > 40)
                {
                    MessageBox.Show("Keithley 2600最大输出电压为40V，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if ((V > 6 && V <= 40) && (I > 1))
                {
                    MessageBox.Show("Keithley 2600在输出电压大于6V时，最大输出电流为1A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if (V <= 6 && I > 3)
                {
                    MessageBox.Show("Keithley 2600在输出电压小于6V时，最大输出电流为3A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }
            else if (Device.Config.DevType == "Precise_S100")
            {
                if (V > 30)
                {
                    MessageBox.Show("Precise_S100最大输出电压为30V，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                if (V <= 30 && I > 1)
                {
                    MessageBox.Show("Precise_S100在输出电压小于30V时，最大输出电流为1A，请调整测量参数后重试！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }


            return PublishAsyncClient(msg);
        }

        public MsgRecord CloseOutput()
        {
            var Params = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "CloseOutput",
                Params = Params,
            };
            Params.Add("Channel",Config.Channel);

            return PublishAsyncClient(msg);
        }
    }
}
