using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using FlowEngineLib.MQTT;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Repeat;

/// <summary>A single-entry, single-exit acyclic region. No graph edges are rewritten.</summary>
public sealed class FlowRepeatRegion
{
    public CVCommonNode[] Nodes { get; }
    public STNodeOption Input { get; }
    public STNodeOption Output { get; }
    public int Count => Nodes[0].RepeatCount;
    public string Id => Nodes[0].RepeatRegionId;

    private FlowRepeatRegion(CVCommonNode[] nodes)
    {
        Nodes = nodes;
        Input = Ports(nodes[0], true)[0];
        Output = Ports(nodes[nodes.Length - 1], false)[0];
    }

    public static string Text(string key) => Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static STNodeOption[] Ports(STNode node, bool input) =>
        (input ? node.GetAllInputOptions() : node.GetAllOutputOptions()).Where(port => port.DataType == typeof(CVStartCFC)).ToArray();

    internal static void ResetStates(BaseStartNode start)
    {
        var pending = new Queue<STNode>();
        var visited = new HashSet<STNode>();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            STNode node = pending.Dequeue();
            if (!visited.Add(node)) continue;
            if (node is CVCommonNode member && !string.IsNullOrEmpty(member.RepeatRegionId))
            {
                member.ResetRepeatState();
                FlowRepeatRegionRun.ResetPortStates(member, null);
            }
            foreach (STNodeOption output in Ports(node, false))
                foreach (STNodeOption connected in output.ConnectedOption)
                    if (connected.Owner != null) pending.Enqueue(connected.Owner);
        }
    }

    public static bool TryCreate(IEnumerable<STNode> selection, out FlowRepeatRegion region, out string error)
    {
        region = null;
        error = Text("请选择单入口、单出口的完整流程区域，分流必须在框内汇合，不能包含环、开始、结束或旧循环节点。");
        STNode[] selected = selection.Distinct().ToArray();
        if (selected.Length == 0 || selected.Any(node => node is not CVCommonNode || node is BaseStartNode || node is CVEndNode || node is LoopNode || node is LoopNextNode)) return false;
        var members = selected.Cast<CVCommonNode>().ToHashSet();
        foreach (CVCommonNode node in members)
        {
            STNodeOption[] inputs = Ports(node, true), outputs = Ports(node, false);
            if (inputs.Length == 0 || outputs.Length != 1 || inputs.Any(input => input.ConnectionCount > 1)) return false;
            if (node.GetAllInputOptions().Concat(node.GetAllOutputOptions()).Any(port => port != STNodeOption.Empty
                && port.DataType != typeof(CVStartCFC) && port.DataType != typeof(CVMQTTRequest) && port.DataType != typeof(MQActionEvent))) return false;
        }
        var incoming = members.ToDictionary(node => node, node => Ports(node, true)
            .Sum(input => input.ConnectedOption.Count(port => members.Contains(port.Owner as CVCommonNode))));
        CVCommonNode[] entries = members.Where(node => incoming[node] == 0).ToArray();
        if (entries.Length != 1) return false;
        CVCommonNode entry = entries[0];
        if (Ports(entry, true).Length != 1) return false;
        foreach (CVCommonNode node in members.Where(node => node != entry))
            if (Ports(node, true).Any(input => input.ConnectionCount != 1
                || !members.Contains(input.ConnectedOption.Single().Owner as CVCommonNode))) return false;
        var targets = members.ToDictionary(node => node, node => Ports(node, false)[0].ConnectedOption.ToArray());
        CVCommonNode[] exits = members.Where(node => !targets[node].Any(port => members.Contains(port.Owner as CVCommonNode))).ToArray();
        if (exits.Length != 1) return false;
        foreach (CVCommonNode node in members.Where(node => node != exits[0]))
            if (targets[node].Any(port => !members.Contains(port.Owner as CVCommonNode))) return false;
        // Count input edges, including two distinct inputs supplied by the same predecessor.
        var pending = new Queue<CVCommonNode>();
        pending.Enqueue(entry);
        var ordered = new List<CVCommonNode>();
        while (pending.Count > 0)
        {
            CVCommonNode current = pending.Dequeue();
            ordered.Add(current);
            foreach (STNodeOption target in targets[current])
                if (target.Owner is CVCommonNode successor && members.Contains(successor) && --incoming[successor] == 0)
                    pending.Enqueue(successor);
        }
        if (ordered.Count != members.Count || ordered[ordered.Count - 1] != exits[0]) return false;
        region = new FlowRepeatRegion(ordered.ToArray());
        error = string.Empty;
        return true;
    }

    public static bool TryValidate(IEnumerable<STNode> nodes, out string error)
    {
        foreach (var group in nodes.OfType<CVCommonNode>().Where(node => !string.IsNullOrEmpty(node.RepeatRegionId)).GroupBy(node => node.RepeatRegionId))
        {
            if (!TryCreate(group, out FlowRepeatRegion region, out error)) return false;
            if (!region.HasConsistentSettings())
            {
                error = Text("重复框的节点或次数已改变，请取消该框后重新框选。");
                return false;
            }
            if (!region.TryCaptureParameters(out _, out error)) return false;
        }
        error = string.Empty;
        return true;
    }

    private bool HasConsistentSettings() => Count > 0 && Nodes.All(node => node.RepeatCount == Count && node.RepeatMemberCount == Nodes.Length);

    private bool TryCaptureParameters(out Dictionary<CVCommonNode, IReadOnlyDictionary<string, double[]>> parameters, out string error)
    {
        parameters = new Dictionary<CVCommonNode, IReadOnlyDictionary<string, double[]>>();
        foreach (CVCommonNode node in Nodes)
        {
            if (!node.TryCaptureRepeatParameters(Count, out IReadOnlyDictionary<string, double[]> values, out error)) return false;
            parameters.Add(node, values);
        }
        error = string.Empty;
        return true;
    }

    private static IEnumerable<STNode> ConnectedMembers(CVCommonNode start)
    {
        var pending = new Queue<CVCommonNode>();
        var visited = new HashSet<CVCommonNode>();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            CVCommonNode node = pending.Dequeue();
            if (!visited.Add(node)) continue;
            yield return node;
            foreach (STNodeOption port in node.GetAllInputOptions().Concat(node.GetAllOutputOptions()))
                foreach (STNodeOption connected in port.ConnectedOption)
                    if (connected.Owner is CVCommonNode member && member.RepeatRegionId == start.RepeatRegionId) pending.Enqueue(member);
        }
    }

    internal static bool BeforeInput(CVCommonNode node, STNodeOption input, STNodeOptionEventArgs e)
    {
        if (string.IsNullOrEmpty(node.RepeatRegionId) || e.Status != ConnectionStatus.Connected || e.TargetOption.Data is not CVStartCFC action) return true;
        if (action.RepeatRun?.Region.Id == node.RepeatRegionId && !action.RepeatRun.OwnsPort(input))
        {
            return false;
        }
        if (!action.IsRunning) return true;
        if (action.RuntimeResources.IsDisposed) return false;
        if (action.RepeatRun?.Region.Id == node.RepeatRegionId)
            return action.RepeatRun.AcceptInput(node, input, e.TargetOption, action);
        if (!TryCreate(ConnectedMembers(node), out FlowRepeatRegion region, out string error) || !region.HasConsistentSettings() || region.Input != input)
        {
            Fail(action, node, string.IsNullOrEmpty(error) ? Text("重复框的节点或次数已改变，请取消该框后重新框选。") : error);
            return false;
        }
        if (!region.TryCaptureParameters(out Dictionary<CVCommonNode, IReadOnlyDictionary<string, double[]>> parameters, out error))
        {
            Fail(action, node, error);
            return false;
        }
        var run = new FlowRepeatRegionRun(region, action, parameters);
        action.RuntimeResources.Set("RepeatRegion:" + region.Id, run);
        action.RepeatRun = run;
        action.RepeatRound = 1;
        run.Start();
        return run.AcceptInput(node, input, e.TargetOption, action);
    }

    internal static bool BeforeOutput(CVCommonNode node, STNodeOption output)
    {
        if (string.IsNullOrEmpty(node.RepeatRegionId) || output.Data is not CVStartCFC action) return true;
        FlowRepeatRegionRun run = action.RepeatRun;
        if (run?.Region.Id == node.RepeatRegionId && !run.OwnsPort(output))
        {
            FlowRepeatRegionRun.RestorePort(output);
            return false;
        }
        action.NormalizeStopStatus();
        if (!action.IsRunning)
        {
            if (action.RepeatRun?.Region.Id == node.RepeatRegionId) action.RepeatRun.ObserveStop(action);
            return true; // Preserve existing error/stop propagation and cleanup nodes.
        }
        if (run == null || run.Region.Id != node.RepeatRegionId) { FlowRepeatRegionRun.RestorePort(output); return false; }
        if (action.RuntimeResources.IsDisposed) { FlowRepeatRegionRun.RestorePort(output); return false; }
        return run.AcceptOutput(node, output, action);
    }

    internal static void Fail(CVStartCFC action, CVCommonNode node, string message)
    {
        action.Failed(message, node.NodeName, DateTime.Now, node.NodeID);
        if (action.TryDoFinishing()) action.FireFinished();
    }
}

