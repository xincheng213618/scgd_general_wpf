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
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Server;
using Newtonsoft.Json;

namespace FlowEngineLib;

public class MQTTHelper
{
	public static readonly ILog logger = LogManager.GetLogger(typeof(MQTTHelper));

	private static string Server;

	private static int Port = 1883;

	private static string UserName;

	private static string Password;

	private Action<ResultData_MQTT> _Callback;

	private MqttServer _MqttServer;

	private IMqttClient _MqttClient;

	public static void SetDefaultCfg(string server, int port, string userName, string password, bool isServer, Action<ResultData_MQTT> callback)
	{
		Server = server;
		Port = port;
		UserName = userName;
		Password = password;
		if (isServer)
		{
			new MQTTHelper().CreateMQTTServerAndStart(Server, port, withPersistentSessions: true, callback);
		}
	}

	private static void OnServerCreateMsg(ResultData_MQTT obj)
	{
		logger.Debug(JsonConvert.SerializeObject(obj));
	}

	public static void GetDefaultCfg(ref string server, ref int port, ref string userName, ref string password)
	{
		server = Server;
		port = Port;
		userName = UserName;
		password = Password;
	}

	public static int GetPortCfg()
	{
		return Port;
	}

	public static string GetServerCfg()
	{
		return Server;
	}

	public async Task<ResultData_MQTT> CreateMQTTServerAndStart(MqttServerOptionsBuilder mqttServerOptionsBuilder, Action<ResultData_MQTT> callback)
	{
		new ResultData_MQTT();
		_Callback = callback;
		ResultData_MQTT resultData_MQTT2;
		try
		{
			MqttServerOptions mqttServerOptions = mqttServerOptionsBuilder.Build();
			_MqttServer = new MqttFactory().CreateMqttServer(mqttServerOptions);
			_MqttServer.StartedAsync += StartedHandle;
			_MqttServer.StoppedAsync += StoppedHandle;
			_MqttServer.ClientConnectedAsync += ClientConnectedHandle;
			_MqttServer.ClientDisconnectedAsync += ClientDisconnectedHandle;
			_MqttServer.ClientSubscribedTopicAsync += ClientSubscribedTopicHandle;
			_MqttServer.ClientUnsubscribedTopicAsync += ClientUnsubscribedTopicHandle;
			_MqttServer.ValidatingConnectionAsync += ValidatingConnectionHandle;
			_MqttServer.ApplicationMessageNotConsumedAsync += ApplicationMessageNotConsumedHandle;
			await _MqttServer.StartAsync();
			if (_MqttServer.IsStarted)
			{
				ResultData_MQTT resultData_MQTT = new ResultData_MQTT();
				resultData_MQTT.ResultCode = 1;
				resultData_MQTT.ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了开启MQTTServer_成功！[" + mqttServerOptions.DefaultEndpointOptions.BoundInterNetworkAddress.ToString() + ":" + mqttServerOptions.DefaultEndpointOptions.Port + "]";
				resultData_MQTT2 = resultData_MQTT;
			}
			else
			{
				resultData_MQTT2 = new ResultData_MQTT
				{
					ResultCode = -1,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了开启MQTTServer_失败！"
				};
			}
		}
		catch (Exception ex)
		{
			resultData_MQTT2 = new ResultData_MQTT
			{
				ResultCode = -1,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了开启MQTTServer_失败！错误信息：" + ex.Message
			};
		}
		_Callback?.Invoke(resultData_MQTT2);
		return resultData_MQTT2;
	}

	public async Task<ResultData_MQTT> CreateMQTTServerAndStart(string ip, int port, bool withPersistentSessions, Action<ResultData_MQTT> callback)
	{
		MqttServerOptionsBuilder mqttServerOptionsBuilder = new MqttServerOptionsBuilder();
		mqttServerOptionsBuilder.WithDefaultEndpoint();
		mqttServerOptionsBuilder.WithDefaultEndpointBoundIPAddress(IPAddress.Parse(ip));
		mqttServerOptionsBuilder.WithDefaultEndpointPort(port);
		mqttServerOptionsBuilder.WithPersistentSessions(withPersistentSessions);
		mqttServerOptionsBuilder.WithConnectionBacklog(2000);
		Task<ResultData_MQTT> task = CreateMQTTServerAndStart(mqttServerOptionsBuilder, callback);
		await task;
		return task.Result;
	}

