using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Server;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{

    public delegate void MQTTMsgHandle(ResultDataMQTT resultDataMQTT);
    /// <summary>
    /// MQTT通讯相关的工具类
    /// </summary>
    public class MQTTHelper
    {

        public event MQTTMsgHandle MsgHandle;


        #region Server
        /// <summary>
        /// MQTT服务
        /// </summary>
        public MqttServer MqttServer { get; set; }

        /// <summary>
        /// 创建MQTTServer并运行
        /// </summary>
        public async Task<ResultDataMQTT> CreateMQTTServerAndStart(MqttServerOptionsBuilder mqttServerOptionsBuilder)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                MqttServerOptions mqttServerOptions = mqttServerOptionsBuilder.Build();
                MqttServer = new MqttFactory().CreateMqttServer(mqttServerOptions);  // 创建服务（配置）

                MqttServer.StartedAsync += StartedHandle;  // 服务器开启事件
                MqttServer.StoppedAsync += StoppedHandle;  // 服务器关闭事件
                MqttServer.ClientConnectedAsync += ClientConnectedHandle;        // 设置客户端连接成功后的处理程序
                MqttServer.ClientDisconnectedAsync += ClientDisconnectedHandle;  // 设置客户端断开后的处理程序
                MqttServer.ClientSubscribedTopicAsync += ClientSubscribedTopicHandle;      // 设置消息订阅通知
                MqttServer.ClientUnsubscribedTopicAsync += ClientUnsubscribedTopicHandle;  // 设置消息退订通知
                MqttServer.ApplicationMessageNotConsumedAsync += ApplicationMessageNotConsumedHandle;  // 设置消息处理程序

                await MqttServer.StartAsync();  // 开启服务

                if (MqttServer.IsStarted)
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTServer_成功！"
                    };
                }
                else
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTServer_失败！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTServer_失败！错误信息：" + ex.Message
                };
            }

            MsgHandle?.Invoke(resultData_MQTT);
            return resultData_MQTT;
        }

        /// <summary>
        /// 简易创建MQTTServer并运行-不使用加密
        /// </summary>
        /// <param name="ip">IP</param>
        /// <param name="port">端口</param>
        /// <param name="withPersistentSessions">是否保持会话</param>
        /// <param name="callback">处理方法</param>
        /// <returns></returns>
        public async Task<ResultDataMQTT> CreateMQTTServerAndStart(string ip, int port, bool withPersistentSessions)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                MqttServerOptionsBuilder mqttServerOptionsBuilder = new MqttServerOptionsBuilder();  // MQTT服务器配置
                mqttServerOptionsBuilder.WithDefaultEndpoint();
                mqttServerOptionsBuilder.WithDefaultEndpointBoundIPAddress(IPAddress.Parse(ip));  // 设置Server的IP
                mqttServerOptionsBuilder.WithDefaultEndpointPort(port);                           // 设置Server的端口号
                //mqttServerOptionsBuilder.WithEncryptedEndpointPort(port);                        // 使用加密的端点端口
                mqttServerOptionsBuilder.WithPersistentSessions(withPersistentSessions);  // 持续会话
                mqttServerOptionsBuilder.WithConnectionBacklog(2000);                     // 最大连接数
                //mqttServerOptionsBuilder.WithConnectionValidator(c =>  // 鉴权-方法失效
                //{
                //    if (c.Username != uName || c.Password != uPwd)
                //    {
                //        c.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                //    }
                //})

                MqttServerOptions mqttServerOptions = mqttServerOptionsBuilder.Build();
                MqttServer = new MqttFactory().CreateMqttServer(mqttServerOptions);  // 创建服务（配置）

                MqttServer.StartedAsync += StartedHandle;  // 服务器开启事件
                MqttServer.StoppedAsync += StoppedHandle;  // 服务器关闭事件
                MqttServer.ClientConnectedAsync += ClientConnectedHandle;        // 设置客户端连接成功后的处理程序
                MqttServer.ClientDisconnectedAsync += ClientDisconnectedHandle;  // 设置客户端断开后的处理程序
                MqttServer.ClientSubscribedTopicAsync += ClientSubscribedTopicHandle;      // 设置消息订阅通知
                MqttServer.ClientUnsubscribedTopicAsync += ClientUnsubscribedTopicHandle;  // 设置消息退订通知
                MqttServer.ValidatingConnectionAsync += ValidatingConnectionHandle;                    // 鉴权-未完
                MqttServer.ApplicationMessageNotConsumedAsync += ApplicationMessageNotConsumedHandle;  // 设置消息处理程序

                await MqttServer.StartAsync();  // 开启服务

                if (MqttServer.IsStarted)
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTServer_成功！"
                    };
                }
                else
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTServer_失败！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTServer_失败！错误信息：" + ex.Message
                };
            }

            MsgHandle?.Invoke(resultData_MQTT);
            return resultData_MQTT;
        }

        /// <summary>
        /// 关闭MQTTServer
        /// </summary>
        public async Task<ResultDataMQTT> StopMQTTServer()
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();

            try
            {
                if (MqttServer == null)
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了关闭MQTTServer_出错！MQTTServer未在运行。"
                    };
                }
                else
                {
                    foreach (var clientStatus in MqttServer.GetClientsAsync().Result)
                    {
                        await clientStatus.DisconnectAsync();
                    }
                    await MqttServer.StopAsync();

                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了关闭MQTTServer_成功！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了关闭MQTTServer_失败！错误信息：" + ex.Message
                };
            }

            MsgHandle?.Invoke(resultData_MQTT);
            return resultData_MQTT;
        }

        /// <summary>
        /// 获取所有的客户端
        /// </summary>
        public List<MqttClientStatus> GetClientsAsync()
        {
            return MqttServer.GetClientsAsync().Result.ToList();
        }

        #region 处理事件
        /// <summary>
        /// 开启Server的处理程序
        /// </summary>
        private Task StartedHandle(EventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "MQTTServer已开启！"
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// 关闭Server的处理程序
        /// </summary>
        private Task StoppedHandle(EventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "MQTTServer已关闭！"
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置客户端连接成功后的处理程序
        /// </summary>
        private Task ClientConnectedHandle(ClientConnectedEventArgs arg)
        {
            var clients = MqttServer.GetClientsAsync().Result;

            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"客户端'{arg.ClientId}'已成功连接！当前客户端连接数：{clients?.Count}个。"
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置客户端断开后的处理程序
        /// </summary>
        private Task ClientDisconnectedHandle(ClientDisconnectedEventArgs arg)
        {
            var clients = MqttServer.GetClientsAsync().Result;
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"客户端'{arg.ClientId}'已断开连接！当前客户端连接数：{clients?.Count}个。"
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置消息订阅通知
        /// </summary>
        private Task ClientSubscribedTopicHandle(ClientSubscribedTopicEventArgs arg)
        {

            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"客户端'{arg.ClientId}'订阅了主题'{arg.TopicFilter.Topic}'，主题服务质量：'{arg.TopicFilter.QualityOfServiceLevel}'！"
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置消息退订通知
        /// </summary>
        private Task ClientUnsubscribedTopicHandle(ClientUnsubscribedTopicEventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"客户端{arg.ClientId}退订了主题{arg.TopicFilter}！"
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// 鉴权-未写完
        /// </summary>
        /// <returns></returns>
        private Task ValidatingConnectionHandle(ValidatingConnectionEventArgs arg)  // 鉴权
        {
            if (arg.UserName != "Admin" || arg.Password != "Admin123")
            {

            }
            return Task.CompletedTask;
        }

        public ApplicationMessageNotConsumedEventArgs ApplicationMessageNotConsumedEventArgs { get; set; }

        /// <summary>
        /// 设置消息处理程序
        /// </summary>
        private Task ApplicationMessageNotConsumedHandle(ApplicationMessageNotConsumedEventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = -1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"客户端：'{arg.SenderId}'发布了消息：主题：'{arg.ApplicationMessage.Topic}'！内容：'{Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment)}'；服务质量：{arg.ApplicationMessage.QualityOfServiceLevel}；保留：{arg.ApplicationMessage.Retain}"
            });

            return Task.CompletedTask;
        }
        #endregion 处理事件
        #endregion Server

        #region Client
        /// <summary>
        /// 客户端
        /// </summary>
        public IMqttClient MqttClient { get; set; }

        /// <summary>
        /// 创建MQTTClient并运行
        /// </summary>
        /// <param name="mqttClientOptionsBuilder">MQTTClient连接配置</param>
        /// <param name="callback">信息处理逻辑</param>
        /// <returns></returns>
        public async Task<ResultDataMQTT> CreateMQTTClientAndStart(MqttClientOptionsBuilder mqttClientOptionsBuilder)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                MqttClientOptions options = mqttClientOptionsBuilder.Build();

                MqttClient = new MqttFactory().CreateMqttClient();
                MqttClient.ConnectedAsync += ConnectedHandle;        // 服务器连接事件
                MqttClient.DisconnectedAsync += DisconnectedHandle;  // 服务器断开事件（可以写入重连事件）
                MqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandle;  // 发送消息事件
                await MqttClient.ConnectAsync(options);  // 连接

                if (MqttClient.IsConnected)
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTClient_成功！"
                    };
                }
                else
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTClient_失败！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTClient_失败！错误信息：" + ex.Message
                };
            }

            MsgHandle?.Invoke(resultData_MQTT);
            return resultData_MQTT;
        }

        public async Task<ResultDataMQTT> CreateMQTTClientAndStart(string mqttServerUrl, int port, string userName, string userPassword)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder();
                mqttClientOptionsBuilder.WithTcpServer(mqttServerUrl, port);          // 设置MQTT服务器地址
                if (!string.IsNullOrEmpty(userName))
                {
                    mqttClientOptionsBuilder.WithCredentials(userName, userPassword);  // 设置鉴权参数
                }
                mqttClientOptionsBuilder.WithClientId(Guid.NewGuid().ToString("N"));  // 设置客户端序列号
                MqttClientOptions options = mqttClientOptionsBuilder.Build();

                MqttClient = new MqttFactory().CreateMqttClient();
                MqttClient.ConnectedAsync += ConnectedHandle;        // 服务器连接事件
                MqttClient.DisconnectedAsync += DisconnectedHandle;  // 服务器断开事件（可以写入重连事件）
                MqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandle;  // 发送消息事件
                await MqttClient.ConnectAsync(options);  // 连接

                if (MqttClient.IsConnected)
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTClient_成功！"
                    };
                }
                else
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTClient_失败！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了开启MQTTClient_失败！错误信息：" + ex.Message
                };
            }
            MsgHandle?.Invoke(resultData_MQTT);
            return resultData_MQTT;
        }

        /// <summary>
        /// 关闭MQTTClient
        /// </summary>
        public async Task DisconnectAsyncClient()
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                if (MqttClient != null && MqttClient.IsConnected)
                {
                    await MqttClient.DisconnectAsync();
                    MqttClient.Dispose();

                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了关闭MQTTClient_成功！"
                    };
                }
                else
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了关闭MQTTClient_失败！MQTTClient未开启连接！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了关闭MQTTClient_失败！错误信息：" + ex.Message
                };
            }
            MsgHandle?.Invoke(resultData_MQTT);
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public async Task ReconnectAsyncClient()
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                if (MqttClient != null)
                {
                    await MqttClient.ReconnectAsync();
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了MQTTClient重连_成功！"
                    };
                }
                else
                {
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了MQTTClient重连_失败！未设置MQTTClient连接！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "执行了MQTTClient重连_失败！错误信息：" + ex.Message
                };
            }
            MsgHandle?.Invoke(resultData_MQTT);
        }

        /// <summary>
        /// 订阅
        /// </summary>
        /// <param name="topic">主题</param>
        public async void SubscribeAsyncClient(string topic)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                MqttTopicFilter topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
                await MqttClient.SubscribeAsync(topicFilter, CancellationToken.None);

                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = 1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"MQTTClient执行了订阅'{topic}'_成功！"
                };
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"MQTTClient执行了订阅'{topic}'_失败！错误信息：" + ex.Message
                };
            }
            MsgHandle?.Invoke(resultData_MQTT);
        }
        /// <summary>
        /// 退订阅
        /// </summary>
        /// <param name="topic">主题</param>
        public async void UnsubscribeAsyncClient(string topic)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();
            try
            {
                await MqttClient.UnsubscribeAsync(topic, CancellationToken.None);
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = 1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"MQTTClient执行了退订'{topic}'_成功！"
                };
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"MQTTClient执行退订'{topic}'_失败！错误信息：" + ex.Message
                };
            }
            MsgHandle?.Invoke(resultData_MQTT);
        }

        /// <summary>
        /// 发布消息( 必须在成功连接以后才生效 )
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="msg">信息</param>
        /// <param name="retained">是否保留</param>
        /// <returns></returns>
        public async Task PublishAsyncClient(string topic, string msg, bool retained)
        {
            ResultDataMQTT resultData_MQTT = new ResultDataMQTT();

            try
            {
                MqttApplicationMessageBuilder mqttApplicationMessageBuilder = new MqttApplicationMessageBuilder();
                mqttApplicationMessageBuilder.WithTopic(topic)          // 主题
                                            .WithPayload(msg)           // 信息
                                            .WithRetainFlag(retained);  // 保留

                MqttApplicationMessage messageObj = mqttApplicationMessageBuilder.Build();

                if (MqttClient.IsConnected)
                {
                    await MqttClient.PublishAsync(messageObj, CancellationToken.None);

                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = 1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"主题:'{topic}',信息:'{msg}'",
                        Topic = topic,
                        Payload = msg,
                    };
                }
                else
                {
                    // 未连接
                    resultData_MQTT = new ResultDataMQTT()
                    {
                        ResultCode = -1,
                        ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "失败 MQTTClient未开启连接！"
                    };
                }
            }
            catch (Exception ex)
            {
                resultData_MQTT = new ResultDataMQTT()
                {
                    ResultCode = -1,
                    ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "失败！错误信息：" + ex.Message
                };
            }

            MsgHandle?.Invoke(resultData_MQTT);
        }

        #region 事件
        /// <summary>
        /// 服务器连接事件
        /// </summary>
        private Task ConnectedHandle(MqttClientConnectedEventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + "已连接到MQTT服务器！"
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// 服务器断开事件（可以写入重连事件）
        /// </summary>
        private Task DisconnectedHandle(MqttClientDisconnectedEventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"已断开与MQTT服务器连接！"
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// 发送消息事件
        /// </summary>
        private Task ApplicationMessageReceivedHandle(MqttApplicationMessageReceivedEventArgs arg)
        {
            MsgHandle?.Invoke(new ResultDataMQTT()
            {
                ResultCode = 1,
                ResultMsg = DateTime.Now.ToString("HH:mm:ss.fff") + $"MQTTClient'{arg.ClientId}'内容：'{Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment)}'；主题：'{arg.ApplicationMessage.Topic}'，消息等级Qos：[{arg.ApplicationMessage.QualityOfServiceLevel}]，是否保留：[{arg.ApplicationMessage.Retain}]",
                Topic = arg.ApplicationMessage.Topic,
                Payload = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment)
            });
            return Task.CompletedTask;
        }
        #endregion 事件
        #endregion Client
    }

    /// <summary>
    /// 信息载体
    /// </summary>
    public class ResultDataMQTT
    {
        /// <summary>
        /// 结果Code
        /// 正常1，其他为异常；0不作为回复结果
        /// </summary>
        public int ResultCode { get; set; }

        /// <summary>
        /// 结果信息
        /// </summary>
        public string ResultMsg { get; set; } = string.Empty;

        /// <summary>
        /// 扩展1
        /// </summary>
        public object Topic { get; set; } = string.Empty;

        /// <summary>
        /// 扩展2
        /// </summary>
        public object Payload { get; set; } = string.Empty;
    }
}
