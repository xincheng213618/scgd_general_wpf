using FlowEngineLib.MQTT;
using log4net;
using MQTTnet;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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


    public async Task<ResultData_MQTT> CreateMQTTClientAndStart(string mqttServerUrl, int port, string userName, string userPassword, Action<ResultData_MQTT> callback)
    {
        ResultData_MQTT resultData_MQTT;
        if (_MqttClient != null )
        {
            if (_MqttClient.IsConnected)
            {
                resultData_MQTT =  new ResultData_MQTT
                {
                    ResultCode = 1,
                    EventType = EventTypeEnum.ClientConnected,
                };
                callback.Invoke(resultData_MQTT);
                return resultData_MQTT;
            }
            else
            {
               await  _MqttClient.DisconnectAsync();
                _MqttClient.Dispose();
            }

        }
        var optionsBuilder = BuildClientOptions(mqttServerUrl, port, userName, userPassword);

        var options = optionsBuilder.Build();
        _MqttClient = new MqttClientFactory().CreateMqttClient();

        // v5 客户端事件
        _MqttClient.ConnectedAsync += ConnectedHandle;
        _MqttClient.DisconnectedAsync += DisconnectedHandle;
        _MqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandle;

        await _MqttClient.ConnectAsync(options);

        if (_MqttClient.IsConnected)
        {
            logger.Info($"执行了开启MQTTClient_成功！[{options.ChannelOptions}]");
        }
        else
        {
            logger.Error($"执行了开启MQTTClient_失败！无法连接。");
            resultData_MQTT = new ResultData_MQTT
            {
                ResultCode = 1,
                EventType = EventTypeEnum.ClientDisconnected,
            };
            callback.Invoke(resultData_MQTT);
            return resultData_MQTT;
        }
         resultData_MQTT = new ResultData_MQTT
        {
            ResultCode = 1,
            EventType = EventTypeEnum.ClientConnected,
        };
        callback.Invoke(resultData_MQTT);
        return resultData_MQTT;
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

    public bool IsClientConnect()
    {
        return _MqttClient != null && _MqttClient.IsConnected;
    }

    public async Task DisconnectAsync_Client()
    {

    }


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
}