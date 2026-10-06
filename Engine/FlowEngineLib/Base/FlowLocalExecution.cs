using System;

namespace FlowEngineLib.Base;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Host-owned work for a service node. Execute prepares a result off-thread;
/// Complete transfers it only after the node has claimed command completion.
/// Dispose releases any result rejected by cancellation or timeout.
/// </summary>
public abstract class FlowLocalExecution : IDisposable
{
    private static readonly object RegistrySync = new object();
    private static readonly Dictionary<string, (Func<CVBaseServerNode, bool> CanExecute, Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> Create)> Backends = new();
    // The application supplies device backends without introducing an Engine dependency here.
    public static Func<CVBaseServerNode, bool> CanExecuteLocally { get; set; } = CanExecuteRegistered;
    public static Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> CreateForNode { get; set; } = CreateRegistered;
    public virtual bool UseNodeTimeout => true;
    public virtual void Bind(CVStartCFC action) { }

    public static void RegisterBackend(string key, Func<CVBaseServerNode, bool> canExecute, Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> create)
    {
        lock (RegistrySync) Backends[key] = (canExecute, create);
    }

    private static bool CanExecuteRegistered(CVBaseServerNode node)
    {
        Func<CVBaseServerNode, bool>[] predicates;
        lock (RegistrySync) predicates = Backends.Values.Select(value => value.CanExecute).ToArray();
        return predicates.Any(predicate => predicate(node));
    }

    private static FlowLocalExecution CreateRegistered(CVBaseServerNode node, CVMQTTRequest request)
    {
        Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution>[] factories;
        lock (RegistrySync) factories = Backends.Values.Select(value => value.Create).ToArray();
        foreach (var factory in factories)
        {
            FlowLocalExecution execution = factory(node, request);
            if (execution != null) return execution;
        }
        return null;
    }
    public abstract void Execute();
    public abstract object Complete(CVStartCFC action);
    public abstract void Dispose();
}
