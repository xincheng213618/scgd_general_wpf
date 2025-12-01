#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Extension;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Windows;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket消息基类
    /// </summary>
    public class SocketMessageBase
    {
        public string Version { get; set; }
        public string MsgID { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    /// <summary>
    /// Socket请求消息
    /// </summary>
    public class SocketRequest : SocketMessageBase
    {
        public string Params { get; set; }
    }

    /// <summary>
    /// Socket响应消息
    /// </summary>
    public class SocketResponse : SocketMessageBase
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public dynamic Data { get; set; }
    }


    /// <summary>
    /// JSON消息分发器
    /// 自动扫描并注册所有实现ISocketJsonHandler接口的处理器
    /// </summary>
    public class SocketJsonDispatcher
    {
        private readonly Dictionary<string, ISocketJsonHandler> _handlers = new();

        public SocketJsonDispatcher()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(ISocketJsonHandler).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is ISocketJsonHandler handler)
                    {
                        if (!_handlers.ContainsKey(handler.EventName))
                            _handlers[handler.EventName] = handler;
                    }
                }
            }
        }

        /// <summary>
        /// 分发Socket请求到对应的处理器
        /// </summary>
        /// <param name="stream">网络流</param>
        /// <param name="request">请求消息</param>
        /// <returns>响应消息</returns>
        public SocketResponse Dispatch(NetworkStream stream, SocketRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.EventName))
                return new SocketResponse { Code = 400, Msg = "Invalid request" };

            if (_handlers.TryGetValue(request.EventName, out var handler))
                return handler.Handle(stream, request);

            return new SocketResponse { Code = 404, Msg = "Handler not found for event: " + request.EventName };
        }
    }
    
    /// <summary>
    /// 文本消息分发器接口
    /// </summary>
    public interface ISocketTextDispatcher
    {
        string  Handle(NetworkStream stream, string request);
    }

    /// <summary>
    /// 文本消息分发器
    /// 自动扫描并注册所有实现ISocketTextDispatcher接口的处理器
    /// </summary>
    public class SocketTextDispatcher
    {
        private readonly List<ISocketTextDispatcher> _handlers = new List<ISocketTextDispatcher>();

        public SocketTextDispatcher()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(ISocketTextDispatcher).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    {
                        var displayNameAttr = type.GetCustomAttribute<DisplayNameAttribute>();
                        var eventName = displayNameAttr?.DisplayName ?? type.Name;
                        if (Activator.CreateInstance(type) is ISocketTextDispatcher handler)
                        {
                            _handlers.Add(handler);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 分发文本请求到对应的处理器
        /// </summary>
        /// <param name="stream">网络流</param>
        /// <param name="request">请求文本</param>
        /// <returns>响应文本</returns>
        public string Dispatch(NetworkStream stream, string request)
        {
            if(_handlers.Count > 0)
            {
                foreach (var handle in _handlers)
                {
                    string respose = handle.Handle(stream, request);
                    if (!string.IsNullOrWhiteSpace(respose))
                        return respose;
                    else
                        return null;
                }
            }
            return "No Dispatcher Hanle";
        }
    }


    /// <summary>
    /// Socket连接管理器
    /// 负责管理TCP服务器、客户端连接和消息分发
    /// </summary>
    public class SocketManager:ViewModelBase
    {
        private static ILog log = LogManager.GetLogger(typeof(SocketManager));
        private static SocketManager? _instance;
        private static readonly object _locker = new();
        
        /// <summary>
        /// 获取SocketManager单例实例
        /// </summary>
        /// <returns>SocketManager实例</returns>
        public static SocketManager GetInstance() { lock (_locker) { return _instance ??= new SocketManager(); } }

        private static TcpListener? tcpListener;
        
        /// <summary>
        /// Socket配置信息
        /// </summary>
        public SocketConfig Config { get; set; } = SocketConfig.Instance;

        /// <summary>
        /// 编辑配置命令
        /// </summary>
        public RelayCommand EditCommand { get; set; } 

        /// <summary>
        /// JSON消息分发器
        /// </summary>
        public SocketJsonDispatcher JsonDispatcher { get; set; }

        /// <summary>
        /// 文本消息分发器
        /// </summary>
        public SocketTextDispatcher TextDispatcher { get;set; }

        /// <summary>
        /// 消息管理器(用于持久化)
        /// </summary>
        public SocketMessageManager MessageManager { get; set; }
        
        public SocketManager()
        {
            JsonDispatcher = new SocketJsonDispatcher();
            TextDispatcher  = new SocketTextDispatcher();
            MessageManager = SocketMessageManager.GetInstance();
            EditCommand = new RelayCommand(a =>  new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        /// <summary>
        /// Socket连接状态改变事件
        /// </summary>
        public event EventHandler<bool> SocketConnectChanged;

        /// <summary>
        /// 获取或设置当前连接状态
        /// </summary>
        public bool IsConnect { get => _IsConnect; private set { if (_IsConnect == value) return;  _IsConnect = value; OnPropertyChanged(); SocketConnectChanged?.Invoke(this, _IsConnect); } }
        private bool _IsConnect;

        /// <summary>
        /// 启动Socket服务器
        /// </summary>
        public void StartServer()
        {
            Task.Run(() => CheckUpdate());
        }

        /// <summary>
        /// 停止Socket服务器
        /// </summary>
        public void StopServer()
        {
            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Stop();
                    IsConnect = false;
                    log.Info("Server stopped.");
                    foreach (var item in TcpClients)
                    {
                        if (item.Connected)
                        {
                            item.Client.Shutdown(SocketShutdown.Both);
                        }
                        item?.Close();
                        item?.Dispose();
                    }
                }
                catch (Exception e)
                {
                    log.Error("Error stopping server: " + e.Message);
                }
            }
        }

        /// <summary>
        /// 已连接的TCP客户端集合
        /// </summary>
        public ObservableCollection<TcpClient> TcpClients { get; set; } = new ObservableCollection<TcpClient>();

        public void CheckUpdate()
        {
            tcpListener = new TcpListener(IPAddress.Parse(Config.IPAddress), Config.ServerPort);
            try
            {
                tcpListener.Start();
                log.Info("Server started. Listening on port: " + Config.ServerPort);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnect = true;
                });
                while (true)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TcpClients.Add(client);
                    });
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                    clientThread.Start(client);
                }
            }
            catch (SocketException e)
            {
                log.Error(e);
            }
            finally
            {
                tcpListener.Stop();
                IsConnect = false;
                Config.IsServerEnabled = false;
            }
        }


        private void HandleClient(object? obj)
        {
            if (obj is not TcpClient client) return;

            NetworkStream stream = client.GetStream();
            string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

            byte[] buffer = Config.SocketBufferSize > 1024 ? new byte[Config.SocketBufferSize] : new byte[1024];
            int bytesRead;
            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // 创建接收消息记录并持久化
                    var receivedMsg = new SocketMessage
                    {
                        ClientEndPoint = clientEndPoint,
                        Direction = SocketMessageDirection.Received,
                        Content = message,
                        MessageTime = DateTime.Now
                    };
                    
                    log.Info("Received raw message: " + message);
                    switch (Config.SocketPhraseType)
                    {
                        case SocketPhraseType.Json:
                            SocketRequest? request = null;
                            try
                            {
                                request = JsonConvert.DeserializeObject<SocketRequest>(message);
                                receivedMsg.EventName = request?.EventName;
                                receivedMsg.MsgID = request?.MsgID;
                                
                                // 持久化接收消息
                                MessageManager.AddMessage(receivedMsg);

                                var response = JsonDispatcher.Dispatch(stream, request);

                                if (response != null)
                                {
                                    string respString = JsonConvert.SerializeObject(response);
                                    
                                    // 创建发送消息记录并持久化
                                    var sentMsg = new SocketMessage
                                    {
                                        ClientEndPoint = clientEndPoint,
                                        Direction = SocketMessageDirection.Sent,
                                        Content = respString,
                                        MessageTime = DateTime.Now,
                                        EventName = response.EventName,
                                        MsgID = response.MsgID,
                                        ResponseCode = response.Code
                                    };
                                    MessageManager.AddMessage(sentMsg);
                                    
                                    stream.Write(Encoding.UTF8.GetBytes(respString));
                                }
                            }
                            catch (Exception ex)
                            {
                                var response = new SocketResponse
                                {
                                    Version = request?.Version ?? "1.0",
                                    MsgID = request?.MsgID ?? "",
                                    EventName = request?.EventName ?? "",
                                    SerialNumber = request?.SerialNumber ?? "",
                                    Code = -1,
                                    Msg = ex.Message,
                                    Data = null
                                };
                                
                                // 持久化接收消息(即使出错)
                                MessageManager.AddMessage(receivedMsg);
                                
                                string respString = JsonConvert.SerializeObject(response);
                                
                                // 创建错误响应消息记录并持久化
                                var sentMsg = new SocketMessage
                                {
                                    ClientEndPoint = clientEndPoint,
                                    Direction = SocketMessageDirection.Sent,
                                    Content = respString,
                                    MessageTime = DateTime.Now,
                                    EventName = response.EventName,
                                    MsgID = response.MsgID,
                                    ResponseCode = response.Code
                                };
                                MessageManager.AddMessage(sentMsg);

                                byte[] respBytes = Encoding.UTF8.GetBytes(respString);
                                stream.Write(respBytes, 0, respBytes.Length);
                                continue;
                            }
                            break;
                        case SocketPhraseType.Text:
                            try
                            {
                                // 持久化接收消息
                                MessageManager.AddMessage(receivedMsg);
                                
                                var string1 = TextDispatcher.Dispatch(stream, message);
                                if (string1 != null)
                                {
                                    // 创建发送消息记录并持久化
                                    var sentMsg = new SocketMessage
                                    {
                                        ClientEndPoint = clientEndPoint,
                                        Direction = SocketMessageDirection.Sent,
                                        Content = string1,
                                        MessageTime = DateTime.Now
                                    };
                                    MessageManager.AddMessage(sentMsg);
                                    
                                    byte[] respBytes = Encoding.UTF8.GetBytes(string1);
                                    stream.Write(respBytes, 0, respBytes.Length);
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                                
                                // 创建错误响应消息记录并持久化
                                var sentMsg = new SocketMessage
                                {
                                    ClientEndPoint = clientEndPoint,
                                    Direction = SocketMessageDirection.Sent,
                                    Content = ex.Message,
                                    MessageTime = DateTime.Now
                                };
                                MessageManager.AddMessage(sentMsg);
                                
                                byte[] respBytes = Encoding.UTF8.GetBytes(ex.Message);
                                stream.Write(respBytes, 0, respBytes.Length);
                            }
                            break;
                        default:
                            // 默认情况下也持久化消息
                            MessageManager.AddMessage(receivedMsg);
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                log.Error("Client handling error: " + ex);
                client?.Close();
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TcpClients.Remove(client);
                });
                client?.Dispose();
            }
        }
    }
}
