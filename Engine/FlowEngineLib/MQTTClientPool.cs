#pragma warning disable CA2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using MQTTnet;

namespace FlowEngineLib;

/// <summary>
/// Shares the configured MQTT connection across flow nodes and keeps it alive
/// while the application continues using the same endpoint. Releasing the last
/// flow-node reference only makes the connection idle; changing the configured
/// endpoint retires the old connection after its final reference is released.
/// </summary>
internal static class MQTTClientPool
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(MQTTClientPool));
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, PoolEntry> _pool = new Dictionary<string, PoolEntry>();
    private static readonly byte[] CredentialKeySalt = RandomNumberGenerator.GetBytes(32);
    private static readonly TimeSpan SlowSubscriptionThreshold = TimeSpan.FromMilliseconds(100);
    private static string _activeKey;

    private class PoolEntry
    {
        public string Key;
        public string EndpointLabel;
        public IMqttClient Client;
        public int RefCount;
        public bool Retired;
        public bool Disposing;
        public Task<bool> ReconnectTask;
        public Task<bool> RestoreSubscriptionsTask;
        public readonly CancellationTokenSource LifetimeCts = new CancellationTokenSource();
        public readonly SemaphoreSlim ConnectionGate = new SemaphoreSlim(1, 1);
        public readonly SemaphoreSlim SubscriptionGate = new SemaphoreSlim(1, 1);
        public readonly Dictionary<string, TopicRegistration> Topics = new Dictionary<string, TopicRegistration>(StringComparer.Ordinal);
        public Func<MqttClientConnectedEventArgs, Task> ConnectedHandler;
        public Func<MqttClientDisconnectedEventArgs, Task> DisconnectedHandler;
    }

    private class TopicRegistration
    {
        public readonly HashSet<Guid> Owners = new HashSet<Guid>();
        public bool IsSubscribed;
    }

    private static string GetKey(string server, int port, string userName, string password)
        => $"{server}:{port}:{userName}:{GetCredentialToken(password)}";

    private static string GetEndpointLabel(string server, int port, string userName)
        => $"{server}:{port}:{userName}";

    private static string GetCredentialToken(string password)
    {
        byte[] credentialBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
        try
        {
            using var hmac = new HMACSHA256(CredentialKeySalt);
            byte[] fingerprint = hmac.ComputeHash(credentialBytes);
            try
            {
                // This process-local HMAC is only an in-memory identity token.
                // It is deliberately never written to logs.
                return Convert.ToHexString(fingerprint);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(fingerprint);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialBytes);
        }
    }

    internal static Func<int, TimeSpan> ReconnectDelayProvider { get; set; } = DefaultReconnectDelay;

    private static TimeSpan DefaultReconnectDelay(int attempt)
    {
        int seconds = attempt switch
        {
            <= 1 => 2,
            2 => 5,
            3 => 10,
            4 => 20,
            _ => 30
        };
        return TimeSpan.FromSeconds(seconds);
    }

    public static void SetActiveEndpoint(string server, int port, string userName, string password = null)
    {
        string activeKey = GetKey(server, port, userName, password);
        List<PoolEntry> retiredEntries = new List<PoolEntry>();
        lock (_lock)
        {
            _activeKey = activeKey;
            List<string> retiredKeys = new List<string>();
            foreach (var item in _pool)
            {
                if (string.Equals(item.Key, activeKey, StringComparison.Ordinal))
                {
                    item.Value.Retired = false;
                }
                else
                {
                    item.Value.Retired = true;
                    if (item.Value.RefCount <= 0)
                    {
                        retiredKeys.Add(item.Key);
                        retiredEntries.Add(item.Value);
                    }
                }
            }
            foreach (string retiredKey in retiredKeys)
            {
                _pool.Remove(retiredKey);
            }
        }
        foreach (PoolEntry retiredEntry in retiredEntries)
        {
            _ = DisconnectAndDisposeAsync(retiredEntry);
        }
    }

    /// <summary>
    /// Try to acquire an existing client from the pool. A temporarily
    /// disconnected client is still returned so all helpers retain the same
    /// physical connection while its single reconnect loop is running.
    /// </summary>
    public static IMqttClient Acquire(string server, int port, string userName, string password = null)
    {
        string key = GetKey(server, port, userName, password);
        lock (_lock)
        {
            if (_pool.TryGetValue(key, out var entry))
            {
                if (entry.Client != null && !entry.Disposing)
                {
                    entry.RefCount++;
                    logger.DebugFormat(
                        "MQTTClientPool: reusing connection [{0}], connected={1}, refCount={2}",
                        entry.EndpointLabel,
                        entry.Client.IsConnected,
                        entry.RefCount);
                    return entry.Client;
                }

                _pool.Remove(key);
            }
        }
        return null;
    }

    /// <summary>
    /// Register a newly created, connected client in the pool with refCount=1.
    /// Returns false instead of replacing a connection that is still leased.
    /// </summary>
    public static bool Register(IMqttClient client, string server, int port, string userName, string password = null)
    {
        if (client == null)
        {
            return false;
        }

        string key = GetKey(server, port, userName, password);
        string endpointLabel = GetEndpointLabel(server, port, userName);
        PoolEntry replacedEntry = null;
        lock (_lock)
        {
            if (_pool.TryGetValue(key, out var old))
            {
                if (ReferenceEquals(old.Client, client))
                {
                    return true;
                }

                if (old.Client != null && !old.Disposing && (old.RefCount > 0 || !old.Retired))
                {
                    logger.WarnFormat(
                        "MQTTClientPool: refused to replace an active connection [{0}], refCount={1}",
                        old.EndpointLabel,
                        old.RefCount);
                    return false;
                }

                _pool.Remove(key);
                replacedEntry = old;
            }

            _activeKey ??= key;
            var entry = new PoolEntry
            {
                Key = key,
                EndpointLabel = endpointLabel,
                Client = client,
                RefCount = 1
            };
            AttachConnectionHandlers(entry);
            _pool[key] = entry;
            logger.DebugFormat("MQTTClientPool: registered new connection [{0}]", endpointLabel);
        }
        if (replacedEntry != null)
        {
            _ = DisconnectAndDisposeAsync(replacedEntry);
        }
        return true;
    }

    /// <summary>
    /// Release one reference to the pooled client.
    /// The active configured connection remains alive at refCount 0 so a later
    /// flow can reuse it. Connections for retired endpoints are disconnected.
    /// </summary>
    public static void Release(IMqttClient client)
    {
        if (client == null) return;

        PoolEntry retiredEntry = null;
        lock (_lock)
        {
            PoolEntry entry = null;
            foreach (var kv in _pool)
            {
                if (ReferenceEquals(kv.Value.Client, client))
                {
                    entry = kv.Value;
                    break;
                }
            }

            if (entry == null) return;

            if (entry.RefCount > 0)
            {
                entry.RefCount--;
            }
            logger.DebugFormat("MQTTClientPool: released connection [{0}], refCount={1}", entry.EndpointLabel, entry.RefCount);

            if (entry.RefCount <= 0 && !string.Equals(entry.Key, _activeKey, StringComparison.Ordinal))
            {
                _pool.Remove(entry.Key);
                retiredEntry = entry;
            }
        }
        if (retiredEntry != null)
        {
            _ = DisconnectAndDisposeAsync(retiredEntry);
        }
    }

    public static bool IsRegistered(IMqttClient client)
    {
        if (client == null)
        {
            return false;
        }

        lock (_lock)
        {
            return FindEntryLocked(client) != null;
        }
    }

    public static async Task<bool> TrySubscribeAsync(
        IMqttClient client,
        Guid ownerId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        if (client == null || string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        topic = topic.Trim();
        PoolEntry entry;
        bool ownerAdded;
        lock (_lock)
        {
            entry = FindEntryLocked(client);
            if (entry == null || entry.Disposing)
            {
                return false;
            }

            if (!entry.Topics.TryGetValue(topic, out TopicRegistration registration))
            {
                registration = new TopicRegistration();
                entry.Topics.Add(topic, registration);
            }
            ownerAdded = registration.Owners.Add(ownerId);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            entry.LifetimeCts.Token);
        var operationStopwatch = System.Diagnostics.Stopwatch.StartNew();
        long gateWaitMilliseconds = 0;
        bool gateAcquired = false;
        try
        {
            await entry.SubscriptionGate.WaitAsync(linkedCts.Token);
            gateAcquired = true;
            gateWaitMilliseconds = operationStopwatch.ElapsedMilliseconds;
            lock (_lock)
            {
                if (entry.Disposing ||
                    !entry.Topics.TryGetValue(topic, out TopicRegistration registration) ||
                    !registration.Owners.Contains(ownerId))
                {
                    return false;
                }
                if (registration.IsSubscribed && client.IsConnected)
                {
                    return true;
                }
            }

            if (!client.IsConnected)
            {
                return false;
            }

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(filter => filter.WithTopic(topic))
                .Build();
            await client.SubscribeAsync(subscribeOptions, linkedCts.Token);

            bool stillRequired;
            lock (_lock)
            {
                entry.Topics.TryGetValue(topic, out TopicRegistration registration);
                stillRequired = !entry.Disposing &&
                                registration != null &&
                                registration.Owners.Count > 0;
                if (stillRequired)
                {
                    registration.IsSubscribed = true;
                }
            }

            if (!stillRequired && client.IsConnected)
            {
                // Once the broker subscribe has completed, compensating
                // cleanup must outlive cancellation of this caller. Pool
                // disposal still cancels it through the lifetime token.
                await client.UnsubscribeAsync(topic, entry.LifetimeCts.Token);
            }
            return stillRequired;
        }
        catch (OperationCanceledException)
        {
            if (ownerAdded)
            {
                lock (_lock)
                {
                    if (entry.Topics.TryGetValue(topic, out TopicRegistration registration))
                    {
                        registration.Owners.Remove(ownerId);
                        if (registration.Owners.Count == 0 && !registration.IsSubscribed)
                        {
                            entry.Topics.Remove(topic);
                        }
                    }
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            logger.Warn($"MQTTClientPool: failed to subscribe '{topic}'", ex);
            lock (_lock)
            {
                if (entry.Topics.TryGetValue(topic, out TopicRegistration registration))
                {
                    registration.IsSubscribed = false;
                }
            }
            return false;
        }
        finally
        {
            if (gateAcquired)
            {
                entry.SubscriptionGate.Release();
            }
            operationStopwatch.Stop();
            if (operationStopwatch.Elapsed >= SlowSubscriptionThreshold)
            {
                logger.InfoFormat(
                    "MQTT subscription slow => operation=subscribe, topic={0}, gateWait={1}ms, total={2}ms",
                    topic,
                    gateWaitMilliseconds,
                    operationStopwatch.ElapsedMilliseconds);
            }
        }
    }

    public static async Task<bool> TryUnsubscribeAsync(
        IMqttClient client,
        Guid ownerId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        if (client == null || string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        topic = topic.Trim();
        PoolEntry entry;
        bool ownerRemoved;
        lock (_lock)
        {
            entry = FindEntryLocked(client);
            if (entry == null || entry.Disposing)
            {
                return false;
            }

            if (!entry.Topics.TryGetValue(topic, out TopicRegistration registration))
            {
                return true;
            }
            ownerRemoved = registration.Owners.Remove(ownerId);
            if (registration.Owners.Count > 0)
            {
                return true;
            }
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            entry.LifetimeCts.Token);
        var operationStopwatch = System.Diagnostics.Stopwatch.StartNew();
        long gateWaitMilliseconds = 0;
        bool gateAcquired = false;
        try
        {
            await entry.SubscriptionGate.WaitAsync(linkedCts.Token);
            gateAcquired = true;
            gateWaitMilliseconds = operationStopwatch.ElapsedMilliseconds;
            bool shouldUnsubscribe;
            lock (_lock)
            {
                if (entry.Disposing ||
                    !entry.Topics.TryGetValue(topic, out TopicRegistration registration))
                {
                    return true;
                }
                if (registration.Owners.Count > 0)
                {
                    return true;
                }
                shouldUnsubscribe = registration.IsSubscribed && client.IsConnected;
            }

            bool success = true;
            if (shouldUnsubscribe)
            {
                try
                {
                    await client.UnsubscribeAsync(topic, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    success = false;
                    logger.Warn($"MQTTClientPool: failed to unsubscribe '{topic}'", ex);
                }
            }

            lock (_lock)
            {
                if (entry.Topics.TryGetValue(topic, out TopicRegistration registration) &&
                    registration.Owners.Count == 0)
                {
                    entry.Topics.Remove(topic);
                }
            }
            return success;
        }
        catch (OperationCanceledException)
        {
            // Cancellation must not leave a broker subscription with no
            // logical owner. Restore this owner's registration so a later
            // unsubscribe or pool disposal can clean it up deterministically.
            if (ownerRemoved)
            {
                lock (_lock)
                {
                    if (!entry.Disposing &&
                        entry.Topics.TryGetValue(topic, out TopicRegistration registration))
                    {
                        registration.Owners.Add(ownerId);
                    }
                }
            }
            throw;
        }
        finally
        {
            if (gateAcquired)
            {
                entry.SubscriptionGate.Release();
            }
            operationStopwatch.Stop();
            if (operationStopwatch.Elapsed >= SlowSubscriptionThreshold)
            {
                logger.InfoFormat(
                    "MQTT subscription slow => operation=unsubscribe, topic={0}, gateWait={1}ms, total={2}ms",
                    topic,
                    gateWaitMilliseconds,
                    operationStopwatch.ElapsedMilliseconds);
            }
        }
    }

    public static Task ReleaseOwnerTopicsAsync(
        IMqttClient client,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            PoolEntry entry = FindEntryLocked(client);
            if (entry == null)
            {
                return Task.CompletedTask;
            }

            foreach (string topic in entry.Topics
                         .Where(item => item.Value.Owners.Contains(ownerId))
                         .Select(item => item.Key)
                         .ToArray())
            {
                TopicRegistration registration = entry.Topics[topic];
                registration.Owners.Remove(ownerId);
                // Keep one broker subscription per distinct topic on the active
                // connection so the next flow can take ownership without a
                // blocking unsubscribe/subscribe round trip. Explicit topic
                // changes and endpoint retirement still perform cleanup.
                if (registration.Owners.Count == 0 &&
                    (entry.Retired || !entry.Client.IsConnected))
                {
                    entry.Topics.Remove(topic);
                }
            }
        }
        return Task.CompletedTask;
    }

    public static void MarkDisconnected(IMqttClient client)
    {
        lock (_lock)
        {
            PoolEntry entry = FindEntryLocked(client);
            if (entry == null)
            {
                return;
            }
            foreach (TopicRegistration registration in entry.Topics.Values)
            {
                registration.IsSubscribed = false;
            }
        }
    }

    public static Task<bool> RestoreSubscriptionsAsync(
        IMqttClient client,
        CancellationToken cancellationToken = default)
    {
        PoolEntry entry;
        Task<bool> restoreTask;
        lock (_lock)
        {
            entry = FindEntryLocked(client);
            if (entry == null || entry.Disposing)
            {
                return Task.FromResult(false);
            }
            if (entry.RestoreSubscriptionsTask == null || entry.RestoreSubscriptionsTask.IsCompleted)
            {
                entry.RestoreSubscriptionsTask = RestoreSubscriptionsCoreAsync(entry);
            }
            restoreTask = entry.RestoreSubscriptionsTask;
        }
        return cancellationToken.CanBeCanceled
            ? restoreTask.WaitAsync(cancellationToken)
            : restoreTask;
    }

    public static Task<bool> EnsureReconnectAsync(
        IMqttClient client,
        CancellationToken cancellationToken = default)
    {
        PoolEntry entry;
        Task<bool> reconnectTask;
        lock (_lock)
        {
            entry = FindEntryLocked(client);
            if (entry == null || entry.Disposing)
            {
                return Task.FromResult(false);
            }
            if (client.IsConnected)
            {
                return RestoreSubscriptionsAsync(client, cancellationToken);
            }
            if (entry.ReconnectTask == null || entry.ReconnectTask.IsCompleted)
            {
                entry.ReconnectTask = ReconnectCoreAsync(entry);
            }
            reconnectTask = entry.ReconnectTask;
        }
        return cancellationToken.CanBeCanceled
            ? reconnectTask.WaitAsync(cancellationToken)
            : reconnectTask;
    }

    public static async Task<bool> TryReconnectNowAsync(
        IMqttClient client,
        CancellationToken cancellationToken = default)
    {
        PoolEntry entry;
        lock (_lock)
        {
            entry = FindEntryLocked(client);
            if (entry == null || entry.Disposing)
            {
                return false;
            }
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            entry.LifetimeCts.Token);
        await entry.ConnectionGate.WaitAsync(linkedCts.Token);
        try
        {
            if (entry.Disposing || entry.Client == null)
            {
                return false;
            }
            if (!entry.Client.IsConnected)
            {
                await entry.Client.ConnectAsync(entry.Client.Options, linkedCts.Token);
            }
            return entry.Client.IsConnected &&
                   await RestoreSubscriptionsAsync(entry.Client, linkedCts.Token);
        }
        finally
        {
            entry.ConnectionGate.Release();
        }
    }

    private static async Task<bool> RestoreSubscriptionsCoreAsync(PoolEntry entry)
    {
        var restoreStopwatch = System.Diagnostics.Stopwatch.StartNew();
        long gateWaitMilliseconds = 0;
        int topicCount = 0;
        try
        {
            await entry.SubscriptionGate.WaitAsync(entry.LifetimeCts.Token);
            gateWaitMilliseconds = restoreStopwatch.ElapsedMilliseconds;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        try
        {
            if (entry.Disposing || entry.Client == null || !entry.Client.IsConnected)
            {
                return false;
            }

            string[] topics;
            lock (_lock)
            {
                topics = entry.Topics
                    .Where(item => item.Value.Owners.Count > 0 && !item.Value.IsSubscribed)
                    .Select(item => item.Key)
                    .ToArray();
            }
            topicCount = topics.Length;

            bool success = true;
            foreach (string topic in topics)
            {
                try
                {
                    var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(filter => filter.WithTopic(topic))
                        .Build();
                    await entry.Client.SubscribeAsync(subscribeOptions, entry.LifetimeCts.Token);
                    bool stillRequired = false;
                    lock (_lock)
                    {
                        if (entry.Topics.TryGetValue(topic, out TopicRegistration registration))
                        {
                            registration.IsSubscribed = true;
                            stillRequired = registration.Owners.Count > 0;
                        }
                    }
                    if (!stillRequired && entry.Client.IsConnected)
                    {
                        await entry.Client.UnsubscribeAsync(topic, entry.LifetimeCts.Token);
                        lock (_lock)
                        {
                            if (entry.Topics.TryGetValue(topic, out TopicRegistration registration) &&
                                registration.Owners.Count == 0)
                            {
                                entry.Topics.Remove(topic);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (entry.LifetimeCts.IsCancellationRequested)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    success = false;
                    logger.Warn($"MQTTClientPool: failed to restore subscription '{topic}'", ex);
                }
            }
            return success;
        }
        finally
        {
            entry.SubscriptionGate.Release();
            restoreStopwatch.Stop();
            if (restoreStopwatch.Elapsed >= SlowSubscriptionThreshold)
            {
                logger.InfoFormat(
                    "MQTT subscription restore slow => topics={0}, gateWait={1}ms, total={2}ms",
                    topicCount,
                    gateWaitMilliseconds,
                    restoreStopwatch.ElapsedMilliseconds);
            }
        }
    }

    private static async Task<bool> ReconnectCoreAsync(PoolEntry entry)
    {
        try
        {
            int attempt = 0;
            while (ShouldReconnect(entry))
            {
                if (entry.Client != null && entry.Client.IsConnected)
                {
                    return await RestoreSubscriptionsCoreAsync(entry);
                }

                attempt++;
                TimeSpan delay = ReconnectDelayProvider(attempt);
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, entry.LifetimeCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                }

                if (!ShouldReconnect(entry))
                {
                    return false;
                }

                try
                {
                    await entry.ConnectionGate.WaitAsync(entry.LifetimeCts.Token);
                    try
                    {
                        if (!ShouldReconnect(entry))
                        {
                            return false;
                        }
                        if (entry.Client.IsConnected)
                        {
                            return await RestoreSubscriptionsCoreAsync(entry);
                        }

                        logger.WarnFormat(
                            "MQTTClientPool: reconnecting [{0}], attempt={1}",
                            entry.EndpointLabel,
                            attempt);
                        await entry.Client.ConnectAsync(
                            entry.Client.Options,
                            entry.LifetimeCts.Token);
                        if (entry.Client.IsConnected)
                        {
                            return await RestoreSubscriptionsCoreAsync(entry);
                        }
                    }
                    finally
                    {
                        entry.ConnectionGate.Release();
                    }
                }
                catch (OperationCanceledException) when (entry.LifetimeCts.IsCancellationRequested)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    logger.Warn(
                        $"MQTTClientPool: reconnect attempt {attempt} failed [{entry.EndpointLabel}]",
                        ex);
                }
            }
            return false;
        }
        finally
        {
            lock (_lock)
            {
                entry.ReconnectTask = null;
            }
        }
    }

    private static bool ShouldReconnect(PoolEntry entry)
    {
        lock (_lock)
        {
            return !entry.Disposing &&
                   entry.Client != null &&
                   (!entry.Retired || entry.RefCount > 0);
        }
    }

    private static PoolEntry FindEntryLocked(IMqttClient client)
    {
        foreach (PoolEntry entry in _pool.Values)
        {
            if (ReferenceEquals(entry.Client, client))
            {
                return entry;
            }
        }
        return null;
    }

    private static void AttachConnectionHandlers(PoolEntry entry)
    {
        entry.ConnectedHandler = _ => RestoreSubscriptionsAsync(entry.Client);
        entry.DisconnectedHandler = args =>
        {
            MarkDisconnected(entry.Client);
            _ = EnsureReconnectAsync(entry.Client);
            return Task.CompletedTask;
        };
        entry.Client.ConnectedAsync += entry.ConnectedHandler;
        entry.Client.DisconnectedAsync += entry.DisconnectedHandler;
    }

    private static void DetachConnectionHandlers(PoolEntry entry)
    {
        if (entry.Client == null)
        {
            return;
        }
        if (entry.ConnectedHandler != null)
        {
            entry.Client.ConnectedAsync -= entry.ConnectedHandler;
        }
        if (entry.DisconnectedHandler != null)
        {
            entry.Client.DisconnectedAsync -= entry.DisconnectedHandler;
        }
    }

    private static async Task DisconnectAndDisposeAsync(PoolEntry entry)
    {
        IMqttClient client = entry.Client;
        if (client != null)
        {
            lock (_lock)
            {
                entry.Disposing = true;
            }
            entry.LifetimeCts.Cancel();
            DetachConnectionHandlers(entry);
            await entry.ConnectionGate.WaitAsync();
            try
            {
                await entry.SubscriptionGate.WaitAsync();
                try
                {
                    logger.DebugFormat("MQTTClientPool: disconnecting retired connection [{0}]", entry.EndpointLabel);
                    if (client.IsConnected)
                    {
                        await client.DisconnectAsync(
                            new MqttClientDisconnectOptionsBuilder()
                                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                                .Build());
                    }
                }
                finally
                {
                    entry.SubscriptionGate.Release();
                }
            }
            catch (Exception ex)
            {
                logger.Warn("MQTTClientPool: error while disconnecting retired connection", ex);
            }
            finally
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Warn("MQTTClientPool: error while disposing retired connection", ex);
                }
                lock (_lock)
                {
                    entry.Client = null;
                }
                entry.ConnectionGate.Release();
            }
        }
    }
}
