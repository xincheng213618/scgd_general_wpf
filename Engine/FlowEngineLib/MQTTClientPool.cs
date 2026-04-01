using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using MQTTnet;

namespace FlowEngineLib;

/// <summary>
/// Pools MQTT client connections so that rapid disconnect/reconnect cycles
/// (e.g. switching flow templates) reuse the existing TCP connection instead
/// of tearing it down and re-establishing it every time.
/// 
/// When the last reference is released, the connection stays alive for a
/// grace period (default 5 s). If a new Acquire arrives within that window
/// the same client is handed back; otherwise it is disconnected and disposed.
/// </summary>
internal static class MQTTClientPool
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(MQTTClientPool));
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, PoolEntry> _pool = new Dictionary<string, PoolEntry>();
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

    private class PoolEntry
    {
        public string Key;
        public IMqttClient Client;
        public int RefCount;
        public CancellationTokenSource GraceCts;
    }

    private static string GetKey(string server, int port, string userName)
        => $"{server}:{port}:{userName}";

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
                    // Cancel any pending grace-period disposal
                    entry.GraceCts?.Cancel();
                    entry.GraceCts = null;
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
                old.GraceCts?.Cancel();
                try { old.Client?.Dispose(); } catch { /* best-effort */ }
            }
            _pool[key] = new PoolEntry { Key = key, Client = client, RefCount = 1 };
            logger.DebugFormat("MQTTClientPool: registered new connection [{0}]", key);
        }
    }

    /// <summary>
    /// Release one reference to the pooled client.
    /// When refCount reaches 0 the grace-period countdown starts.
    /// </summary>
    public static void Release(IMqttClient client)
    {
        if (client == null) return;

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

            entry.RefCount--;
            logger.DebugFormat("MQTTClientPool: released connection [{0}], refCount={1}", entry.Key, entry.RefCount);

            if (entry.RefCount <= 0)
            {
                var cts = new CancellationTokenSource();
                entry.GraceCts = cts;
                _ = GraceDisconnectAsync(entry, cts.Token);
            }
        }
    }

    private static async Task GraceDisconnectAsync(PoolEntry entry, CancellationToken token)
    {
        try
        {
            await Task.Delay(GracePeriod, token);
        }
        catch (TaskCanceledException)
        {
            return; // Re-acquired during grace period – keep alive
        }

        IMqttClient clientToDispose = null;
        lock (_lock)
        {
            if (entry.RefCount <= 0)
            {
                _pool.Remove(entry.Key);
                clientToDispose = entry.Client;
                entry.Client = null;
                logger.DebugFormat("MQTTClientPool: grace period expired, disconnecting [{0}]", entry.Key);
            }
        }

        if (clientToDispose != null)
        {
            try
            {
                if (clientToDispose.IsConnected)
                {
                    await clientToDispose.DisconnectAsync(
                        new MqttClientDisconnectOptionsBuilder()
                            .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                            .Build());
                }
                clientToDispose.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warn("MQTTClientPool: error during grace disconnect", ex);
            }
        }
    }
}
