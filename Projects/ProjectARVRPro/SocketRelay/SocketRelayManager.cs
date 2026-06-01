using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor;
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
        private readonly object _generalSensorResetPatchLock = new();
        private bool _generalSensorResetPatchCompletedForSocketOpen;
        private bool _generalSensorResetPatchRunning;

        private const string DefaultGeneralSensorCode = "DEV.Sensor.Default";
        private const string DefaultGeneralSensorCategory = "Sensor.Default";

        private SocketRelayManager()
        {
            ServiceManager.GetInstance().ServiceChanged += OnServiceChanged;
        }

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

        public void SetAutoStart(bool autoStart)
        {
            if (Config.AutoStart == autoStart)
            {
                return;
            }

            Config.AutoStart = autoStart;
            ConfigService.Instance.SaveConfigs();
        }

        private void ListenLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Parse(Config.ListenIP), Config.ListenPort);
                _listener.Start();
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => IsListening = true);
                TryRunGeneralSensorResetPatch();

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
                    SetFlowConnectionState(true);

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
                SetFlowConnectionState(false);
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
            ResetGeneralSensorResetPatchForSocketOpen();
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
                });
                SetFlowConnectionState(false);
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
                SetFlowConnectionState(false);
            }
        }

        private void SetFlowConnectionState(bool isConnected)
        {
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                IsFlowConnected = isConnected;
            });
        }

        private void ResetGeneralSensorResetPatchForSocketOpen()
        {
            lock (_generalSensorResetPatchLock)
            {
                _generalSensorResetPatchCompletedForSocketOpen = false;
            }
        }

        private void OnServiceChanged(object? sender, EventArgs e)
        {
            if (IsListening)
            {
                TryRunGeneralSensorResetPatch();
            }
        }

        private void TryRunGeneralSensorResetPatch()
        {
            lock (_generalSensorResetPatchLock)
            {
                if (_generalSensorResetPatchCompletedForSocketOpen || _generalSensorResetPatchRunning)
                {
                    return;
                }

                _generalSensorResetPatchRunning = true;
            }

            _ = RunGeneralSensorResetPatchAsync();
        }

        private async Task RunGeneralSensorResetPatchAsync()
        {
            bool completed = false;
            try
            {
                completed = await ApplyGeneralSensorResetPatchAsync();
            }
            finally
            {
                lock (_generalSensorResetPatchLock)
                {
                    if (completed)
                    {
                        _generalSensorResetPatchCompletedForSocketOpen = true;
                    }

                    _generalSensorResetPatchRunning = false;
                }
            }
        }

        // TEMP PATCH: Socket 服务打开后，重置一次通用传感器。
        // 后续后台修好连接状态判断后，可以连同本方法和相关 helper 一起删除。
        private static async Task<bool> ApplyGeneralSensorResetPatchAsync()
        {
            try
            {
                DeviceSensor? deviceSensor = await FindGeneralSensorAsync();
                if (deviceSensor == null)
                {
                    log.Info("Socket 打开后通用传感器尚未创建，暂不执行自动重置");
                    return false;
                }

                log.Info($"Socket 打开后开始重置通用传感器: {deviceSensor.Name} ({deviceSensor.Code})");
                deviceSensor.DService.Close();
                log.Info($"Socket 打开后已发送通用传感器关闭指令: {deviceSensor.Name} ({deviceSensor.Code})");

                await Task.Delay(1000);

                MsgRecord openRecord = deviceSensor.DService.Open();
                MsgRecordState openState = await WaitForMsgRecordAsync(openRecord, TimeSpan.FromSeconds(5));

                if (openState == MsgRecordState.Success)
                {
                    log.Info($"Socket 打开后重置通用传感器成功: {deviceSensor.Name} ({deviceSensor.Code})");
                    return true;
                }

                string failureMessage = BuildSensorResetFailureMessage(openRecord, openState);
                log.Warn($"Socket 打开后重置通用传感器失败: {deviceSensor.Name} ({deviceSensor.Code}), {failureMessage}");
                ShowSensorResetPrompt($"通用传感器自动重置失败：{failureMessage}\n请手动关闭后重新打开通用传感器。");
                return true;
            }
            catch (Exception ex)
            {
                log.Error("Socket 打开后重置通用传感器异常", ex);
                ShowSensorResetPrompt($"通用传感器自动重置异常：{ex.Message}\n请手动关闭后重新打开通用传感器。");
                return true;
            }
        }

        private static async Task<MsgRecordState> WaitForMsgRecordAsync(MsgRecord msgRecord, TimeSpan timeout)
        {
            if (IsTerminalMsgRecordState(msgRecord.MsgRecordState))
            {
                return msgRecord.MsgRecordState;
            }

            TaskCompletionSource<MsgRecordState> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? sender, MsgRecordState state)
            {
                if (IsTerminalMsgRecordState(state))
                {
                    taskCompletionSource.TrySetResult(state);
                }
            }

            msgRecord.MsgRecordStateChanged += Handler;
            try
            {
                if (IsTerminalMsgRecordState(msgRecord.MsgRecordState))
                {
                    return msgRecord.MsgRecordState;
                }

                Task completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(timeout));
                if (completedTask == taskCompletionSource.Task)
                {
                    return await taskCompletionSource.Task;
                }

                return MsgRecordState.Timeout;
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= Handler;
            }
        }

        private static async Task<DeviceSensor?> FindGeneralSensorAsync()
        {
            for (int i = 0; i < 20; i++)
            {
                DeviceSensor? deviceSensor = ServiceManager.GetInstance().DeviceServices
                    .OfType<DeviceSensor>()
                    .FirstOrDefault(x => string.Equals(x.Code, DefaultGeneralSensorCode, StringComparison.OrdinalIgnoreCase))
                    ?? ServiceManager.GetInstance().DeviceServices
                        .OfType<DeviceSensor>()
                        .FirstOrDefault(x => string.Equals(x.Config.Category, DefaultGeneralSensorCategory, StringComparison.OrdinalIgnoreCase));

                if (deviceSensor != null)
                {
                    return deviceSensor;
                }

                await Task.Delay(250);
            }

            return null;
        }

        private static bool IsTerminalMsgRecordState(MsgRecordState state)
        {
            return state == MsgRecordState.Success || state == MsgRecordState.Fail || state == MsgRecordState.Timeout;
        }

        private static string BuildSensorResetFailureMessage(MsgRecord msgRecord, MsgRecordState state)
        {
            return state switch
            {
                MsgRecordState.Fail => string.IsNullOrWhiteSpace(msgRecord.MsgReturn?.Message) ? "后台返回失败" : msgRecord.MsgReturn.Message,
                MsgRecordState.Timeout => "等待后台响应超时",
                _ => $"未知状态: {state}"
            };
        }

        private static void ShowSensorResetPrompt(string message)
        {
            var application = System.Windows.Application.Current;
            application?.Dispatcher?.BeginInvoke(() =>
            {
                System.Windows.Window? owner = null;
                foreach (System.Windows.Window window in application.Windows)
                {
                    if (!window.IsActive)
                    {
                        continue;
                    }

                    owner = window;
                    break;
                }

                owner ??= application.MainWindow;
                System.Windows.MessageBox.Show(owner, message, "ColorVision", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            });
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