	public async Task<ResultData_MQTT> StopMQTTServer()
	{
		new ResultData_MQTT();
		ResultData_MQTT resultData_MQTT;
		try
		{
			if (_MqttServer == null)
			{
				resultData_MQTT = new ResultData_MQTT
				{
					ResultCode = -1,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了关闭MQTTServer_出错！MQTTServer未在运行。"
				};
			}
			else
			{
				foreach (MqttClientStatus item in _MqttServer.GetClientsAsync().Result)
				{
					await item.DisconnectAsync();
				}
				await _MqttServer.StopAsync();
				_MqttServer = null;
				resultData_MQTT = new ResultData_MQTT
				{
					ResultCode = 1,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了关闭MQTTServer_成功！"
				};
			}
		}
		catch (Exception ex)
		{
			resultData_MQTT = new ResultData_MQTT
			{
				ResultCode = -1,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了关闭MQTTServer_失败！错误信息：" + ex.Message
			};
		}
		_Callback?.Invoke(resultData_MQTT);
		return resultData_MQTT;
	}

	public List<MqttClientStatus> GetClientsAsync()
	{
		return _MqttServer.GetClientsAsync().Result.ToList();
	}

	public Task SedMessage(string Topic, string msg)
	{
		return Task.CompletedTask;
	}