internal sealed class FlowRepeatRegionRun : IDisposable
{
    [ThreadStatic] private static Queue<Action> pending;
    private sealed class PortDataState
    {
        public FlowRepeatRegionRun Owner;
        public CVStartCFC Data;
    }
    // A rejected old run must restore the current run's accepted value, rather than its own stale value.
    private static readonly ConditionalWeakTable<STNodeOption, PortDataState> portData = new();
    private readonly object sync = new();
    private readonly object displayOwner = new();
    private readonly CVStartCFC input;
    private readonly int count;
    private readonly Dictionary<CVCommonNode, IReadOnlyDictionary<string, double[]>> parameters;
    private readonly Dictionary<CVCommonNode, STNodeOption[]> inputs;
    private readonly HashSet<STNodeOption> arrivals = new();
    private readonly HashSet<STNodeOption> outputs = new();
    private readonly int requiredInputs;
    private int round = 1;
    private bool finished;
    private CVStartCFC batchOwner;
    private bool batchCompleted;
    private readonly HashSet<string> resources = new(StringComparer.Ordinal);
    public FlowRepeatRegion Region { get; }

    internal FlowRepeatRegionRun(FlowRepeatRegion region, CVStartCFC action, Dictionary<CVCommonNode, IReadOnlyDictionary<string, double[]>> parameters)
    {
        Region = region;
        input = new CVStartCFC(action);
        count = region.Count;
        this.parameters = parameters;
        inputs = region.Nodes.ToDictionary(node => node, node => node.GetAllInputOptions().Where(port => port.DataType == typeof(CVStartCFC)).ToArray());
        requiredInputs = inputs.Values.Sum(ports => ports.Length);
    }

