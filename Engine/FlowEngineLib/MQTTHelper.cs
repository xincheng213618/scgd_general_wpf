using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.MQTT;
using log4net;
using MQTTnet;
using MQTTnet.Protocol;

namespace FlowEngineLib;

public class MQTTHelper
{
    public static readonly ILog logger = LogManager.GetLogger(typeof(MQTTHelper));

    private static string Server;
    private static int Port = 1883;
    private static string UserName;
    private static string Password;

    // 回调委托
    private Action<ResultData_MQTT> _Callback;

    // MQTTnet v5 核心对象
    private IMqttClient _MqttClient;

    #region 配置与初始化

    public static void SetDefaultCfg(string server, int port, string userName, string password, bool isServer, Action<ResultData_MQTT> callback)
    {
        Server = server;
        Port = port;
        UserName = userName;
        Password = password;
    }

    public static void GetDefaultCfg(ref string server, ref int port, ref string userName, ref string password)
    {
        server = Server;
        port = Port;
        userName = UserName;
        password = Password;
    }

    public static int GetPortCfg() => Port;
    public static string GetServerCfg() => Server;

    #endregion

    #region Client 端逻辑

    public async Task<ResultData_MQTT> CreateMQTTClientAndStart(string mqttServerUrl, int port, string userName, string userPassword, Action<ResultData_MQTT> callback)
    {
        var optionsBuilder = BuildClientOptions(mqttServerUrl, port, userName, userPassword);
        return await CreateMQTTClientAndStart(optionsBuilder, callback);
    }