	private Task StartedHandle(EventArgs arg)
	{
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTServer已开启！"
		});
		return Task.CompletedTask;
	}

	private Task StoppedHandle(EventArgs arg)
	{
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTServer已关闭！"
		});
		return Task.CompletedTask;
	}

	private Task ClientConnectedHandle(ClientConnectedEventArgs arg)
	{
		IList<MqttClientStatus> result = _MqttServer.GetClientsAsync().Result;
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			EventType = EventTypeEnum.ClientConnected,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $">>>客户端'{arg.ClientId}'已成功连接！当前客户端连接数：{result?.Count}个。"
		});
		return Task.CompletedTask;
	}

	private Task ClientDisconnectedHandle(ClientDisconnectedEventArgs arg)
	{
		IList<MqttClientStatus> result = _MqttServer.GetClientsAsync().Result;
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			EventType = EventTypeEnum.ClientDisconnected,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $">>>客户端'{arg.ClientId}'已断开连接！当前客户端连接数：{result?.Count}个。"
		});
		return Task.CompletedTask;
	}

	private Task ClientSubscribedTopicHandle(ClientSubscribedTopicEventArgs arg)
	{
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			EventType = EventTypeEnum.Subscribe,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $">>>客户端'{arg.ClientId}'订阅了主题'{arg.TopicFilter.Topic}'，主题服务质量：'{arg.TopicFilter.QualityOfServiceLevel}'！"
		});
		return Task.CompletedTask;
	}

	private Task ClientUnsubscribedTopicHandle(ClientUnsubscribedTopicEventArgs arg)
	{
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			EventType = EventTypeEnum.Unsubscribe,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>客户端" + arg.ClientId + "退订了主题" + arg.TopicFilter + "！"
		});
		return Task.CompletedTask;
	}

	private Task ValidatingConnectionHandle(ValidatingConnectionEventArgs arg)
	{
		if (!(arg.UserName != "Admin"))
		{
			_ = arg.Password != "Admin123";
		}
		return Task.CompletedTask;
	}

	private Task ApplicationMessageNotConsumedHandle(ApplicationMessageNotConsumedEventArgs arg)
	{
		ResultData_MQTT resultData_MQTT = new ResultData_MQTT();
		resultData_MQTT.ResultCode = 1;
		resultData_MQTT.EventType = EventTypeEnum.Publish;
		resultData_MQTT.ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $">>>客户端：'{arg.SenderId}'发布了消息：主题：'{arg.ApplicationMessage.Topic}'！内容：'{Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment.Array)}'；服务质量：{arg.ApplicationMessage.QualityOfServiceLevel}；保留：{arg.ApplicationMessage.Retain}";
		ResultData_MQTT obj = resultData_MQTT;
		_Callback?.Invoke(obj);
		return Task.CompletedTask;
	}

	public async Task<ResultData_MQTT> CreateMQTTClientAndStart(MqttClientOptionsBuilder mqttClientOptionsBuilder, Action<ResultData_MQTT> callback)
	{
		new ResultData_MQTT();
		_Callback = callback;
		ResultData_MQTT resultData_MQTT;
		try
		{
			MqttClientOptions options = mqttClientOptionsBuilder.Build();
			_MqttClient = new MqttFactory().CreateMqttClient();
			_MqttClient.ConnectedAsync += ConnectedHandle;
			_MqttClient.DisconnectedAsync += DisconnectedHandle;
			_MqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandle;
			await _MqttClient.ConnectAsync(options);
			resultData_MQTT = ((!_MqttClient.IsConnected) ? new ResultData_MQTT
			{
				ResultCode = -1,
				EventType = EventTypeEnum.ClientConnected,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了开启MQTTClient_失败！[" + options.ChannelOptions.ToString() + "]"
			} : new ResultData_MQTT
			{
				ResultCode = 1,
				EventType = EventTypeEnum.ClientConnected,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了开启MQTTClient_成功！[" + options.ChannelOptions.ToString() + "]"
			});
		}
		catch (Exception ex)
		{
			resultData_MQTT = new ResultData_MQTT
			{
				ResultCode = -1,
				EventType = EventTypeEnum.ClientConnected,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了开启MQTTClient_失败！错误信息：" + ex.Message
			};
		}
		_Callback?.Invoke(resultData_MQTT);
		return resultData_MQTT;
	}

	private MqttClientOptionsBuilder buildOptions(string mqttServerUrl, int port, string userName, string userPassword)
	{
		MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder();
		mqttClientOptionsBuilder.WithTcpServer(mqttServerUrl, port);
		if (!string.IsNullOrEmpty(userName))
		{
			mqttClientOptionsBuilder.WithCredentials(userName, userPassword);
		}
		mqttClientOptionsBuilder.WithClientId(Guid.NewGuid().ToString("N"));
		return mqttClientOptionsBuilder;
	}

	public async Task<ResultData_MQTT> CreateMQTTClientAndStart(string mqttServerUrl, int port, string userName, string userPassword, Action<ResultData_MQTT> callback)
	{
		MqttClientOptionsBuilder mqttClientOptionsBuilder = buildOptions(mqttServerUrl, port, userName, userPassword);
		Task<ResultData_MQTT> task = CreateMQTTClientAndStart(mqttClientOptionsBuilder, callback);
		await task;
		return task.Result;
	}

	public bool IsClientConnect()
	{
		return _MqttClient.IsConnected;
	}

	public async Task DisconnectAsync_Client()
	{
		new ResultData_MQTT();
		ResultData_MQTT obj;
		try
		{
			if (_MqttClient != null && _MqttClient.IsConnected)
			{
				await _MqttClient.DisconnectAsync();
				_MqttClient.Dispose();
				_MqttClient = null;
				obj = new ResultData_MQTT
				{
					ResultCode = 1,
					EventType = EventTypeEnum.ClientDisconnected,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了关闭MQTTClient_成功！"
				};
			}
			else
			{
				obj = new ResultData_MQTT
				{
					ResultCode = -1,
					EventType = EventTypeEnum.ClientDisconnected,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了关闭MQTTClient_失败！MQTTClient未开启连接！"
				};
			}
		}
		catch (Exception ex)
		{
			obj = new ResultData_MQTT
			{
				ResultCode = -1,
				EventType = EventTypeEnum.ClientDisconnected,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了关闭MQTTClient_失败！错误信息：" + ex.Message
			};
		}
		_Callback?.Invoke(obj);
	}

	public async Task ReconnectAsync_Client()
	{
		new ResultData_MQTT();
		ResultData_MQTT obj;
		try
		{
			if (_MqttClient != null)
			{
				await _MqttClient.ReconnectAsync();
				obj = new ResultData_MQTT
				{
					ResultCode = 1,
					EventType = EventTypeEnum.ClientReconnected,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了MQTTClient重连_成功！"
				};
			}
			else
			{
				obj = new ResultData_MQTT
				{
					ResultCode = -1,
					EventType = EventTypeEnum.ClientReconnected,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了MQTTClient重连_失败！未设置MQTTClient连接！"
				};
			}
		}
		catch (Exception ex)
		{
			obj = new ResultData_MQTT
			{
				ResultCode = -1,
				EventType = EventTypeEnum.ClientReconnected,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了MQTTClient重连_失败！错误信息：" + ex.Message
			};
		}
		_Callback?.Invoke(obj);
	}

	public async void SubscribeAsync_Client(string topic)
	{
		new ResultData_MQTT();
		ResultData_MQTT obj;
		try
		{
			MqttTopicFilter topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
			await _MqttClient.SubscribeAsync(topicFilter, CancellationToken.None);
			obj = new ResultData_MQTT
			{
				ResultCode = 1,
				EventType = EventTypeEnum.Subscribe,
				ResultObject1 = topic,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTClient执行了订阅'" + topic + "'_成功！"
			};
		}
		catch (Exception ex)
		{
			ResultData_MQTT resultData_MQTT = new ResultData_MQTT();
			resultData_MQTT.ResultCode = -1;
			resultData_MQTT.EventType = EventTypeEnum.Subscribe;
			resultData_MQTT.ResultObject1 = topic;
			resultData_MQTT.ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTClient执行了订阅'" + topic + "'_失败！错误信息：" + ex.Message;
			obj = resultData_MQTT;
		}
		_Callback?.Invoke(obj);
	}

	public async void UnsubscribeAsync_Client(string topic)
	{
		new ResultData_MQTT();
		ResultData_MQTT obj;
		try
		{
			await _MqttClient.UnsubscribeAsync(topic, CancellationToken.None);
			obj = new ResultData_MQTT
			{
				ResultCode = 1,
				EventType = EventTypeEnum.Unsubscribe,
				ResultObject1 = topic,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTClient执行了退订'" + topic + "'_成功！"
			};
		}
		catch (Exception ex)
		{
			ResultData_MQTT resultData_MQTT = new ResultData_MQTT();
			resultData_MQTT.ResultCode = -1;
			resultData_MQTT.EventType = EventTypeEnum.Unsubscribe;
			resultData_MQTT.ResultObject1 = topic;
			resultData_MQTT.ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTClient执行退订'" + topic + "'_失败！错误信息：" + ex.Message;
			obj = resultData_MQTT;
		}
		_Callback?.Invoke(obj);
	}

	public async Task PublishAsync_Client(string topic, string msg, bool retained)
	{
		new ResultData_MQTT();
		ResultData_MQTT obj;
		try
		{
			MqttApplicationMessageBuilder mqttApplicationMessageBuilder = new MqttApplicationMessageBuilder();
			mqttApplicationMessageBuilder.WithTopic(topic).WithPayload(msg).WithRetainFlag(retained);
			MqttApplicationMessage applicationMessage = mqttApplicationMessageBuilder.Build();
			if (_MqttClient.IsConnected)
			{
				await _MqttClient.PublishAsync(applicationMessage, CancellationToken.None);
				ResultData_MQTT resultData_MQTT = new ResultData_MQTT();
				resultData_MQTT.ResultCode = 1;
				resultData_MQTT.EventType = EventTypeEnum.Publish;
				resultData_MQTT.ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了发布信息_成功！主题:'" + topic + "'，信息:'" + msg + "'";
				obj = resultData_MQTT;
			}
			else
			{
				obj = new ResultData_MQTT
				{
					ResultCode = -1,
					EventType = EventTypeEnum.Publish,
					ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了发布信息_失败！MQTTClient未开启连接！"
				};
			}
		}
		catch (Exception ex)
		{
			obj = new ResultData_MQTT
			{
				ResultCode = -1,
				EventType = EventTypeEnum.Publish,
				ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>执行了发布信息_失败！错误信息：" + ex.Message
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
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>已连接到MQTT服务器！"
		});
		return Task.CompletedTask;
	}

	private Task DisconnectedHandle(MqttClientDisconnectedEventArgs arg)
	{
		_Callback?.Invoke(new ResultData_MQTT
		{
			ResultCode = 1,
			EventType = EventTypeEnum.ClientDisconnected,
			ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>已断开与MQTT服务器连接！"
		});
		if (_MqttClient != null)
		{
			_MqttClient.ReconnectAsync();
		}
		return Task.CompletedTask;
	}

	private Task ApplicationMessageReceivedHandle(MqttApplicationMessageReceivedEventArgs arg)
	{
		ResultData_MQTT resultData_MQTT = new ResultData_MQTT();
		resultData_MQTT.ResultCode = 1;
		resultData_MQTT.EventType = EventTypeEnum.MsgRecv;
		resultData_MQTT.ResultMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ">>>MQTTClient'" + arg.ClientId + "'内容：'" + Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment.Array) + "'；主题：'" + arg.ApplicationMessage.Topic + "'";
		resultData_MQTT.ResultObject1 = arg.ApplicationMessage.Topic;
		resultData_MQTT.ResultObject2 = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment.Array);
		ResultData_MQTT obj = resultData_MQTT;
		_Callback?.Invoke(obj);
		return Task.CompletedTask;
	}
}
