using ColorVision.Common.MVVM;
using ColorVision.SocketProtocol;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProjectARVRPro.Services
{
    public enum RelayMessageDirection
    {
        FlowToRelay,
        RelayToClient,
        ClientToRelay,
        RelayToFlow
    }

    public class RelayMessage : ViewModelBase
    {
        public DateTime Time { get; set; }
        public RelayMessageDirection Direction { get; set; }
        public string EventName { get; set; }
        public string Content { get; set; }

        public string DirectionText => Direction switch
        {
            RelayMessageDirection.FlowToRelay => "Flow → Relay",
            RelayMessageDirection.RelayToClient => "Relay → Client",
            RelayMessageDirection.ClientToRelay => "Client → Relay",
            RelayMessageDirection.RelayToFlow => "Relay → Flow",
            _ => Direction.ToString()
        };

        public override string ToString() => $"[{Time:HH:mm:ss.fff}] [{DirectionText}] {EventName}: {Content}";
    }

    public class SocketRelayConfig : ViewModelBase, IConfig
    {
        public static SocketRelayConfig Instance => ConfigService.Instance.GetRequiredService<SocketRelayConfig>();

        [Category("Server"), DisplayName("监听IP")]
        public string ListenIP { get => _ListenIP; set { _ListenIP = value; OnPropertyChanged(); } }
        private string _ListenIP = "127.0.0.1";

        [Category("Server"), DisplayName("监听端口")]
        public int ListenPort { get => _ListenPort; set { _ListenPort = value; OnPropertyChanged(); } }
        private int _ListenPort = 9200;

        [Category("Server"), DisplayName("超时(ms)")]
        public int TimeoutMs { get => _TimeoutMs; set { _TimeoutMs = value; OnPropertyChanged(); } }
        private int _TimeoutMs = 5000;

        [Category("Server"), DisplayName("开机自启")]
        public bool AutoStart { get => _AutoStart; set { _AutoStart = value; OnPropertyChanged(); } }
        private bool _AutoStart;
    }

    /// <summary>
    /// Socket中转服务器
    /// 作为TCP Server，Flow Engine作为Client连接进来。
    /// Flow发送消息 → 中转服务器 → 转发到SocketControl.Current.Stream(外部Client)
    /// 外部Client返回消息(通过ISocketJsonHandler) → 中转服务器 → 转给Flow
    /// </summary>
    public class SocketRelayManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketRelayManager));
        private static SocketRelayManager _instance;
        private static readonly object _locker = new();

        public static SocketRelayManager GetInstance() { lock (_locker) { return _instance ??= new SocketRelayManager(); } }

        public SocketRelayConfig Config => SocketRelayConfig.Instance;

        private TcpListener _listener;
        private TcpClient _flowClient;
        private NetworkStream _flowStream;
        private Thread _listenThread;
        private Thread _readThread;
        private volatile bool _running;
        private readonly object _sendLock = new();

        /// <summary>
        /// 等待外部Client响应的信号量, Flow发消息转给Client后, 等Client回消息再转给Flow
        /// </summary>
        private ManualResetEventSlim _responseWaiter = new(false);
        private SocketResponse _pendingResponse;

        public bool IsListening { get => _IsListening; private set { _IsListening = value; OnPropertyChanged(); } }
        private bool _IsListening;

        public bool IsFlowConnected { get => _IsFlowConnected; private set { _IsFlowConnected = value; OnPropertyChanged(); } }
        private bool _IsFlowConnected;

        public ObservableCollection<RelayMessage> Messages { get; set; } = new();

        public event Action<RelayMessage> MessageReceived;

        /// <summary>
        /// 启动中转服务器
        /// </summary>
        public void StartServer(string ip, int port)
        {
            StopServer();
            _running = true;
            Config.ListenIP = ip;
            Config.ListenPort = port;
            ConfigService.Instance.SaveConfigs();

            _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "RelayServerListener" };
            _listenThread.Start();
        }

        private void ListenLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Parse(Config.ListenIP), Config.ListenPort);
                _listener.Start();
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => IsListening = true);

                log.Info($"中转服务器启动, 监听 {Config.ListenIP}:{Config.ListenPort}");
                AddMessage(new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToFlow,
                    EventName = "System",
                    Content = $"服务器已启动, 监听 {Config.ListenIP}:{Config.ListenPort}"
                });

                while (_running)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    // 只保留最新的Flow连接
                    CloseFlowClient();
                    _flowClient = client;
                    _flowStream = client.GetStream();
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => IsFlowConnected = true);

                    string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    log.Info($"Flow已连接: {endpoint}");
                    AddMessage(new RelayMessage
                    {
                        Time = DateTime.Now,
                        Direction = RelayMessageDirection.FlowToRelay,
                        EventName = "System",
                        Content = $"Flow已连接: {endpoint}"
                    });

                    _readThread = new Thread(ReadFlowMessages) { IsBackground = true, Name = "RelayFlowReader" };
                    _readThread.Start();
                }
            }
            catch (SocketException ex) when (_running)
            {
                log.Error($"中转服务器异常: {ex.Message}");
            }
            finally
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => IsListening = false);
            }
        }

        /// <summary>
        /// 持续读取Flow发来的消息，转发给外部Client
        /// </summary>
        private void ReadFlowMessages()
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (_running && _flowStream != null)
                {
                    int bytesRead = _flowStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    log.Info($"收到Flow消息: {message}");

                    AddMessage(new RelayMessage
                    {
                        Time = DateTime.Now,
                        Direction = RelayMessageDirection.FlowToRelay,
                        EventName = TryGetEventName(message),
                        Content = message
                    });

                    // 转发给外部Client
                    ForwardToClient(message);
                }
            }
            catch (Exception ex) when (_running)
            {
                log.Error($"读取Flow消息异常: {ex.Message}");
                AddMessage(new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.FlowToRelay,
                    EventName = "Error",
                    Content = $"Flow连接断开: {ex.Message}"
                });
            }
            finally
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => IsFlowConnected = false);
            }
        }

        /// <summary>
        /// 将Flow的消息转发到外部Client (SocketControl.Current.Stream)
        /// </summary>
        private void ForwardToClient(string message)
        {
            var clientStream = SocketControl.Current.Stream;
            if (clientStream == null)
            {
                log.Warn("外部Client未连接, 无法转发");
                AddMessage(new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToClient,
                    EventName = "Error",
                    Content = "外部Client未连接, 无法转发"
                });
                return;
            }

            try
            {
                if (message == "1")
                {
                    var response = new SocketResponse
                    {
                        Version = "1.0",
                        MsgID = string.Empty,
                        EventName = "AoiSwitchPG",
                        Code = 0,
                        Msg = "AoiSwitchPG",
                    };

                    string respString = JsonConvert.SerializeObject(response);
                    log.Info(respString);
                    var sentMsg = new SocketMessage
                    {
                        Direction = SocketMessageDirection.Sent,
                        Content = respString,
                        MessageTime = DateTime.Now,
                        EventName = response.EventName,
                        MsgID = response.MsgID,
                        ResponseCode = response.Code
                    };
                    SocketMessageManager.GetInstance().AddMessage(sentMsg);
                    clientStream.Write(Encoding.UTF8.GetBytes(respString));
                    AddMessage(new RelayMessage
                    {
                        Time = DateTime.Now,
                        Direction = RelayMessageDirection.RelayToClient,
                        EventName = TryGetEventName(message),
                        Content = message
                    });
                    log.Info($"已转发给外部Client: {message}");
                }
                else
                {
                    byte[] sendBytes = Encoding.UTF8.GetBytes(message);
                    clientStream.Write(sendBytes, 0, sendBytes.Length);
                    clientStream.Flush();

                    AddMessage(new RelayMessage
                    {
                        Time = DateTime.Now,
                        Direction = RelayMessageDirection.RelayToClient,
                        EventName = TryGetEventName(message),
                        Content = message
                    });
                    log.Info($"已转发给外部Client: {message}");
                }

            }
            catch (Exception ex)
            {
                log.Error($"转发到外部Client失败: {ex.Message}");
                AddMessage(new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToClient,
                    EventName = "Error",
                    Content = $"转发失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 将外部Client的响应转发给Flow (由ISocketJsonHandler调用)
        /// </summary>
        public void ForwardToFlow(string message)
        {
            if (_flowStream == null || !IsFlowConnected)
            {
                log.Warn("Flow未连接, 无法转发");
                AddMessage(new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToFlow,
                    EventName = "Error",
                    Content = "Flow未连接, 无法转发"
                });
                return;
            }

            lock (_sendLock)
            {
                try
                {
                    byte[] sendBytes = Encoding.UTF8.GetBytes(message);
                    _flowStream.Write(sendBytes, 0, sendBytes.Length);
                    _flowStream.Flush();

                    AddMessage(new RelayMessage
                    {
                        Time = DateTime.Now,
                        Direction = RelayMessageDirection.RelayToFlow,
                        EventName = TryGetEventName(message),
                        Content = message
                    });
                    log.Info($"已转发给Flow: {message}");
                }
                catch (Exception ex)
                {
                    log.Error($"转发到Flow失败: {ex.Message}");
                    AddMessage(new RelayMessage
                    {
                        Time = DateTime.Now,
                        Direction = RelayMessageDirection.RelayToFlow,
                        EventName = "Error",
                        Content = $"转发失败: {ex.Message}"
                    });
                }
            }
        }

        /// <summary>
        /// 将外部Client的响应转发给Flow (SocketResponse对象版本)
        /// </summary>
        public void ForwardToFlow(SocketResponse response)
        {
            string json = JsonConvert.SerializeObject(response);
            ForwardToFlow(json);
        }

        /// <summary>
        /// 手动发送消息给Flow
        /// </summary>
        public void SendToFlow(string message)
        {
            ForwardToFlow(message);
        }

        /// <summary>
        /// 手动发送消息给外部Client
        /// </summary>
        public void SendToClient(string message)
        {
            ForwardToClient(message);
        }

        /// <summary>
        /// 停止中转服务器
        /// </summary>
        public void StopServer()
        {
            _running = false;
            try
            {
                CloseFlowClient();
                _listener?.Stop();
            }
            catch { }
            finally
            {
                _listener = null;
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    IsListening = false;
                    IsFlowConnected = false;
                });
            }
        }

        private void CloseFlowClient()
        {
            try
            {
                _flowStream?.Close();
                _flowClient?.Close();
            }
            catch { }
            finally
            {
                _flowStream = null;
                _flowClient = null;
            }
        }

        private string TryGetEventName(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<SocketMessageBase>(json);
                return obj?.EventName ?? "Unknown";
            }
            catch
            {
                return "RawText";
            }
        }

        private void AddMessage(RelayMessage msg)
        {
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                Messages.Add(msg);
                MessageReceived?.Invoke(msg);
            });
        }

        public void ClearMessages()
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                Messages.Clear();
            });
        }
    }
}
