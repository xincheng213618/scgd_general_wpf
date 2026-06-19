#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
        public dynamic? Data { get; set; }
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
        string? Handle(NetworkStream stream, string request);
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
        public string? Dispatch(NetworkStream stream, string request)
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

    public enum SocketServerState
    {
        Disabled,
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
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
        private volatile bool _isStopRequested;

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
            TextDispatcher = new SocketTextDispatcher();
            MessageManager = SocketMessageManager.GetInstance();
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            Config.PropertyChanged += (_, _) => NotifyServerStatusChanged();
            TcpClients.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ClientCountText));
            ServerState = Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled;
        }

        /// <summary>
        /// Socket连接状态改变事件
        /// </summary>
        public event EventHandler<bool> SocketConnectChanged;

        /// <summary>
        /// 获取或设置当前连接状态
        /// </summary>
        public bool IsConnect
        {
            get => _IsConnect;
            private set
            {
                if (_IsConnect == value)
                    return;

                _IsConnect = value;
                OnPropertyChanged();
                NotifyServerStatusChanged();
                SocketConnectChanged?.Invoke(this, _IsConnect);
            }
        }
        private bool _IsConnect;

        public SocketServerState ServerState
        {
            get => _ServerState;
            private set
            {
                if (_ServerState == value)
                    return;

                _ServerState = value;
                OnPropertyChanged();
                NotifyServerStatusChanged();
            }
        }
        private SocketServerState _ServerState;

        public string ServerStateText
        {
            get
            {
                if (!Config.IsServerEnabled)
                    return Properties.Resources.Disabled;

                return ServerState switch
                {
                    SocketServerState.Starting => Properties.Resources.Starting,
                    SocketServerState.Running => Properties.Resources.Running,
                    SocketServerState.Stopping => Properties.Resources.Stopping,
                    SocketServerState.Error => Properties.Resources.OpenFailed,
                    _ => Properties.Resources.Stopped
                };
            }
        }

        public string EnabledStatusText => Config.IsServerEnabled ? Properties.Resources.Enabled : Properties.Resources.Disabled;

        public string OpenStatusText
        {
            get
            {
                if (!Config.IsServerEnabled)
                    return Properties.Resources.Stopped;

                return IsConnect
                    ? Properties.Resources.Running
                    : ServerState == SocketServerState.Error
                        ? Properties.Resources.OpenFailed
                        : Properties.Resources.Stopped;
            }
        }

        public string ListenAddress => $"{Config.IPAddress}:{Config.ServerPort}";

        public string ClientCountText => FormatResource(Properties.Resources.ClientCountFormat, TcpClients.Count);

        public string LastErrorMessage
        {
            get => _LastErrorMessage;
            private set
            {
                if (_LastErrorMessage == value)
                    return;

                _LastErrorMessage = value;
                OnPropertyChanged();
                NotifyServerStatusChanged();
            }
        }
        private string _LastErrorMessage = string.Empty;

        public DateTime? LastStatusChangedTime
        {
            get => _LastStatusChangedTime;
            private set
            {
                if (_LastStatusChangedTime == value)
                    return;

                _LastStatusChangedTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastStatusChangedText));
            }
        }
        private DateTime? _LastStatusChangedTime;

        public string LastStatusChangedText => LastStatusChangedTime.HasValue
            ? FormatResource(Properties.Resources.UpdatedAtFormat, LastStatusChangedTime.Value)
            : string.Empty;

        public bool HasLastError => Config.IsServerEnabled && ServerState == SocketServerState.Error && !string.IsNullOrWhiteSpace(LastErrorMessage);

        public string LastErrorDisplay => HasLastError ? LastErrorMessage : Properties.Resources.NoError;

        public string ServerSummary => $"{EnabledStatusText} / {OpenStatusText} / {ListenAddress}";

        private static string FormatResource(string format, params object?[] args)
        {
#pragma warning disable CA1863
            return string.Format(CultureInfo.CurrentUICulture, format, args);
#pragma warning restore CA1863
        }

        private void NotifyServerStatusChanged()
        {
            OnPropertyChanged(nameof(ServerStateText));
            OnPropertyChanged(nameof(EnabledStatusText));
            OnPropertyChanged(nameof(OpenStatusText));
            OnPropertyChanged(nameof(ListenAddress));
            OnPropertyChanged(nameof(ClientCountText));
            OnPropertyChanged(nameof(LastStatusChangedText));
            OnPropertyChanged(nameof(HasLastError));
            OnPropertyChanged(nameof(LastErrorDisplay));
            OnPropertyChanged(nameof(ServerSummary));
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        private void SetServerState(SocketServerState state)
        {
            RunOnUiThread(() =>
            {
                ServerState = state;
                LastStatusChangedTime = DateTime.Now;
            });
        }

        private void ClearLastError()
        {
            RunOnUiThread(() => LastErrorMessage = string.Empty);
        }

        private void SetLastError(string message)
        {
            RunOnUiThread(() =>
            {
                LastErrorMessage = message;
                LastStatusChangedTime = DateTime.Now;
                ServerState = SocketServerState.Error;
            });
        }

        /// <summary>
        /// 启动Socket服务器
        /// </summary>
        public void StartServer()
        {
            if (IsConnect || ServerState == SocketServerState.Starting)
            {
                NotifyServerStatusChanged();
                return;
            }

            _isStopRequested = false;
            ClearLastError();
            SetServerState(SocketServerState.Starting);
            Task.Run(() => CheckUpdate());
        }

        /// <summary>
        /// 停止Socket服务器
        /// </summary>
        public void StopServer()
        {
            _isStopRequested = true;
            ClearLastError();
            SetServerState(SocketServerState.Stopping);
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
                    SetServerState(Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled);
                }
                catch (Exception e)
                {
                    log.Error("Error stopping server: " + e.Message);
                    SetLastError(FormatResource(Properties.Resources.StopServerFailedFormat, e.Message));
                }
            }
            else
            {
                SetServerState(Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled);
            }
        }

        /// <summary>
        /// 已连接的TCP客户端集合
        /// </summary>
        public ObservableCollection<TcpClient> TcpClients { get; set; } = new ObservableCollection<TcpClient>();

        public void CheckUpdate()
        {
            TcpListener? listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Parse(Config.IPAddress), Config.ServerPort);
                tcpListener = listener;
                listener.Start();
                log.Info("Server started. Listening on port: " + Config.ServerPort);
                RunOnUiThread(() =>
                {
                    IsConnect = true;
                    ServerState = SocketServerState.Running;
                    LastStatusChangedTime = DateTime.Now;
                });
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    RunOnUiThread(() =>
                    {
                        TcpClients.Add(client);
                    });
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                    clientThread.Start(client);
                }
            }
            catch (SocketException e)
            {
                if (_isStopRequested || !Config.IsServerEnabled)
                {
                    log.Info("Socket server stopped: " + e.Message);
                }
                else
                {
                    log.Error("Socket server error on port " + Config.ServerPort + ": " + e.Message);
                    SetLastError(FormatResource(Properties.Resources.OpenListenAddressFailedFormat, ListenAddress, e.Message));
                }
            }
            catch (ObjectDisposedException)
            {
                log.Info("Socket server stopped.");
            }
            catch (Exception e)
            {
                log.Error("Socket server error: " + e.Message);
                SetLastError(FormatResource(Properties.Resources.OpenListenAddressFailedFormat, ListenAddress, e.Message));
            }
            finally
            {
                listener?.Stop();
                RunOnUiThread(() =>
                {
                    IsConnect = false;
                    if (_isStopRequested || !Config.IsServerEnabled)
                    {
                        ServerState = Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled;
                        LastErrorMessage = string.Empty;
                    }
                    else if (ServerState != SocketServerState.Error)
                    {
                        ServerState = Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled;
                    }
                });
                // 不修改 Config.IsServerEnabled，保留用户配置
                // 下次启动时仍会尝试开启服务器
            }
        }


        private void HandleClient(object? obj)
        {
            if (obj is not TcpClient client) return;

            string clientEndPoint = GetClientEndPoint(client);
            int bytesRead;
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = Config.SocketBufferSize > 1024 ? new byte[Config.SocketBufferSize] : new byte[1024];
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
            catch (IOException ex) when (IsClientDisconnect(ex))
            {
                log.Info("Socket client disconnected: " + clientEndPoint + ". " + ex.Message);
            }
            catch (SocketException ex) when (IsClientDisconnect(ex))
            {
                log.Info("Socket client disconnected: " + clientEndPoint + ". " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                log.Info("Socket client disposed: " + clientEndPoint + ". " + ex.Message);
            }
            catch (Exception ex)
            {
                log.Error("Client handling error: " + ex);
                client?.Close();
            }
            finally
            {
                RemoveClient(client);
                DisposeClient(client);
            }
        }

        private static string GetClientEndPoint(TcpClient client)
        {
            try
            {
                return client.Client?.RemoteEndPoint?.ToString()
                    ?? client.Client?.LocalEndPoint?.ToString()
                    ?? $"Client:{client.GetHashCode():X8}";
            }
            catch (Exception ex)
            {
                log.Warn("Unable to get socket client endpoint.", ex);
                return $"Client:{client.GetHashCode():X8}";
            }
        }

        private void RemoveClient(TcpClient client)
        {
            try
            {
                RunOnUiThread(() => TcpClients.Remove(client));
            }
            catch (Exception ex)
            {
                log.Warn("Error removing socket client.", ex);
            }
        }

        private static void DisposeClient(TcpClient client)
        {
            try
            {
                client.Close();
                client.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn("Error disposing socket client.", ex);
            }
        }

        private static bool IsClientDisconnect(Exception ex)
        {
            if (ex is SocketException socketException)
            {
                return IsClientDisconnect(socketException.SocketErrorCode);
            }

            return ex.InnerException is SocketException innerSocketException
                && IsClientDisconnect(innerSocketException.SocketErrorCode);
        }

        private static bool IsClientDisconnect(SocketError error)
        {
            return error == SocketError.ConnectionReset
                || error == SocketError.ConnectionAborted
                || error == SocketError.Shutdown
                || error == SocketError.OperationAborted;
        }
    }
}
