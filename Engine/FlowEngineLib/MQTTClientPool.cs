#pragma warning disable CA2016
using System;
using System.Collections.Generic;
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
    private static string _activeKey;

    private class PoolEntry
    {
        public string Key;
        public IMqttClient Client;
        public int RefCount;
    }

    private static string GetKey(string server, int port, string userName)
        => $"{server}:{port}:{userName}";

    public static void SetActiveEndpoint(string server, int port, string userName)
    {
        string activeKey = GetKey(server, port, userName);
        List<PoolEntry> retiredEntries = new List<PoolEntry>();
        lock (_lock)
        {
            _activeKey = activeKey;
            List<string> retiredKeys = new List<string>();
            foreach (var item in _pool)
            {
                if (!string.Equals(item.Key, activeKey, StringComparison.Ordinal) && item.Value.RefCount <= 0)
                {
                    retiredKeys.Add(item.Key);
                    retiredEntries.Add(item.Value);
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
    /// Try to acquire an existing, connected client from the pool.
    /// Returns null when no reusable connection exists.
    /// </summary>
    public static IMqttClient Acquire(string server, int port, string userName)
    {
        string key = GetKey(server, port, userName);
        lock (_lock)
        {
            if (_pool.TryGetValue(key, out var entry))
            {
                if (entry.Client != null && entry.Client.IsConnected)
                {
                    entry.RefCount++;
                    logger.DebugFormat("MQTTClientPool: reusing connection [{0}], refCount={1}", key, entry.RefCount);
                    return entry.Client;
                }

                // Stale entry – remove it
                _pool.Remove(key);
                try { entry.Client?.Dispose(); } catch { /* best-effort */ }
            }
        }
        return null;
    }

    /// <summary>
    /// Register a newly created, connected client in the pool with refCount=1.
    /// </summary>
    public static void Register(IMqttClient client, string server, int port, string userName)
    {
        string key = GetKey(server, port, userName);
        lock (_lock)
        {
            if (_pool.TryGetValue(key, out var old))
            {
                try { old.Client?.Dispose(); } catch { /* best-effort */ }
            }
            _activeKey ??= key;
            _pool[key] = new PoolEntry { Key = key, Client = client, RefCount = 1 };
            logger.DebugFormat("MQTTClientPool: registered new connection [{0}]", key);
        }
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
            logger.DebugFormat("MQTTClientPool: released connection [{0}], refCount={1}", entry.Key, entry.RefCount);

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

    private static async Task DisconnectAndDisposeAsync(PoolEntry entry)
    {
        if (entry.Client != null)
        {
            try
            {
                logger.DebugFormat("MQTTClientPool: disconnecting retired connection [{0}]", entry.Key);
                if (entry.Client.IsConnected)
                {
                    await entry.Client.DisconnectAsync(
                        new MqttClientDisconnectOptionsBuilder()
                            .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                            .Build());
                }
                entry.Client.Dispose();
                entry.Client = null;
            }
            catch (Exception ex)
            {
                logger.Warn("MQTTClientPool: error while disconnecting retired connection", ex);
            }
        }
    }
}