    internal static void ResetPortStates(CVCommonNode node, FlowRepeatRegionRun owner, bool requireOwner = false)
    {
        foreach (STNodeOption port in node.GetAllInputOptions().Concat(node.GetAllOutputOptions()).Where(port => port.DataType == typeof(CVStartCFC)))
        {
            PortDataState state = portData.GetValue(port, _ => new PortDataState());
            lock (state)
            {
                if (requireOwner && !ReferenceEquals(state.Owner, owner)) continue;
                state.Owner = owner;
                state.Data = null;
                port.Data = null;
            }
        }
    }

    internal bool OwnsPort(STNodeOption port)
    {
        PortDataState state = portData.GetValue(port, _ => new PortDataState());
        lock (state) return ReferenceEquals(state.Owner, this);
    }

    internal static void RestorePort(STNodeOption port)
    {
        PortDataState state = portData.GetValue(port, _ => new PortDataState());
        lock (state) port.Data = state.Data == null ? null : new CVStartCFC(state.Data);
    }

    private bool RememberPort(STNodeOption port, CVStartCFC action)
    {
        PortDataState state = portData.GetValue(port, _ => new PortDataState());
        lock (state)
        {
            if (!ReferenceEquals(state.Owner, this)) { port.Data = state.Data == null ? null : new CVStartCFC(state.Data); return false; }
            state.Data = new CVStartCFC(action);
            port.Data = action;
            return true;
        }
    }

