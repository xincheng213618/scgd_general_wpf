using System;

namespace FlowEngineLib.Base;

/// <summary>
/// Host-owned work for a service node. Execute prepares a result off-thread;
/// Complete transfers it only after the node has claimed command completion.
/// Dispose releases any result rejected by cancellation or timeout.
/// </summary>
public abstract class FlowLocalExecution : IDisposable
{
    // The application supplies device backends without introducing an Engine dependency here.
    public static Func<CVBaseServerNode, bool> CanExecuteLocally { get; set; }
    public static Func<CVBaseServerNode, CVMQTTRequest, FlowLocalExecution> CreateForNode { get; set; }
    public abstract void Execute();
    public abstract object Complete(CVStartCFC action);
    public abstract void Dispose();
}
