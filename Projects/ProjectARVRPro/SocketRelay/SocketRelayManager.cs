#pragma warning disable CA1001,CA1822,CS0169,CS8625
using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.SocketProtocol;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.SocketRelay;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
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

    internal readonly record struct SocketRelaySensorResetResult(bool Completed, string? WarningMessage = null);

    internal sealed class SocketRelayRuntime
    {
        internal Action<Action>? StateDispatcher { get; init; }
        internal Func<IPAddress, int, SocketRelayGeneration>? GenerationFactory { get; init; }
        internal Func<string, SocketRelayWriteResult>? ExternalClientWriter { get; init; }
        internal Action<SocketMessage>? SocketMessagePublisher { get; init; }
        internal Func<Task<SocketRelaySensorResetResult>>? SensorResetOperation { get; init; }
        internal Action<string>? SensorResetPrompt { get; init; }
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
        private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(2);

        public static SocketRelayManager GetInstance() { lock (_locker) { return _instance ??= new SocketRelayManager(); } }

        private readonly object _lifecycleLock = new();
        private readonly object _generationSideEffectLock = new();
        private readonly SocketRelayConfig? _configOverride;
        private readonly bool _enableHostIntegration;
        private readonly bool _enableSensorReset;
        private readonly Action<Action>? _stateDispatcher;
        private readonly Func<IPAddress, int, SocketRelayGeneration> _generationFactory;
        private readonly Func<string, SocketRelayWriteResult> _externalClientWriter;
        private readonly Action<SocketMessage> _socketMessagePublisher;
        private readonly Func<Task<SocketRelaySensorResetResult>> _sensorResetOperation;
        private readonly Action<string> _sensorResetPrompt;
        private readonly object _sensorResetSingleFlightLock = new();
        private SocketRelayGeneration? _currentGeneration;
        private SocketRelayGeneration? _pendingSensorResetGeneration;
        private long _lifecycleVersion;
        private bool _sensorResetRunning;

        public SocketRelayConfig Config => _configOverride ?? SocketRelayConfig.Instance;
        public RelayCommand EditCommand { get; }

        /// <summary>
        /// 等待外部Client响应的信号量, Flow发消息转给Client后, 等Client回消息再转给Flow
        /// </summary>
        private ManualResetEventSlim _responseWaiter = new(false);
        private SocketResponse _pendingResponse;

        private const string DefaultGeneralSensorCode = "DEV.Sensor.Default";
        private const string DefaultGeneralSensorCategory = "Sensor.Default";

        private SocketRelayManager() : this(null, true, null)
        {
        }

        internal SocketRelayManager(
            SocketRelayConfig config,
            Action<Action>? stateDispatcher = null,
            Func<IPAddress, int, SocketRelayGeneration>? generationFactory = null)
            : this(config, false, new SocketRelayRuntime
            {
                StateDispatcher = stateDispatcher,
                GenerationFactory = generationFactory
            })
        {
        }

        internal SocketRelayManager(SocketRelayConfig config, SocketRelayRuntime runtime)
            : this(config, false, runtime)
        {
        }

        private SocketRelayManager(
            SocketRelayConfig? config,
            bool enableHostIntegration,
            SocketRelayRuntime? runtime)
        {
            _configOverride = config;
            _enableHostIntegration = enableHostIntegration;
            _enableSensorReset = enableHostIntegration || runtime?.SensorResetOperation != null;
            _stateDispatcher = runtime?.StateDispatcher;
            _generationFactory = runtime?.GenerationFactory ?? ((address, port) => new SocketRelayGeneration(address, port));
            _externalClientWriter = runtime?.ExternalClientWriter ?? WriteToExternalClient;
            _socketMessagePublisher = runtime?.SocketMessagePublisher ?? (message => SocketMessageManager.GetInstance().AddMessage(message));
            _sensorResetOperation = runtime?.SensorResetOperation ?? ApplyGeneralSensorResetPatchAsync;
            _sensorResetPrompt = runtime?.SensorResetPrompt ?? ShowSensorResetPrompt;
            EditCommand = new RelayCommand(_ => SocketRelayWindow.OpenWindow());
            if (enableHostIntegration)
            {
                ServiceManager.GetInstance().ServiceChanged += OnServiceChanged;
            }
        }

        public bool IsListening { get => _IsListening; private set { _IsListening = value; OnPropertyChanged(); } }
        private bool _IsListening;

        public bool IsFlowConnected { get => _IsFlowConnected; private set { _IsFlowConnected = value; OnPropertyChanged(); } }
        private bool _IsFlowConnected;

        public ObservableCollection<RelayMessage> Messages { get; set; } = new();

        public event Action<RelayMessage> MessageReceived;

        internal long? ActiveGenerationId => Volatile.Read(ref _currentGeneration)?.Id;

        internal long? ActiveFlowConnectionId => Volatile.Read(ref _currentGeneration)?.CurrentConnectionId;

        internal int ActiveFlowReaderCount => Volatile.Read(ref _currentGeneration)?.ActiveConnectionCount ?? 0;

        internal IPEndPoint? ListeningEndpoint => Volatile.Read(ref _currentGeneration)?.ListeningEndpoint;

        internal bool ActiveGenerationSensorResetCompleted => Volatile.Read(ref _currentGeneration)?.IsSensorResetCompleted == true;

        internal long? PendingSensorResetGenerationId
        {
            get
            {
                lock (_sensorResetSingleFlightLock)
                {
                    return _pendingSensorResetGeneration?.Id;
                }
            }
        }

        /// <summary>
        /// 启动中转服务器
        /// </summary>
        public void StartServer(string ip, int port)
        {
            IPAddress address = IPAddress.Parse(ip);
            Stopwatch stopDeadline = Stopwatch.StartNew();
            (SocketRelayGeneration? previousGeneration, long lifecycleVersion) = BeginLifecycleTransition();
            QueueStoppedState(lifecycleVersion);

            if (previousGeneration != null)
            {
                SocketRelayStopResult previousStop = previousGeneration.StopAndWait(GetRemainingTimeout(DefaultStopTimeout, stopDeadline));
                DisposeAfterStop(previousGeneration, previousStop);
                if (!previousStop.Completed)
                {
                    log.Warn($"旧中转服务器线程未在限定时间内退出，剩余线程数: {previousStop.RemainingWorkerCount}");
                }
            }

            if (!IsLifecycleIntentCurrent(lifecycleVersion))
            {
                return;
            }

            if (!TryRunForLifecycleIntent(lifecycleVersion, () => Config.ListenIP = ip))
            {
                return;
            }

            if (!TryRunForLifecycleIntent(lifecycleVersion, () => Config.ListenPort = port))
            {
                return;
            }

            if (_enableHostIntegration)
            {
                if (!TryRunForLifecycleIntent(lifecycleVersion, ConfigService.Instance.SaveConfigs))
                {
                    return;
                }
            }

            SocketRelayGeneration generation = CreateGeneration(address, port);
            if (!TryPublishGeneration(generation, lifecycleVersion))
            {
                DisposeAfterStop(generation, generation.StopAndWait(GetRemainingTimeout(DefaultStopTimeout, stopDeadline)));
                return;
            }

            QueueGenerationStartingState(generation, lifecycleVersion);
            if (!TryStartPublishedGeneration(generation, lifecycleVersion, out Exception? startError))
            {
                if (startError != null)
                {
                    ExceptionDispatchInfo.Capture(startError).Throw();
                }

                return;
            }
        }

        private (SocketRelayGeneration? Generation, long LifecycleVersion) BeginLifecycleTransition()
        {
            lock (_lifecycleLock)
            {
                SocketRelayGeneration? generation = _currentGeneration;
                _currentGeneration = null;
                long lifecycleVersion = ++_lifecycleVersion;

                // Cancellation happens in the same short critical section as the detach. It does
                // not publish manager state or invoke manager subscribers synchronously.
                generation?.RequestStop();
                return (generation, lifecycleVersion);
            }
        }

        private bool IsLifecycleIntentCurrent(long lifecycleVersion)
        {
            return Volatile.Read(ref _lifecycleVersion) == lifecycleVersion;
        }

        private bool TryRunForLifecycleIntent(long lifecycleVersion, Action action)
        {
            lock (_generationSideEffectLock)
            {
                if (!IsLifecycleIntentCurrent(lifecycleVersion))
                {
                    return false;
                }

                action();
                return IsLifecycleIntentCurrent(lifecycleVersion);
            }
        }

        private bool TryPublishGeneration(SocketRelayGeneration generation, long lifecycleVersion)
        {
            // Old-generation side effects and new-generation publication share this gate. A
            // callback that entered first is linearized before publication; one that enters later
            // sees the new generation and is ignored.
            lock (_generationSideEffectLock)
            {
                lock (_lifecycleLock)
                {
                    if (_lifecycleVersion != lifecycleVersion || _currentGeneration != null)
                    {
                        generation.RequestStop();
                        return false;
                    }

                    _currentGeneration = generation;
                    return true;
                }
            }
        }

        private bool TryStartPublishedGeneration(
            SocketRelayGeneration generation,
            long lifecycleVersion,
            out Exception? startError)
        {
            startError = null;
            long stoppedVersion = 0;

            lock (_lifecycleLock)
            {
                if (_lifecycleVersion != lifecycleVersion || !ReferenceEquals(_currentGeneration, generation))
                {
                    return false;
                }

                try
                {
                    generation.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    _currentGeneration = null;
                    stoppedVersion = ++_lifecycleVersion;
                    generation.RequestStop();
                    startError = ex;
                }
            }

            DisposeAfterStop(generation, generation.StopAndWait(DefaultStopTimeout));
            QueueStoppedState(stoppedVersion);
            return false;
        }

        public void SetAutoStart(bool autoStart)
        {
            if (Config.AutoStart == autoStart)
            {
                return;
            }

            Config.AutoStart = autoStart;
            if (_enableHostIntegration)
            {
                ConfigService.Instance.SaveConfigs();
            }
        }

        private SocketRelayGeneration CreateGeneration(IPAddress address, int port)
        {
            SocketRelayGeneration generation = _generationFactory(address, port);
            generation.Listening += OnGenerationListening;
            generation.ListeningStopped += OnGenerationListeningStopped;
            generation.FlowConnected += OnGenerationFlowConnected;
            generation.FlowDisconnected += OnGenerationFlowDisconnected;
            generation.FlowMessageReceived += OnGenerationFlowMessageReceived;
            generation.ListenerError += OnGenerationListenerError;
            generation.FlowReadError += OnGenerationFlowReadError;
            return generation;
        }

        private void OnGenerationListening(SocketRelayGeneration generation)
        {
            if (!IsCurrentGeneration(generation))
            {
                return;
            }

            QueueListeningState(generation, true);
            if (_enableSensorReset)
            {
                TryRunGeneralSensorResetPatch(generation);
            }

            IPEndPoint endpoint = generation.ListeningEndpoint ?? new IPEndPoint(IPAddress.Parse(Config.ListenIP), Config.ListenPort);
            log.Info($"中转服务器启动, 监听 {endpoint.Address}:{endpoint.Port}");
            AddMessage(generation, new RelayMessage
            {
                Time = DateTime.Now,
                Direction = RelayMessageDirection.RelayToFlow,
                EventName = "System",
                Content = $"服务器已启动, 监听 {endpoint.Address}:{endpoint.Port}"
            });
        }

        private void OnGenerationListeningStopped(SocketRelayGeneration generation)
        {
            QueueListeningState(generation, false);
        }

        private void OnGenerationFlowConnected(SocketRelayGeneration generation, SocketRelayConnection connection)
        {
            if (!IsCurrentConnection(generation, connection))
            {
                return;
            }

            QueueFlowConnectionState(generation, connection, true);
            log.Info($"Flow已连接: {connection.RemoteEndpoint}");
            AddMessage(generation, new RelayMessage
            {
                Time = DateTime.Now,
                Direction = RelayMessageDirection.FlowToRelay,
                EventName = "System",
                Content = $"Flow已连接: {connection.RemoteEndpoint}"
            });
        }

        private void OnGenerationFlowDisconnected(SocketRelayGeneration generation, SocketRelayConnection connection)
        {
            QueueFlowConnectionState(generation, connection, false);
        }

        private void OnGenerationFlowMessageReceived(SocketRelayGeneration generation, SocketRelayConnection connection, string message)
        {
            if (!IsCurrentConnection(generation, connection))
            {
                return;
            }

            log.Info($"收到Flow消息: {message}");
            AddMessage(generation, new RelayMessage
            {
                Time = DateTime.Now,
                Direction = RelayMessageDirection.FlowToRelay,
                EventName = TryGetEventName(message),
                Content = message
            });

            ForwardToClient(generation, connection, message);
        }

        private void OnGenerationListenerError(SocketRelayGeneration generation, Exception ex)
        {
            if (IsCurrentGeneration(generation))
            {
                log.Error($"中转服务器异常: {ex.Message}");
            }
        }

        private void OnGenerationFlowReadError(SocketRelayGeneration generation, SocketRelayConnection connection, Exception ex)
        {
            if (!IsCurrentConnection(generation, connection))
            {
                return;
            }

            log.Error($"读取Flow消息异常: {ex.Message}");
            AddMessage(generation, new RelayMessage
            {
                Time = DateTime.Now,
                Direction = RelayMessageDirection.FlowToRelay,
                EventName = "Error",
                Content = $"Flow连接断开: {ex.Message}"
            });
        }

        /// <summary>
        /// 将Flow的消息转发到外部Client (SocketControl.Current.Stream)
        /// </summary>
        private void ForwardToClient(
            SocketRelayGeneration generation,
            SocketRelayConnection connection,
            string message)
        {
            ForwardToClient(message, generation, connection);
        }

        private void ForwardToClient(
            string message,
            SocketRelayGeneration? generation = null,
            SocketRelayConnection? connection = null)
        {
            string forwardedMessage = message;
            SocketMessage? socketMessage = null;

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

                forwardedMessage = JsonConvert.SerializeObject(response);
                socketMessage = new SocketMessage
                {
                    Direction = SocketMessageDirection.Sent,
                    Content = forwardedMessage,
                    MessageTime = DateTime.Now,
                    EventName = response.EventName,
                    MsgID = response.MsgID,
                    ResponseCode = response.Code
                };
            }

            SocketRelayWriteResult writeResult;
            try
            {
                if (generation != null)
                {
                    if (connection == null || !TryRunForCurrentConnection(
                        generation,
                        connection,
                        () => _externalClientWriter(forwardedMessage),
                        out writeResult))
                    {
                        return;
                    }
                }
                else
                {
                    writeResult = _externalClientWriter(forwardedMessage);
                }
            }
            catch (Exception ex)
            {
                writeResult = new SocketRelayWriteResult(SocketRelayWriteStatus.Failed, ex);
            }

            if (writeResult.Status == SocketRelayWriteStatus.NoConnection)
            {
                log.Warn("外部Client未连接, 无法转发");
                PublishRelayMessage(generation, new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToClient,
                    EventName = "Error",
                    Content = "外部Client未连接, 无法转发"
                });
                return;
            }

            if (writeResult.Status == SocketRelayWriteStatus.Failed)
            {
                Exception error = writeResult.Error ?? new IOException("Unknown socket write failure.");
                log.Error($"转发到外部Client失败: {error.Message}");
                PublishRelayMessage(generation, new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToClient,
                    EventName = "Error",
                    Content = $"转发失败: {error.Message}"
                });
                return;
            }

            if (socketMessage != null)
            {
                if (generation == null)
                {
                    _socketMessagePublisher(socketMessage);
                }
                else if (connection != null)
                {
                    QueueSocketMessage(generation, connection, socketMessage);
                }
            }

            PublishRelayMessage(generation, new RelayMessage
            {
                Time = DateTime.Now,
                Direction = RelayMessageDirection.RelayToClient,
                EventName = TryGetEventName(message),
                Content = message
            });
            log.Info($"已转发给外部Client: {message}");
        }

        private static SocketRelayWriteResult WriteToExternalClient(string message)
        {
            NetworkStream? clientStream = SocketControl.Current.Stream;
            if (clientStream == null)
            {
                return new SocketRelayWriteResult(SocketRelayWriteStatus.NoConnection);
            }

            try
            {
                byte[] sendBytes = Encoding.UTF8.GetBytes(message);
                clientStream.Write(sendBytes, 0, sendBytes.Length);
                clientStream.Flush();
                return new SocketRelayWriteResult(SocketRelayWriteStatus.Sent);
            }
            catch (Exception ex)
            {
                return new SocketRelayWriteResult(SocketRelayWriteStatus.Failed, ex);
            }
        }

        private bool TryRunForCurrentConnection<T>(
            SocketRelayGeneration generation,
            SocketRelayConnection connection,
            Func<T> action,
            out T result)
        {
            lock (_generationSideEffectLock)
            {
                if (!IsCurrentConnection(generation, connection))
                {
                    result = default!;
                    return false;
                }

                result = action();
                return true;
            }
        }

        private void QueueSocketMessage(
            SocketRelayGeneration generation,
            SocketRelayConnection connection,
            SocketMessage message)
        {
            DispatchMessage(() =>
            {
                lock (_generationSideEffectLock)
                {
                    if (IsCurrentConnection(generation, connection))
                    {
                        _socketMessagePublisher(message);
                    }
                }
            });
        }

        private void PublishRelayMessage(SocketRelayGeneration? generation, RelayMessage message)
        {
            if (generation == null)
            {
                AddMessage(message);
            }
            else
            {
                AddMessage(generation, message);
            }
        }

        /// <summary>
        /// 将外部Client的响应转发给Flow (由ISocketJsonHandler调用)
        /// </summary>
        public void ForwardToFlow(string message)
        {
            SocketRelayGeneration? generation = Volatile.Read(ref _currentGeneration);
            SocketRelayWriteResult writeResult = generation?.WriteToCurrent(message)
                ?? new SocketRelayWriteResult(SocketRelayWriteStatus.NoConnection);

            if (writeResult.Status == SocketRelayWriteStatus.NoConnection)
            {
                log.Warn("Flow未连接, 无法转发");
                PublishRelayMessage(generation, new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToFlow,
                    EventName = "Error",
                    Content = "Flow未连接, 无法转发"
                });
                return;
            }

            if (writeResult.Status == SocketRelayWriteStatus.Failed)
            {
                Exception error = writeResult.Error ?? new IOException("Unknown socket write failure.");
                log.Error($"转发到Flow失败: {error.Message}");
                PublishRelayMessage(generation, new RelayMessage
                {
                    Time = DateTime.Now,
                    Direction = RelayMessageDirection.RelayToFlow,
                    EventName = "Error",
                    Content = $"转发失败: {error.Message}"
                });
                return;
            }

            PublishRelayMessage(generation, new RelayMessage
            {
                Time = DateTime.Now,
                Direction = RelayMessageDirection.RelayToFlow,
                EventName = TryGetEventName(message),
                Content = message
            });
            log.Info($"已转发给Flow: {message}");
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
            SocketRelayStopResult stopResult = StopServerAndWait(DefaultStopTimeout);
            if (!stopResult.Completed)
            {
                log.Warn($"中转服务器线程未在限定时间内退出，剩余线程数: {stopResult.RemainingWorkerCount}");
            }
        }

        internal SocketRelayStopResult StopServerAndWait(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            Stopwatch stopDeadline = Stopwatch.StartNew();
            (SocketRelayGeneration? generation, long lifecycleVersion) = BeginLifecycleTransition();
            QueueStoppedState(lifecycleVersion);

            if (generation == null)
            {
                return new SocketRelayStopResult(true, 0);
            }

            SocketRelayStopResult stopResult = generation.StopAndWait(GetRemainingTimeout(timeout, stopDeadline));
            DisposeAfterStop(generation, stopResult);
            return stopResult;
        }

        private static TimeSpan GetRemainingTimeout(TimeSpan timeout, Stopwatch stopwatch)
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                return Timeout.InfiniteTimeSpan;
            }

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private static void DisposeAfterStop(SocketRelayGeneration generation, SocketRelayStopResult stopResult)
        {
            if (stopResult.Completed)
            {
                generation.Dispose();
                return;
            }

            new Thread(generation.Dispose)
            {
                IsBackground = true,
                Name = $"RelayGenerationCleanup-{generation.Id}"
            }.Start();
        }

        private bool IsCurrentGeneration(SocketRelayGeneration generation)
        {
            return ReferenceEquals(Volatile.Read(ref _currentGeneration), generation);
        }

        private bool IsCurrentConnection(SocketRelayGeneration generation, SocketRelayConnection connection)
        {
            return IsCurrentGeneration(generation) && generation.IsCurrentConnection(connection);
        }

        private void QueueGenerationStartingState(SocketRelayGeneration generation, long lifecycleVersion)
        {
            DispatchState(() =>
            {
                if (Volatile.Read(ref _lifecycleVersion) != lifecycleVersion || !IsCurrentGeneration(generation))
                {
                    return;
                }

                IsListening = false;
                if (Volatile.Read(ref _lifecycleVersion) != lifecycleVersion || !IsCurrentGeneration(generation))
                {
                    return;
                }

                IsFlowConnected = false;
            });
        }

        private void QueueStoppedState(long lifecycleVersion)
        {
            DispatchState(() =>
            {
                if (Volatile.Read(ref _lifecycleVersion) != lifecycleVersion || Volatile.Read(ref _currentGeneration) != null)
                {
                    return;
                }

                IsListening = false;
                if (Volatile.Read(ref _lifecycleVersion) != lifecycleVersion || Volatile.Read(ref _currentGeneration) != null)
                {
                    return;
                }

                IsFlowConnected = false;
            });
        }

        private void QueueListeningState(SocketRelayGeneration generation, bool isListening)
        {
            if (!IsCurrentGeneration(generation))
            {
                return;
            }

            DispatchState(() =>
            {
                if (IsCurrentGeneration(generation))
                {
                    IsListening = isListening;
                }
            });
        }

        private void QueueFlowConnectionState(SocketRelayGeneration generation, SocketRelayConnection connection, bool isConnected)
        {
            if (!IsCurrentGeneration(generation))
            {
                return;
            }

            DispatchState(() =>
            {
                if (!IsCurrentGeneration(generation))
                {
                    return;
                }

                if (isConnected)
                {
                    if (generation.IsCurrentConnection(connection))
                    {
                        IsFlowConnected = true;
                    }
                }
                else if (generation.CurrentConnectionId == null)
                {
                    IsFlowConnected = false;
                }
            });
        }

        private void DispatchState(Action action)
        {
            if (_stateDispatcher != null)
            {
                _stateDispatcher(action);
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action);
            }
        }

        private void DispatchMessage(Action action)
        {
            if (_stateDispatcher != null)
            {
                _stateDispatcher(action);
                return;
            }

            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(action);
        }

        private void OnServiceChanged(object? sender, EventArgs e)
        {
            SocketRelayGeneration? generation = Volatile.Read(ref _currentGeneration);
            if (generation?.IsListening == true)
            {
                TryRunGeneralSensorResetPatch(generation);
            }
        }

        private void TryRunGeneralSensorResetPatch(SocketRelayGeneration generation)
        {
            if (!IsCurrentGeneration(generation) || generation.IsSensorResetCompleted)
            {
                return;
            }

            lock (_sensorResetSingleFlightLock)
            {
                if (!IsCurrentGeneration(generation) || generation.IsSensorResetCompleted)
                {
                    return;
                }

                if (_sensorResetRunning)
                {
                    // Keep only the newest intent. If A is still running while B/C starts, the
                    // current generation is retried as soon as the single-flight slot is released.
                    _pendingSensorResetGeneration = generation;
                    return;
                }

                _sensorResetRunning = true;
                if (ReferenceEquals(_pendingSensorResetGeneration, generation))
                {
                    _pendingSensorResetGeneration = null;
                }
            }

            _ = RunGeneralSensorResetPatchAsync(generation);
        }

        private async Task RunGeneralSensorResetPatchAsync(SocketRelayGeneration generation)
        {
            SocketRelaySensorResetResult result;
            try
            {
                result = await _sensorResetOperation();
            }
            catch (Exception ex)
            {
                log.Error("Socket 打开后重置通用传感器异常", ex);
                result = new SocketRelaySensorResetResult(
                    true,
                    $"通用传感器自动重置异常：{ex.Message}\n请手动关闭后重新打开通用传感器。");
            }

            try
            {
                if (IsCurrentGeneration(generation))
                {
                    generation.CompleteSensorReset(result.Completed);
                    if (!string.IsNullOrWhiteSpace(result.WarningMessage))
                    {
                        QueueSensorResetPrompt(generation, result.WarningMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("发布通用传感器自动重置结果失败", ex);
            }

            SocketRelayGeneration? retryGeneration;
            lock (_sensorResetSingleFlightLock)
            {
                _sensorResetRunning = false;
                retryGeneration = _pendingSensorResetGeneration;
                _pendingSensorResetGeneration = null;
            }

            if (retryGeneration != null)
            {
                TryRunGeneralSensorResetPatch(retryGeneration);
            }
        }

        private void QueueSensorResetPrompt(SocketRelayGeneration generation, string message)
        {
            DispatchState(() =>
            {
                lock (_generationSideEffectLock)
                {
                    if (!IsCurrentGeneration(generation))
                    {
                        return;
                    }

                    try
                    {
                        _sensorResetPrompt(message);
                    }
                    catch (Exception ex)
                    {
                        log.Error("显示通用传感器自动重置提示失败", ex);
                    }
                }
            });
        }

        // TEMP PATCH: Socket 服务打开后，重置一次通用传感器。
        // 后续后台修好连接状态判断后，可以连同本方法和相关 helper 一起删除。
        private static async Task<SocketRelaySensorResetResult> ApplyGeneralSensorResetPatchAsync()
        {
            try
            {
                DeviceSensor? deviceSensor = await FindGeneralSensorAsync();
                if (deviceSensor == null)
                {
                    log.Info("Socket 打开后通用传感器尚未创建，暂不执行自动重置");
                    return new SocketRelaySensorResetResult(false);
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
                    return new SocketRelaySensorResetResult(true);
                }

                string failureMessage = BuildSensorResetFailureMessage(openRecord, openState);
                log.Warn($"Socket 打开后重置通用传感器失败: {deviceSensor.Name} ({deviceSensor.Code}), {failureMessage}");
                return new SocketRelaySensorResetResult(
                    true,
                    $"通用传感器自动重置失败：{failureMessage}\n请手动关闭后重新打开通用传感器。");
            }
            catch (Exception ex)
            {
                log.Error("Socket 打开后重置通用传感器异常", ex);
                return new SocketRelaySensorResetResult(
                    true,
                    $"通用传感器自动重置异常：{ex.Message}\n请手动关闭后重新打开通用传感器。");
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
            if (application == null)
            {
                return;
            }

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

        private void AddMessage(SocketRelayGeneration generation, RelayMessage msg)
        {
            if (!IsCurrentGeneration(generation))
            {
                return;
            }

            DispatchMessage(() =>
            {
                lock (_generationSideEffectLock)
                {
                    if (!IsCurrentGeneration(generation))
                    {
                        return;
                    }

                    Messages.Add(msg);
                    if (IsCurrentGeneration(generation))
                    {
                        MessageReceived?.Invoke(msg);
                    }
                }
            });
        }

        private void AddMessage(RelayMessage msg)
        {
            DispatchMessage(() =>
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
