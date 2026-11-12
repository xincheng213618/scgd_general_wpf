using System;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Logical;

internal sealed class BranchJoinInputs
{
    private readonly object sync = new();

    internal CVStartCFC[] Collect(STNodeOptionCollection options, STNodeOption sender, CVStartCFC action)
    {
        lock (sync)
        {
            if (action.HasFinished || action.RuntimeResources.IsDisposed) return null;

            sender.Data = new CVStartCFC(action);
            int received = 0;
            foreach (STNodeOption option in options)
            {
                if (option.DataType != typeof(CVStartCFC) || option.Data is not CVStartCFC pending) continue;
                if (ReferenceEquals(action.RuntimeResources, pending.RuntimeResources) && action.IsSameExecutionIteration(pending))
                    received++;
                else
                    option.Data = null;
            }
            // STNodeInHub keeps one extra port for the next connection.
            if (received != options.Count - 1) return null;

            var ready = new CVStartCFC[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                STNodeOption option = options[i];
                if (option.DataType != typeof(CVStartCFC)) continue;
                ready[i] = (CVStartCFC)option.Data;
                option.Data = null;
            }
            return ready;
        }
    }

    internal void ClearRun(STNodeOptionCollection options, CVStartCFC action)
    {
        lock (sync)
        {
            foreach (STNodeOption option in options)
            {
                if (option.DataType == typeof(CVStartCFC) && option.Data is CVStartCFC pending
                    && ReferenceEquals(action.RuntimeResources, pending.RuntimeResources))
                    option.Data = null;
            }
        }
    }
}
