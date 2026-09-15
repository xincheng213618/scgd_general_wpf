using System;

namespace FlowEngineLib.Base;

/// <summary>
/// Host-owned work for a service node. Execute prepares a result off-thread;
/// Complete transfers it only after the node has claimed command completion.
/// Dispose releases any result rejected by cancellation or timeout.
/// </summary>
public abstract class FlowLocalExecution : IDisposable
{
    public abstract void Execute();
    public abstract object Complete(CVStartCFC action);
    public abstract void Dispose();
}
