using System;

namespace FlowEngineLib.Base;

using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Host-owned work for a service node. Execute prepares a result off-thread;
/// Complete transfers it only after the node has claimed command completion.
/// Dispose releases any result rejected by cancellation or timeout.
/// </summary>
public abstract class FlowLocalExecution : IDisposable
{
    private static readonly object RegistrySync = new object();
    private static readonly Dictionary<string, Backend> Backends = new();
    private static Backend[] backendSnapshot = Array.Empty<Backend>();

    private readonly record struct Backend(
        Func<CVBaseServerNode, bool> CanExecute,
        Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> Create);

    // The application supplies device backends without introducing an Engine dependency here.
    public static Func<CVBaseServerNode, bool> CanExecuteLocally { get; set; } = CanExecuteRegistered;
    public static Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> CreateForNode { get; set; } = CreateRegistered;
    public virtual bool UseNodeTimeout => true;
    public virtual void Bind(CVStartCFC action) { }

    public static void RegisterBackend(string key, Func<CVBaseServerNode, bool> canExecute, Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> create)
    {
        lock (RegistrySync)
        {
            int count = Backends.Count + (Backends.ContainsKey(key) ? 0 : 1);
            var snapshot = new Backend[count];
            Backends[key] = new Backend(canExecute, create);
            Backends.Values.CopyTo(snapshot, 0);
            Volatile.Write(ref backendSnapshot, snapshot);
        }
    }

    private static bool CanExecuteRegistered(CVBaseServerNode node)
    {
        foreach (Backend backend in Volatile.Read(ref backendSnapshot))
        {
            if (backend.CanExecute(node)) return true;
        }
        return false;
    }

    private static FlowLocalExecution CreateRegistered(CVBaseServerNode node, CVMQTTRequest request)
    {
        foreach (Backend backend in Volatile.Read(ref backendSnapshot))
        {
            FlowLocalExecution execution = backend.Create(node, request);
            if (execution != null) return execution;
        }
        return null;
    }
    public abstract void Execute();
    public abstract object Complete(CVStartCFC action);
    public abstract void Dispose();
}
