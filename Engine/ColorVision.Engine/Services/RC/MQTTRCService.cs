﻿#pragma warning disable CS8603
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using CVCommCore;
using FlowEngineLib;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTMessageLib.Util;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    /// <summary>
    /// 注册服务
    /// </summary>
    public class MqttRCService : MQTTServiceBase,IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(MqttRCService));
        private static MqttRCService _instance;
        private static readonly object _locker = new();
        public static MqttRCService GetInstance() { lock (_locker) { return _instance ??= new MqttRCService(); } }

        private string NodeName;
        private string NodeType;
        private static string AppId => RCSetting.Instance.Config.AppId;
        private static string AppSecret => RCSetting.Instance.Config.AppSecret;
        private static string RCNodeName => RCSetting.Instance.Config.RCName;
        private string DevcieName;
        private string RCRegTopic;
        private string RCHeartbeatTopic;
        private string RCPublicTopic;
        private string RCAdminTopic;
        private string ArchivedTopic;
        private string SysConfigTopic;
        private string SysConfigRespTopic;
        private NodeToken? Token;
        private bool TryTestRegist;

        public bool IsConnect { get => _IsConnect; set { if (_IsConnect == value) return;  _IsConnect = value; if (value) initialized = false; NotifyPropertyChanged(); } }
        private bool _IsConnect ;

        public List<MQTTServiceInfo> ServiceTokens { get; set; } = new List<MQTTServiceInfo>();

        private bool initialized;
        public event EventHandler ServiceTokensInitialized;
        System.Threading.Timer Timer { get; set; }

        public MqttRCService()
        {
            NodeType = "client";
            NodeName = MQTTRCServiceTypeConst.BuildNodeName(NodeType, null);
            DeviceCode = DevcieName = "dev." + NodeType + ".127.0.0.1";
            LoadCfg();
            ServiceName = Guid.NewGuid().ToString();
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.MQTTConnectChanged += (s,e) =>
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    GetInstance().ReRegist();
                });
            };
            int heartbeatTime = 2 * 1000;

            Timer = new Timer(e=> KeepLive(),null,1000,2000);
        }

        public void LoadCfg()
        {
            SubscribeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName, RCNodeName);
            SysConfigRespTopic = MQTTRCServiceTypeConst.BuildSysConfigRespTopic(RCNodeName);
            RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCNodeName);
            RCHeartbeatTopic = MQTTRCServiceTypeConst.BuildHeartbeatTopic(RCNodeName);
            RCPublicTopic = MQTTRCServiceTypeConst.BuildPublicTopic(RCNodeName);
            RCAdminTopic = MQTTRCServiceTypeConst.BuildAdminTopic(RCNodeName);
            ArchivedTopic = MQTTRCServiceTypeConst.BuildArchivedTopic(RCNodeName);
            SysConfigTopic = MQTTRCServiceTypeConst.BuildSysConfigTopic(RCNodeName);

            RCFileUpload.GetInstance().SendTopic = MQTTRCServiceTypeConst.BuildSysConfigTopic(RCNodeName); ;
            RCFileUpload.GetInstance().SubscribeTopic = MQTTRCServiceTypeConst.BuildSysConfigRespTopic(RCNodeName);

            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.SubscribeCache(SysConfigRespTopic);
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                LastAliveTime = DateTime.Now; ;

                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTNodeServiceHeader json = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(Msg);
                    if (json==null)
                        return Task.CompletedTask;

                    switch (json.EventName)
                    {
                        case MQTTNodeServiceEventEnum.Event_Regist:
                            MQTTNodeServiceRegistResponse resp = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(Msg);
                            break;
                        case MQTTNodeServiceEventEnum.Event_Startup:
                            MQTTNodeServiceStartupRequest req = JsonConvert.DeserializeObject<MQTTNodeServiceStartupRequest>(Msg);
                            if (req != null)
                            {
                                IsConnect = true;
                                Token = req.Data.Token;

                                if (!TryTestRegist)
                                {
                                    QueryServices();
                                }
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_QueryServices:
                            MQTTRCServicesQueryResponse respQurey = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(Msg);
                            if (respQurey != null)
                            {
                                Application.Current?.Dispatcher.BeginInvoke(() => {
                                    UpdateServices(respQurey.Data);
                                });
                            }

                            break;
                        case MQTTNodeServiceEventEnum.Event_QueryServiceStatus:
                            MQTTRCServiceStatusQueryResponse respStatus = JsonConvert.DeserializeObject<MQTTRCServiceStatusQueryResponse>(Msg);
                            if (respStatus != null)
                            {
                                UpdateServiceStatus(respStatus.Data);
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_NotRegist:
                            Regist();
                            break;
                    }

                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }
        public static void UpdateServiceStatus(List<MQTTNodeServiceStatus> services)
        {
            foreach (var serviceKind in ServiceManager.GetInstance().TypeServices.ToList())
            {
                foreach (var baseObject in serviceKind.VisualChildren)
                {
                    if (baseObject is TerminalService serviceTerminal)
                    {
                        foreach (var svr in services) 
                        {
                            bool updateTime = false;
                            DateTime lastLive = DateTime.Now;
                            if (string.Equals(svr.ServiceType, serviceKind.ServiceTypes.ToString(),StringComparison.OrdinalIgnoreCase) && svr.ServiceCode == serviceTerminal.Code)
                            {
                                if (!string.IsNullOrEmpty(svr.LiveTime))
                                {
                                    if (DateTime.TryParse(svr.LiveTime, out lastLive))
                                    {
                                        serviceTerminal.Config.LastAliveTime = lastLive;
                                        updateTime = true;
                                    }
                                }
                                if (svr.OverTime > 0) serviceTerminal.Config.HeartbeatTime = svr.OverTime;
                                foreach (var devNew in svr.DeviceList)
                                {
                                    foreach (var dev in serviceTerminal.VisualChildren)
                                    {
                                        if (dev is DeviceService baseChannel && baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                                        {
                                            if (devNew.Code == baseDeviceConfig.Code)
                                            {
                                                baseDeviceConfig.DeviceStatus = (DeviceStatusType)Enum.Parse(typeof(DeviceStatusType), devNew.Status);

                                                if (dev is DeviceService deviceService && deviceService.GetMQTTService() is MQTTServiceBase serviceBase)
                                                {
                                                    serviceBase.DeviceStatus = baseDeviceConfig.DeviceStatus;

                                                    if (serviceBase.DeviceStatus == DeviceStatusType.Unknown)
                                                    {
                                                    }
                                                }

                                                if(updateTime) baseDeviceConfig.LastAliveTime = lastLive;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }

                }

            }
        }
        public void UpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            DoUpdateServiceTokens(services);
            DoUpdateServices(services);
        }
        private void DoUpdateServiceTokens(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            log.Debug("Refresh Token");
            var tokens = ServiceTokens;
            tokens.Clear();
            foreach (var itemService in services.Values)
            {
                foreach (var nodeService in itemService)
                {
                    FlowEngineLib.MQTTServiceInfo serviceInfo = new()
                    {
                        ServiceType = nodeService.ServiceType,
                        ServiceCode = nodeService.ServiceCode,
                        PublishTopic = nodeService.UpChannel,
                        SubscribeTopic = nodeService.DownChannel,
                        Token = nodeService.ServiceToken,
                    };
                    foreach (var dev in nodeService.Devices)
                    {
                        serviceInfo.AddDevice(dev.Key, dev.Value.Code);
                    }

                    tokens.Add(serviceInfo);
                }
            }

            if (!initialized)
            {
                ServiceTokensInitialized?.Invoke(tokens, new EventArgs());
            }
            initialized = true;
        }

        public static void DoUpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> data)
        {
            foreach (var itemService in data)
            {
                ServiceTypes cvSType = EnumTool.ParseEnum<ServiceTypes>(itemService.Key.ToString());
                var serviceKind  = ServiceManager.GetInstance().TypeServices.FirstOrDefault(serviceKind => cvSType == serviceKind.ServiceTypes);
                if (serviceKind == null) { continue; }
                foreach (var baseObject in serviceKind.VisualChildren)
                {
                    if (baseObject is TerminalService serviceTerminal)
                    {
                        var nodeService = itemService.Value.FirstOrDefault(nodeService => nodeService.ServiceCode == serviceTerminal.Code);
                        if (nodeService == null) { continue; }
                        serviceTerminal.Config.SendTopic = nodeService.UpChannel;
                        serviceTerminal.Config.SubscribeTopic = nodeService.DownChannel;
                        if (nodeService.OverTime > 0) serviceTerminal.Config.HeartbeatTime = nodeService.OverTime;
                        serviceTerminal.MQTTServiceTerminalBase.ServiceToken = nodeService.ServiceToken;

                        foreach (var deviceObj in serviceTerminal.VisualChildren)
                        {
                            if (deviceObj is DeviceService baseChannel && baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                            {
                                baseDeviceConfig.ServiceToken = nodeService.ServiceToken;
                                baseDeviceConfig.SendTopic = nodeService.UpChannel;
                                baseDeviceConfig.SubscribeTopic = nodeService.DownChannel;
                                baseChannel.GetMQTTService()?.SubscribeCache();
                            }
                        }
                    }
                }
            }
        }
        public bool ReRegist()
        {
            LoadCfg();
            return Regist();
        }

        public bool Regist()
        {
            Token = null;
            IsConnect = false;
            ServiceTokens.Clear();

            MQTTNodeServiceRegist reg = new(NodeName, AppId, AppSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }
        public async Task <bool> Connect()
        {
            Regist();
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(10);
                if (IsConnect)
                    return true;
            }
            return false;
        }

        public void QueryServices()
        {
            if (Token != null)
            {
                MQTTRCServicesQueryRequest reg = new(NodeName, null, Token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void QueryServiceStatus()
        {
            if (Token != null)
            {
                MQTTRCServiceStatusQueryRequest reg = new(NodeName, null, Token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }


        public void KeepLive()
        {
            TimeSpan sp = DateTime.Now - LastAliveTime;
            if (sp.TotalMilliseconds > 10000)
            {
                Regist();
                return;
            }
            if (!IsConnect)
                return;

            List<DeviceHeartbeat> deviceStatues = new();
            deviceStatues.Add(new DeviceHeartbeat(DevcieName, DeviceStatusType.Opened.ToString()));
            string serviceHeartbeat = JsonConvert.SerializeObject(new MQTTServiceHeartbeat(NodeName, "", "", NodeType, ServiceName, deviceStatues, Token.AccessToken, (int)(2000 * 1.5f)));
            PublishAsyncClient(RCHeartbeatTopic, serviceHeartbeat);
            QueryServiceStatus();
        }

        public void RestartServices(string? nodeType = null, string? svrCode =null)
        {
            if (Token != null)
            {
                nodeType ??= string.Empty;
                MQTTRCServicesRestartRequest reg = svrCode == null ? new(AppId, NodeName, nodeType, Token.AccessToken) : new(AppId, NodeName, nodeType, Token.AccessToken, svrCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }
        public void RestartServices(string nodeType, string svrCode, string devCode)
        {
            if (Token != null)
            {
                MQTTRCServicesRestartRequest reg = new(AppId, NodeName, nodeType, Token.AccessToken, svrCode, devCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
            Task.Factory.StartNew(async () => {
                await Task.Delay(2000);
                QueryServices();
            });
        }

        public async Task<bool> TryRegist(RCServiceConfig cfg)
        {
            TryTestRegist = true;
            string RegTopic = MQTTRCServiceTypeConst.BuildRegTopic(cfg.RCName);
            string appId = cfg.AppId;
            string appSecret = cfg.AppSecret;
            IsConnect = false;
            MQTTNodeServiceRegist reg = new(NodeName, appId, appSecret, SubscribeTopic, NodeType);
            await PublishAsyncClient(RegTopic, JsonConvert.SerializeObject(reg));
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(10);
                if (IsConnect)
                {
                    TryTestRegist = false;
                    return true;
                }
            }
            TryTestRegist = false;
            return false;
        }

        public void Archived(string sn)
        {
            MQTTArchivedRequest request = new MQTTArchivedRequest(sn);
            PublishAsyncClient(ArchivedTopic, JsonConvert.SerializeObject(request));
        }
        public void ArchivedAll()
        {
            MQTTArchivedRequest request = new MQTTArchivedRequest();
            request.EventName = "ArchivedAll";
            PublishAsyncClient(ArchivedTopic, JsonConvert.SerializeObject(request));
        }

        public Task PublishAsyncClient(string topic, string json) => MQTTControl.PublishAsyncClient(topic, json, false);

        public override void Dispose()
        {
            Timer?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