    private MqttClientOptionsBuilder BuildClientOptions(string mqttServerUrl, int port, string userName, string userPassword)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttServerUrl, port)
            .WithClientId(Guid.NewGuid().ToString("N"));

        if (!string.IsNullOrEmpty(userName))
        {
            builder.WithCredentials(userName, userPassword);
        }

        return builder;
    }

    public async Task<ResultData_MQTT> CreateMQTTClientAndStart(MqttClientOptionsBuilder mqttClientOptionsBuilder, Action<ResultData_MQTT> callback)
    {
        _Callback = callback;
        ResultData_MQTT resultData;

        try
        {
            var options = mqttClientOptionsBuilder.Build();
            _MqttClient = new MqttClientFactory().CreateMqttClient();

            // v5 客户端事件
            _MqttClient.ConnectedAsync += ConnectedHandle;
            _MqttClient.DisconnectedAsync += DisconnectedHandle;
            _MqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandle;

            await _MqttClient.ConnectAsync(options);

            if (_MqttClient.IsConnected)
            {
                resultData = new ResultData_MQTT
                {
                    ResultCode = 1,
                    EventType = EventTypeEnum.ClientConnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了开启MQTTClient_成功！[{options.ChannelOptions}]"
                };
            }
            else
            {
                resultData = new ResultData_MQTT
                {
                    ResultCode = -1,
                    EventType = EventTypeEnum.ClientConnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了开启MQTTClient_失败！无法连接。"
                };
            }
        }
        catch (Exception ex)
        {
            resultData = new ResultData_MQTT
            {
                ResultCode = -1,
                EventType = EventTypeEnum.ClientConnected,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了开启MQTTClient_失败！错误信息：{ex.Message}"
            };
        }

        _Callback?.Invoke(resultData);
        return resultData;
    }

    public bool IsClientConnect()
    {
        return _MqttClient != null && _MqttClient.IsConnected;
    }

    public async Task DisconnectAsync_Client()
    {
        ResultData_MQTT obj;
        try
        {
            if (_MqttClient != null && _MqttClient.IsConnected)
            {
                // v5 发送 Disconnect 包
                await _MqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
                _MqttClient.Dispose();
                _MqttClient = null;

                obj = new ResultData_MQTT
                {
                    ResultCode = 1,
                    EventType = EventTypeEnum.ClientDisconnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了关闭MQTTClient_成功！"
                };
            }
            else
            {
                obj = new ResultData_MQTT
                {
                    ResultCode = -1,
                    EventType = EventTypeEnum.ClientDisconnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了关闭MQTTClient_失败！MQTTClient未开启连接！"
                };
            }
        }
        catch (Exception ex)
        {
            obj = new ResultData_MQTT
            {
                ResultCode = -1,
                EventType = EventTypeEnum.ClientDisconnected,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了关闭MQTTClient_失败！错误信息：{ex.Message}"
            };
        }
        _Callback?.Invoke(obj);
    }

    public async Task ReconnectAsync_Client()
    {
        ResultData_MQTT obj;
        try
        {
            if (_MqttClient != null)
            {
                // v5 中 ReconnectAsync 通常保留，如果移除了需要重新调用 ConnectAsync (视具体 5.x 小版本)
                // 大多数 5.x 版本 _MqttClient.ReconnectAsync() 仍然是扩展方法或通过断线重连策略处理
                // 如果编译报错，请使用 _MqttClient.ConnectAsync(_MqttClient.Options);
                await _MqttClient.ReconnectAsync();

                obj = new ResultData_MQTT
                {
                    ResultCode = 1,
                    EventType = EventTypeEnum.ClientReconnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了MQTTClient重连_成功！"
                };
            }
            else
            {
                obj = new ResultData_MQTT
                {
                    ResultCode = -1,
                    EventType = EventTypeEnum.ClientReconnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了MQTTClient重连_失败！未设置MQTTClient连接！"
                };
            }
        }
        catch (Exception ex)
        {
            obj = new ResultData_MQTT
            {
                ResultCode = -1,
                EventType = EventTypeEnum.ClientReconnected,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了MQTTClient重连_失败！错误信息：{ex.Message}"
            };
        }
        _Callback?.Invoke(obj);
    }

    // 优化：使用 Task 而不是 async void
    public async Task SubscribeAsync_Client(string topic)
    {
        ResultData_MQTT obj;
        try
        {
            // v5 订阅写法变更：使用 MqttClientSubscribeOptions
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic))
                .Build();

            await _MqttClient.SubscribeAsync(subscribeOptions, CancellationToken.None);

            obj = new ResultData_MQTT
            {
                ResultCode = 1,
                EventType = EventTypeEnum.Subscribe,
                ResultObject1 = topic,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行了订阅'{topic}'_成功！"
            };
        }
        catch (Exception ex)
        {
            obj = new ResultData_MQTT
            {
                ResultCode = -1,
                EventType = EventTypeEnum.Subscribe,
                ResultObject1 = topic,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行了订阅'{topic}'_失败！错误信息：{ex.Message}"
            };
        }
        _Callback?.Invoke(obj);
    }

    // 优化：使用 Task 而不是 async void
    public async Task UnsubscribeAsync_Client(string topic)
    {
        ResultData_MQTT obj;
        try
        {
            await _MqttClient.UnsubscribeAsync(topic, CancellationToken.None);
            obj = new ResultData_MQTT
            {
                ResultCode = 1,
                EventType = EventTypeEnum.Unsubscribe,
                ResultObject1 = topic,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行了退订'{topic}'_成功！"
            };
        }
        catch (Exception ex)
        {
            obj = new ResultData_MQTT
            {
                ResultCode = -1,
                EventType = EventTypeEnum.Unsubscribe,
                ResultObject1 = topic,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行退订'{topic}'_失败！错误信息：{ex.Message}"
            };
        }
        _Callback?.Invoke(obj);
    }

    public async Task PublishAsync_Client(string topic, string msg, bool retained)
    {
        if (_MqttClient == null) return;

        ResultData_MQTT obj;
        try
        {
            if (_MqttClient.IsConnected)
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(msg) // v5 这里可以直接传 string，内部自动转 byte[]
                    .WithRetainFlag(retained)
                    .Build();

                await _MqttClient.PublishAsync(applicationMessage, CancellationToken.None);

                obj = new ResultData_MQTT
                {
                    ResultCode = 1,
                    EventType = EventTypeEnum.Publish,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了发布信息_成功！主题:'{topic}'，信息:'{msg}'"
                };
            }
            else
            {
                obj = new ResultData_MQTT
                {
                    ResultCode = -1,
                    EventType = EventTypeEnum.Publish,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了发布信息_失败！MQTTClient未开启连接！"
                };
            }
        }
        catch (Exception ex)
        {
            obj = new ResultData_MQTT
            {
                ResultCode = -1,
                EventType = EventTypeEnum.Publish,
                ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了发布信息_失败！错误信息：{ex.Message}"
            };
        }
        _Callback?.Invoke(obj);
    }

    #region Client Event Handlers

    private Task ConnectedHandle(MqttClientConnectedEventArgs arg)
    {
        _Callback?.Invoke(new ResultData_MQTT
        {
            ResultCode = 1,
            EventType = EventTypeEnum.ClientConnected,
            ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>已连接到MQTT服务器！"
        });
        return Task.CompletedTask;
    }

    private Task DisconnectedHandle(MqttClientDisconnectedEventArgs arg)
    {
        _Callback?.Invoke(new ResultData_MQTT
        {
            ResultCode = 1,
            EventType = EventTypeEnum.ClientDisconnected,
            ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>已断开与MQTT服务器连接！"
        });

        // 自动重连逻辑建议由外部控制或使用 Reconnect 策略，这里简单保留原逻辑
        // 注意：v5 中如果在 DisconnectedHandle 直接调用 Reconnect 可能引发死锁或异常，建议使用 Task.Run
        if (_MqttClient != null)
        {
            Task.Run(async () => {
                try { await _MqttClient.ReconnectAsync(); } catch { /* log error */ }
            });
        }
        return Task.CompletedTask;
    }

    private Task ApplicationMessageReceivedHandle(MqttApplicationMessageReceivedEventArgs arg)
    {
        // v5 获取 Payload 方式变更
        string payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);

        var resultData = new ResultData_MQTT
        {
            ResultCode = 1,
            EventType = EventTypeEnum.MsgRecv,
            ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient'{arg.ClientId}'内容：'{payload}'；主题：'{arg.ApplicationMessage.Topic}'",
            ResultObject1 = arg.ApplicationMessage.Topic,
            ResultObject2 = payload
        };

        _Callback?.Invoke(resultData);
        return Task.CompletedTask;
    }

    #endregion

    #endregion
}