    internal bool AcceptInput(CVCommonNode node, STNodeOption port, STNodeOption source, CVStartCFC action)
    {
        lock (sync)
        {
            bool connectedSource = port == Region.Input
                ? source.Owner == null || port.ConnectedOption.Contains(source)
                : port.ConnectedOption.Contains(source) && outputs.Contains(source);
            if (!Accepts(action) || !OwnsPort(port) || !inputs.TryGetValue(node, out STNodeOption[] required)
                || !required.Contains(port) || !connectedSource || !arrivals.Add(port))
            {
                return false;
            }
            // Only the input handler may publish input.Data under its own join lock. Publishing here
            // would let a sibling handler consume this input before its own handler has even started.
            return true;
        }
    }

    internal bool AcceptOutput(CVCommonNode node, STNodeOption port, CVStartCFC action)
    {
        if (port == Region.Output) return CompleteRound(action);
        lock (sync)
        {
            if (!Accepts(action) || !OwnsPort(port) || !inputs.TryGetValue(node, out STNodeOption[] required)
                || required.Any(input => !arrivals.Contains(input)) || !outputs.Add(port))
            {
                RestorePort(port);
                return false;
            }
            return RememberPort(port, action);
        }
    }

    internal bool TryResolveParameter(CVCommonNode node, CVStartCFC action, string key, out double value)
    {
        value = default;
        if (!parameters.TryGetValue(node, out IReadOnlyDictionary<string, double[]> values) || !values.TryGetValue(key, out double[] sequence)) return false;
        if (action.RepeatRound < 1 || action.RepeatRound > count)
            throw new InvalidOperationException($"节点“{node.Title}”的重复轮次无效。 ");
        value = sequence[action.RepeatRound - 1];
        return true;
    }

    private bool CanBatch(CVCommonNode node, CVStartCFC action, bool isRunning) =>
        count > 1 && round == 1 && Region.Nodes.Length == 1 && ReferenceEquals(Region.Nodes[0], node)
        && ReferenceEquals(action.RepeatRun, this) && ReferenceEquals(action.RuntimeResources, input.RuntimeResources)
        && isRunning && OwnsPort(Region.Input) && Accepts(action);

    internal bool TryGetBatchParameters(CVCommonNode node, CVStartCFC action, out int batchCount, out IReadOnlyDictionary<string, double[]> values)
    {
        batchCount = 0;
        values = null;
        // Finishing holds the shared finish-state lock while disposing this run. Read that state before taking sync.
        bool isRunning = action.IsRunning && !action.IsDel && !action.TryGetStopStatus(out _) && action.FinishedStatus == null;
        lock (sync)
        {
            if (!CanBatch(node, action, isRunning) || batchCompleted || batchOwner != null && !ReferenceEquals(batchOwner, action)) return false;
            batchOwner = action;
            batchCount = count;
            values = new ReadOnlyDictionary<string, double[]>(parameters[node].ToDictionary(entry => entry.Key,
                entry => (double[])entry.Value.Clone(), StringComparer.Ordinal));
            return true;
        }
    }

