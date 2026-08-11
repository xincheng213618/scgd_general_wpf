#pragma warning disable CA1001
using log4net;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.MQTT
{
    /// <summary>
    /// Owns the single live MQTT client binding, its configuration epoch, and connection serialization.
    /// MQTTControl remains responsible for application messages and topic state.
    /// </summary>
    internal sealed class MqttClientLifecycle
    {
        private readonly ILog _log;
        private readonly Func<IMqttClient> _clientFactory;
        private readonly Func<MqttClientBinding, MqttClientConnectedEventArgs, Task> _connectedAsync;
        private readonly Func<MqttClientBinding, MqttClientDisconnectedEventArgs, Task> _disconnectedAsync;
        private readonly Func<MqttClientBinding, MqttApplicationMessageReceivedEventArgs, Task> _messageReceivedAsync;
        private readonly Action _connectionStateChanged;
        private readonly object _locker = new();
        private readonly SemaphoreSlim _connectionGate = new(1, 1);

        private MqttClientBinding? _binding;
        private MqttConnectionConfig? _boundConnectionConfig;
        private IMqttClient? _client;
        private long _clientGeneration;
        private long _configurationEpoch;
        private bool _isConnected;
        private Task<bool> _currentConfigBindTask = Task.FromResult(true);

        public MqttClientLifecycle(
            ILog log,
            Func<IMqttClient> clientFactory,
            Func<MqttClientBinding, MqttClientConnectedEventArgs, Task> connectedAsync,
            Func<MqttClientBinding, MqttClientDisconnectedEventArgs, Task> disconnectedAsync,
            Func<MqttClientBinding, MqttApplicationMessageReceivedEventArgs, Task> messageReceivedAsync,
            Action connectionStateChanged)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _connectedAsync = connectedAsync ?? throw new ArgumentNullException(nameof(connectedAsync));
            _disconnectedAsync = disconnectedAsync ?? throw new ArgumentNullException(nameof(disconnectedAsync));
            _messageReceivedAsync = messageReceivedAsync ?? throw new ArgumentNullException(nameof(messageReceivedAsync));
            _connectionStateChanged = connectionStateChanged ?? throw new ArgumentNullException(nameof(connectionStateChanged));

            IMqttClient client = CreateClient();
            MqttClientBinding binding = CreateBinding(client, ++_clientGeneration);
            try
            {
                AttachEvents(binding);
            }
            catch (Exception ex)
            {
                Exception? cleanupException = RetireBinding(binding);
                throw new InvalidOperationException(
                    "Unable to initialize the MQTT client event binding.",
                    CombineExceptions(ex, cleanupException));
            }

            _client = client;
            _binding = binding;
        }

        public IMqttClient? Client
        {
            get
            {
                lock (_locker)
                {
                    return _client;
                }
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (_locker)
                {
                    return _isConnected;
                }
            }
        }

        public Task<bool> CurrentConfigBindTask
        {
            get
            {
                lock (_locker)
                {
                    return _currentConfigBindTask;
                }
            }
        }

        public Action? BeforeConnectTransition { get; set; }

        public void Bind(MqttConnectionConfig connectionConfig, Action installCurrentConfig)
        {
            ArgumentNullException.ThrowIfNull(connectionConfig);
            ArgumentNullException.ThrowIfNull(installCurrentConfig);

            long configurationEpoch;
            lock (_locker)
            {
                installCurrentConfig();
                configurationEpoch = ++_configurationEpoch;
            }

            MqttClientTransition? transition;
            try
            {
                transition = BeginTransition(connectionConfig, expectedConfigurationEpoch: configurationEpoch);
            }
            catch
            {
                lock (_locker)
                {
                    if (_binding == null)
                        _currentConfigBindTask = Task.FromResult(false);
                }
                throw;
            }

            if (transition == null)
            {
                throw new InvalidOperationException(
                    "The MQTT configuration bind was superseded before it could install its client.");
            }

            Task<bool> bindTask = transition.PreviousWasConnected
                ? ConnectClientAsync(transition.Binding, connectionConfig)
                : Task.FromResult(true);

            lock (_locker)
            {
                _currentConfigBindTask = bindTask;
            }

            if (transition.RetirementException != null)
            {
                throw new InvalidOperationException(
                    "The previous MQTT client could not be fully retired while applying the current configuration.",
                    transition.RetirementException);
            }
        }

        public Task<bool> Connect(Func<MqttConnectionConfig?> captureConnectionConfig)
        {
            ArgumentNullException.ThrowIfNull(captureConnectionConfig);

            MqttConnectionConfig? connectionConfig;
            long configurationEpoch;
            lock (_locker)
            {
                connectionConfig = captureConnectionConfig();
                if (connectionConfig == null)
                    return Task.FromResult(false);
                configurationEpoch = _configurationEpoch;
            }

            return Connect(connectionConfig, configurationEpoch);
        }

        private async Task<bool> Connect(
            MqttConnectionConfig connectionConfig,
            long expectedConfigurationEpoch)
        {
            _log.Info($"Connecting to MQTT: {connectionConfig}");

            try
            {
                BeforeConnectTransition?.Invoke();
                MqttClientTransition? transition = BeginTransition(
                    connectionConfig,
                    expectedConfigurationEpoch: expectedConfigurationEpoch);
                if (transition == null)
                    return false;

                if (transition.RetirementException != null)
                {
                    _log.Warn(
                        "The previous MQTT client reported an error while it was retired.",
                        transition.RetirementException);
                }

                return await ConnectClientAsync(transition.Binding, connectionConfig).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return false;
            }
        }

        public bool TryScheduleReconnect(
            MqttClientBinding binding,
            out CancellationToken cancellationToken)
        {
            lock (_locker)
            {
                if (!IsCurrentNoLock(binding) || binding.ReconnectScheduled)
                {
                    cancellationToken = default;
                    return false;
                }

                binding.ReconnectScheduled = true;
                cancellationToken = binding.LifetimeCancellation.Token;
                return true;
            }
        }

        public void Reconnect(MqttClientBinding binding)
        {
            MqttConnectionConfig? connectionConfig;
            long configurationEpoch;
            lock (_locker)
            {
                if (!IsCurrentNoLock(binding))
                    return;

                connectionConfig = _boundConnectionConfig;
                configurationEpoch = _configurationEpoch;
            }

            if (connectionConfig == null)
                return;

            try
            {
                MqttClientTransition? transition = BeginTransition(
                    connectionConfig,
                    binding,
                    configurationEpoch);
                if (transition == null)
                    return;

                if (transition.RetirementException != null)
                {
                    _log.Warn(
                        "The disconnected MQTT client reported an error while it was retired.",
                        transition.RetirementException);
                }
                _ = ConnectClientAsync(transition.Binding, connectionConfig);
            }
            catch (Exception ex)
            {
                _log.Error("Unable to create an MQTT client for reconnect.", ex);
            }
        }

        public MqttClientBinding? GetCurrentBinding()
        {
            lock (_locker)
            {
                return _binding;
            }
        }

        public bool IsCurrent(MqttClientBinding binding)
        {
            lock (_locker)
            {
                return IsCurrentNoLock(binding);
            }
        }

        public bool TrySetConnectionState(MqttClientBinding binding, bool value)
        {
            bool changed;
            lock (_locker)
            {
                if (!IsCurrentNoLock(binding))
                    return false;

                if (value)
                    binding.HasConnected = true;
                changed = _isConnected != value;
                _isConnected = value;
            }

            if (changed)
                RaiseConnectionStateChanged();
            return true;
        }

        public void ReplaceClient(IMqttClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            lock (_locker)
            {
                if (ReferenceEquals(_binding?.Client, client))
                    return;
            }

            MqttClientTransition transition = TransitionClient(
                () => client,
                connectionConfig: null,
                replaceBoundConnectionConfig: false,
                expectedBinding: null,
                expectedConfigurationEpoch: null,
                adoptReplacementConnectionState: true,
                invalidatePendingConnections: true)!;

            if (transition.RetirementException != null)
            {
                throw new InvalidOperationException(
                    "The previous MQTT client could not be fully retired after the public client replacement.",
                    transition.RetirementException);
            }
        }

        public IMqttClient CreateStandaloneClient() => CreateClient();

        public async Task DisconnectAsync()
        {
            MqttClientBinding? binding;
            bool connectionStateChanged;
            lock (_locker)
            {
                binding = _binding;
                _binding = null;
                _boundConnectionConfig = null;
                _client = null;
                _clientGeneration++;
                _configurationEpoch++;
                connectionStateChanged = _isConnected;
                _isConnected = false;
            }

            if (connectionStateChanged)
                RaiseConnectionStateChanged();
            if (binding == null)
                return;

            Exception? retirementException = DeactivateBinding(binding);
            bool gateEntered = false;
            try
            {
                await _connectionGate.WaitAsync().ConfigureAwait(false);
                gateEntered = true;
                if (binding.Client.IsConnected)
                    await binding.Client.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                retirementException = CombineExceptions(retirementException, ex);
            }
            finally
            {
                retirementException = RunRetirementStep(binding.Client.Dispose, retirementException);
                if (gateEntered)
                    _connectionGate.Release();
            }

            if (retirementException != null)
            {
                _log.Warn(
                    "The MQTT client reported an error while it was disconnected and retired.",
                    retirementException);
            }
        }

        private async Task<bool> ConnectClientAsync(
            MqttClientBinding binding,
            MqttConnectionConfig connectionConfig)
        {
            bool gateEntered = false;
            try
            {
                await _connectionGate.WaitAsync(binding.LifetimeCancellation.Token).ConfigureAwait(false);
                gateEntered = true;

                if (!IsCurrent(binding))
                    return false;

                MqttClientOptions options = BuildClientOptions(connectionConfig);
                await binding.Client.ConnectAsync(options, binding.LifetimeCancellation.Token).ConfigureAwait(false);

                if (!IsCurrent(binding))
                    return false;

                return TrySetConnectionState(binding, true);
            }
            catch (OperationCanceledException) when (binding.LifetimeCancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                _ = TrySetConnectionState(binding, false);
                return false;
            }
            finally
            {
                if (gateEntered)
                    _connectionGate.Release();
            }
        }

        private MqttClientTransition? BeginTransition(
            MqttConnectionConfig connectionConfig,
            MqttClientBinding? expectedBinding = null,
            long? expectedConfigurationEpoch = null) =>
            TransitionClient(
                CreateClient,
                connectionConfig,
                replaceBoundConnectionConfig: true,
                expectedBinding,
                expectedConfigurationEpoch,
                adoptReplacementConnectionState: false,
                invalidatePendingConnections: false);

        private MqttClientTransition? TransitionClient(
            Func<IMqttClient> clientProvider,
            MqttConnectionConfig? connectionConfig,
            bool replaceBoundConnectionConfig,
            MqttClientBinding? expectedBinding,
            long? expectedConfigurationEpoch,
            bool adoptReplacementConnectionState,
            bool invalidatePendingConnections)
        {
            MqttClientBinding? previousBinding = null;
            MqttClientBinding? nextBinding = null;
            IMqttClient? nextClient = null;
            bool previousWasConnected = false;
            bool connectionStateChanged = false;
            Exception? setupException = null;

            lock (_locker)
            {
                if (expectedBinding != null && !ReferenceEquals(_binding, expectedBinding))
                    return null;
                if (expectedConfigurationEpoch.HasValue
                    && expectedConfigurationEpoch.Value != _configurationEpoch)
                {
                    return null;
                }

                previousBinding = _binding;
                try
                {
                    nextClient = clientProvider()
                        ?? throw new InvalidOperationException("The MQTT client provider returned null.");
                    if (ReferenceEquals(previousBinding?.Client, nextClient))
                    {
                        throw new InvalidOperationException(
                            "The MQTT client factory returned the currently bound client as its replacement.");
                    }

                    if (invalidatePendingConnections)
                        _configurationEpoch++;

                    long nextGeneration = _clientGeneration + 1;
                    nextBinding = CreateBinding(nextClient, nextGeneration);
                    previousWasConnected = previousBinding?.HasConnected == true
                        || previousBinding?.Client.IsConnected == true
                        || _isConnected;

                    bool nextConnectionState = adoptReplacementConnectionState
                        && nextClient.IsConnected;
                    nextBinding.HasConnected = nextConnectionState;
                    AttachEvents(nextBinding);

                    _clientGeneration = nextGeneration;
                    _binding = nextBinding;
                    if (replaceBoundConnectionConfig)
                        _boundConnectionConfig = connectionConfig;
                    _client = nextClient;
                    connectionStateChanged = _isConnected != nextConnectionState;
                    _isConnected = nextConnectionState;
                }
                catch (Exception ex)
                {
                    setupException = ex;
                    _binding = null;
                    _boundConnectionConfig = null;
                    _client = null;
                    _clientGeneration++;
                    _configurationEpoch++;
                    connectionStateChanged = _isConnected;
                    _isConnected = false;
                }
            }

            if (setupException != null)
            {
                Exception? cleanupException = null;
                if (nextBinding != null)
                {
                    cleanupException = RetireBinding(nextBinding);
                }
                else if (nextClient != null
                    && !ReferenceEquals(previousBinding?.Client, nextClient))
                {
                    cleanupException = RunRetirementStep(nextClient.Dispose, null);
                }

                if (previousBinding != null)
                {
                    cleanupException = CombineExceptions(
                        cleanupException,
                        RetireBinding(previousBinding));
                }

                if (connectionStateChanged)
                    RaiseConnectionStateChanged();

                throw new InvalidOperationException(
                    "Unable to install the replacement MQTT client. The previous client was retired to prevent stale configuration use.",
                    CombineExceptions(setupException, cleanupException));
            }

            Exception? retirementException = previousBinding == null
                ? null
                : RetireBinding(previousBinding);
            if (connectionStateChanged)
                RaiseConnectionStateChanged();

            return new MqttClientTransition(nextBinding!, previousWasConnected, retirementException);
        }

        private static Exception? DeactivateBinding(MqttClientBinding binding)
        {
            Exception? retirementException = null;
            try
            {
                binding.LifetimeCancellation.Cancel();
            }
            catch (Exception ex)
            {
                retirementException = ex;
            }

            retirementException = RunRetirementStep(
                () => binding.Client.ConnectedAsync -= binding.ConnectedAsync,
                retirementException);
            retirementException = RunRetirementStep(
                () => binding.Client.DisconnectedAsync -= binding.DisconnectedAsync,
                retirementException);
            retirementException = RunRetirementStep(
                () => binding.Client.ApplicationMessageReceivedAsync -= binding.ApplicationMessageReceivedAsync,
                retirementException);
            return retirementException;
        }

        private static Exception? RetireBinding(MqttClientBinding binding)
        {
            Exception? retirementException = DeactivateBinding(binding);
            return RunRetirementStep(binding.Client.Dispose, retirementException);
        }

        private MqttClientBinding CreateBinding(IMqttClient client, long generation)
        {
            var binding = new MqttClientBinding(client, generation);
            binding.ConnectedAsync = arg => _connectedAsync(binding, arg);
            binding.DisconnectedAsync = arg => _disconnectedAsync(binding, arg);
            binding.ApplicationMessageReceivedAsync = arg => _messageReceivedAsync(binding, arg);
            return binding;
        }

        private static void AttachEvents(MqttClientBinding binding)
        {
            binding.Client.ConnectedAsync += binding.ConnectedAsync;
            binding.Client.DisconnectedAsync += binding.DisconnectedAsync;
            binding.Client.ApplicationMessageReceivedAsync += binding.ApplicationMessageReceivedAsync;
        }

        private bool IsCurrentNoLock(MqttClientBinding binding) =>
            ReferenceEquals(_binding, binding)
            && binding.Generation == _clientGeneration
            && !binding.LifetimeCancellation.IsCancellationRequested;

        private void RaiseConnectionStateChanged()
        {
            try
            {
                _connectionStateChanged();
            }
            catch (Exception ex)
            {
                _log.Error("The MQTT connection-state notification failed.", ex);
            }
        }

        private IMqttClient CreateClient() =>
            _clientFactory() ?? throw new InvalidOperationException("The MQTT client factory returned null.");

        internal static MqttClientOptions BuildClientOptions(MqttConnectionConfig connectionConfig)
        {
            string? host = string.IsNullOrWhiteSpace(connectionConfig.Host)
                ? null
                : connectionConfig.Host.Trim();

            return new MqttClientOptionsBuilder()
                .WithTcpServer(host, connectionConfig.Port)
                .WithCredentials(connectionConfig.UserName, connectionConfig.UserPwd)
                .WithClientId(Guid.NewGuid().ToString("N"))
                .Build();
        }

        private static Exception? RunRetirementStep(Action action, Exception? previousException)
        {
            try
            {
                action();
                return previousException;
            }
            catch (Exception ex)
            {
                return CombineExceptions(previousException, ex);
            }
        }

        private static Exception? CombineExceptions(Exception? first, Exception? second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            var exceptions = new List<Exception>();
            AddExceptions(exceptions, first);
            AddExceptions(exceptions, second);
            return new AggregateException(exceptions);
        }

        private static void AddExceptions(List<Exception> exceptions, Exception exception)
        {
            if (exception is AggregateException aggregateException)
                exceptions.AddRange(aggregateException.Flatten().InnerExceptions);
            else
                exceptions.Add(exception);
        }

        private sealed record MqttClientTransition(
            MqttClientBinding Binding,
            bool PreviousWasConnected,
            Exception? RetirementException);
    }

    internal sealed class MqttClientBinding
    {
        public MqttClientBinding(IMqttClient client, long generation)
        {
            Client = client;
            Generation = generation;
        }

        public IMqttClient Client { get; }

        public long Generation { get; }

        public CancellationTokenSource LifetimeCancellation { get; } = new();

        public Func<MqttClientConnectedEventArgs, Task> ConnectedAsync { get; set; } = null!;

        public Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync { get; set; } = null!;

        public Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync { get; set; } = null!;

        public bool ReconnectScheduled { get; set; }

        public bool HasConnected { get; set; }
    }

    internal sealed record MqttConnectionConfig(
        string Host,
        int Port,
        string UserName,
        string UserPwd)
    {
        public static MqttConnectionConfig Capture(MQTTConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            return new MqttConnectionConfig(
                config.Host,
                config.Port,
                config.UserName,
                config.UserPwd);
        }

        public override string ToString()
        {
            string userName = string.IsNullOrWhiteSpace(UserName) ? "<empty>" : "***";
            string userPwd = string.IsNullOrWhiteSpace(UserPwd) ? "<empty>" : "***";
            return $"Host={Host};Port={Port};UserName={userName};UserPwd={userPwd}";
        }
    }
}
