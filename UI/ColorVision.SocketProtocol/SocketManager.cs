#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace ColorVision.SocketProtocol
{

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
        private int _firewallRefreshVersion;

        /// <summary>
        /// Socket配置信息
        /// </summary>
        public SocketConfig Config { get; set; } = SocketConfig.Instance;

        /// <summary>
        /// 编辑配置命令
        /// </summary>
        public RelayCommand EditCommand { get; set; }

        /// <summary>
        /// 添加当前程序防火墙允许规则命令
        /// </summary>
        public RelayCommand AllowFirewallRuleCommand { get; set; }

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
            AllowFirewallRuleCommand = new RelayCommand(a => _ = AllowFirewallRuleAsync(a?.ToString()));
            Config.PropertyChanged += (_, _) =>
            {
                NotifyServerStatusChanged();
            };
            TcpClients.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ClientCountText));
            ServerState = Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled;
            RefreshNetworkAccessStatus();
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

        public string PrivateFirewallStatusText
        {
            get => _PrivateFirewallStatusText;
            private set
            {
                if (_PrivateFirewallStatusText == value)
                    return;

                _PrivateFirewallStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrivateFirewallTooltip));
            }
        }
        private string _PrivateFirewallStatusText = string.Empty;

        public string PrivateFirewallStatusDetail
        {
            get => _PrivateFirewallStatusDetail;
            private set
            {
                if (_PrivateFirewallStatusDetail == value)
                    return;

                _PrivateFirewallStatusDetail = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrivateFirewallTooltip));
            }
        }
        private string _PrivateFirewallStatusDetail = string.Empty;

        public bool CanAllowPrivateFirewall
        {
            get => _CanAllowPrivateFirewall;
            private set
            {
                if (_CanAllowPrivateFirewall == value)
                    return;

                _CanAllowPrivateFirewall = value;
                OnPropertyChanged();
            }
        }
        private bool _CanAllowPrivateFirewall;

        public string PrivateFirewallTooltip => PrivateFirewallStatusDetail;

        public string PublicFirewallStatusText
        {
            get => _PublicFirewallStatusText;
            private set
            {
                if (_PublicFirewallStatusText == value)
                    return;

                _PublicFirewallStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PublicFirewallTooltip));
            }
        }
        private string _PublicFirewallStatusText = string.Empty;

        public string PublicFirewallStatusDetail
        {
            get => _PublicFirewallStatusDetail;
            private set
            {
                if (_PublicFirewallStatusDetail == value)
                    return;

                _PublicFirewallStatusDetail = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PublicFirewallTooltip));
            }
        }
        private string _PublicFirewallStatusDetail = string.Empty;

        public bool CanAllowPublicFirewall
        {
            get => _CanAllowPublicFirewall;
            private set
            {
                if (_CanAllowPublicFirewall == value)
                    return;

                _CanAllowPublicFirewall = value;
                OnPropertyChanged();
            }
        }
        private bool _CanAllowPublicFirewall;

        public string PublicFirewallTooltip => PublicFirewallStatusDetail;

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
            OnPropertyChanged(nameof(PrivateFirewallStatusText));
            OnPropertyChanged(nameof(PrivateFirewallStatusDetail));
            OnPropertyChanged(nameof(CanAllowPrivateFirewall));
            OnPropertyChanged(nameof(PrivateFirewallTooltip));
            OnPropertyChanged(nameof(PublicFirewallStatusText));
            OnPropertyChanged(nameof(PublicFirewallStatusDetail));
            OnPropertyChanged(nameof(CanAllowPublicFirewall));
            OnPropertyChanged(nameof(PublicFirewallTooltip));
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

        private void RefreshNetworkAccessStatus()
        {
            _ = RefreshNetworkAccessStatusAsync();
        }

        private async Task RefreshNetworkAccessStatusAsync()
        {
            int refreshVersion = Interlocked.Increment(ref _firewallRefreshVersion);
            string? executablePath = GetCurrentExecutablePath();

            RunOnUiThread(() =>
            {
                PrivateFirewallStatusText = "检测中...";
                PrivateFirewallStatusDetail = "正在后台读取 Windows 防火墙规则。";
                CanAllowPrivateFirewall = false;
                PublicFirewallStatusText = "检测中...";
                PublicFirewallStatusDetail = "正在后台读取 Windows 防火墙规则。";
                CanAllowPublicFirewall = false;
            });

            try
            {
                FirewallProfileStatuses statuses = await Task.Run(() => SocketFirewallService.GetStatuses(executablePath)).ConfigureAwait(false);

                if (refreshVersion != Volatile.Read(ref _firewallRefreshVersion))
                    return;

                RunOnUiThread(() =>
                {
                    PrivateFirewallStatusText = statuses.PrivateStatus.Summary;
                    PrivateFirewallStatusDetail = statuses.PrivateStatus.Detail;
                    CanAllowPrivateFirewall = statuses.PrivateStatus.CanAllow;
                    PublicFirewallStatusText = statuses.PublicStatus.Summary;
                    PublicFirewallStatusDetail = statuses.PublicStatus.Detail;
                    CanAllowPublicFirewall = statuses.PublicStatus.CanAllow;
                });
            }
            catch (Exception ex)
            {
                if (refreshVersion != Volatile.Read(ref _firewallRefreshVersion))
                    return;

                RunOnUiThread(() =>
                {
                    PrivateFirewallStatusText = "无法读取";
                    PrivateFirewallStatusDetail = ex.Message;
                    CanAllowPrivateFirewall = false;
                    PublicFirewallStatusText = "无法读取";
                    PublicFirewallStatusDetail = ex.Message;
                    CanAllowPublicFirewall = false;
                });
            }
        }

        private static string? GetCurrentExecutablePath()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch (Exception ex)
            {
                log.Warn("Unable to get current executable path.", ex);
                return null;
            }
        }

        private async Task AllowFirewallRuleAsync(string? profile)
        {
            string? executablePath = GetCurrentExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法获取当前程序路径，不能创建防火墙规则。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            FirewallCommandResult allowResult = await SocketFirewallService.AllowApplicationAsync(executablePath, profile ?? "private").ConfigureAwait(true);
            RefreshNetworkAccessStatus();

            MessageBoxImage image = allowResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error;
            MessageBox.Show(Application.Current.GetActiveWindow(), allowResult.Message, "ColorVision", MessageBoxButton.OK, image);
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
            if (ServerState == SocketServerState.Stopping)
                return;

            _isStopRequested = true;
            ClearLastError();
            SetServerState(SocketServerState.Stopping);
            RunOnUiThread(() => IsConnect = false);
            Task.Run(StopServerCore);
        }

        private void StopServerCore()
        {
            TcpListener? listener = Interlocked.Exchange(ref tcpListener, null);
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                    log.Info("Server stopped.");
                    CloseConnectedClients();
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

        private void CloseConnectedClients()
        {
            List<TcpClient> clients = new();
            RunOnUiThread(() => clients = TcpClients.ToList());
            foreach (TcpClient item in clients)
            {
                try
                {
                    if (item.Connected)
                        item.Client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    log.Debug("Socket client shutdown skipped.", ex);
                }

                DisposeClient(item);
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
                    SetLastError(BuildOpenFailureMessage(e));
                }
            }
            catch (ObjectDisposedException)
            {
                log.Info("Socket server stopped.");
            }
            catch (Exception e)
            {
                log.Error("Socket server error: " + e.Message);
                SetLastError(BuildOpenFailureMessage(e));
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

        private string BuildOpenFailureMessage(Exception exception)
        {
            if (exception is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
            {
                return $"打开 {ListenAddress} 失败：端口 {Config.ServerPort} 已被占用，请关闭占用该端口的程序或在服务设置中更换端口。";
            }

            if (exception is SocketException { SocketErrorCode: SocketError.AccessDenied })
            {
                return $"打开 {ListenAddress} 失败：没有权限监听该地址，请检查监听地址或系统权限。";
            }

            return FormatResource(Properties.Resources.OpenListenAddressFailedFormat, ListenAddress, exception.Message);
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