    internal void CompleteBatch(CVCommonNode node, CVStartCFC action, int batchCount)
    {
        bool isRunning = action.IsRunning && !action.IsDel && !action.TryGetStopStatus(out _) && action.FinishedStatus == null;
        lock (sync)
        {
            if (!CanBatch(node, action, isRunning) || !ReferenceEquals(batchOwner, action) || batchCompleted || batchCount != count)
                throw new InvalidOperationException("批量执行的动作、次数或状态与当前重复框不一致，不能跳过剩余轮次。");
            batchCompleted = true;
        }
    }

    internal bool Accepts(CVStartCFC action)
    {
        lock (sync) return !finished && ReferenceEquals(action.RepeatRun, this) && action.RepeatRound == round && !input.RuntimeResources.IsDisposed;
    }

    internal void Start()
    {
        lock (sync)
        {
            if (finished) return;
            foreach (CVCommonNode node in Region.Nodes)
            {
                ResetPortStates(node, this);
                node.BeginRepeatState(displayOwner);
            }
        }
    }

    internal void ShowRound()
    {
        lock (sync)
        {
            if (!finished) ShowState(STNodeGroupState.Running);
        }
    }

    private void ShowState(STNodeGroupState state)
    {
        foreach (CVCommonNode node in Region.Nodes) node.ShowRepeatState(displayOwner, round, state);
    }

    internal void ObserveStop(CVStartCFC action)
    {
        if (!action.TryGetStopStatus(out StatusTypeEnum status)) return;
        lock (sync)
        {
            if (finished || action.RepeatRound != round) return;
            finished = true;
            ShowState(status == StatusTypeEnum.Canceled ? STNodeGroupState.Stopped : STNodeGroupState.Failed);
        }
    }

    internal void TrackResource(string key, int resourceRound)
    {
        lock (sync)
        {
            if (!finished && resourceRound == round) resources.Add(key);
        }
    }

    internal bool CompleteRound(CVStartCFC action)
    {
        CVStartCFC next;
        lock (sync)
        {
            if (!Accepts(action) || !OwnsPort(Region.Output) || arrivals.Count != requiredInputs || outputs.Count != Region.Nodes.Length - 1)
            {
                RestorePort(Region.Output);
                return false;
            }
            if (batchCompleted)
            {
                if (!ReferenceEquals(batchOwner, action)) { RestorePort(Region.Output); return false; }
                round = count;
                action.RepeatRound = count;
            }
            if (!RememberPort(Region.Output, action)) return false;
            outputs.Add(Region.Output);
            if (round == count)
            {
                finished = true;
                ShowState(STNodeGroupState.Completed);
                return true;
            }
            foreach (string key in resources) input.RuntimeResources.Remove(key);
            resources.Clear();
            round++;
            arrivals.Clear();
            outputs.Clear();
            foreach (CVCommonNode node in Region.Nodes) ResetPortStates(node, this, requireOwner: true);
            // Each iteration starts from the original upstream input; only the last output escapes.
            next = new CVStartCFC(input) { RepeatRun = this, RepeatRound = round, TTL = action.TTL };
        }
        ShowRound();
        Enqueue(() =>
        {
            if (!Accepts(next)) return;
            try { Region.Input.ReceiveData(next); }
            catch (Exception ex) { FlowRepeatRegion.Fail(next, Region.Nodes[0], ex.Message); }
        });
        return false;
    }

    private static void Enqueue(Action action)
    {
        // Synchronous nodes may finish inline. Drain iteratively instead of growing the call stack.
        if (pending != null) { pending.Enqueue(action); return; }
        pending = new Queue<Action>();
        try
        {
            pending.Enqueue(action);
            while (pending.Count > 0) pending.Dequeue()();
        }
        finally { pending = null; }
    }

    public void Dispose()
    {
        // Completion may happen on another action copy before its output is forwarded.
        // Read the shared terminal status before taking the run lock to avoid lock inversion.
        StatusTypeEnum? status = input.FinishedStatus;
        lock (sync)
        {
            if (finished) return;
            finished = true;
            ShowState(status is StatusTypeEnum.Failed or StatusTypeEnum.OverTime ? STNodeGroupState.Failed : STNodeGroupState.Stopped);
        }
    }
}
