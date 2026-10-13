using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using FlowEngineLib.MQTT;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Repeat;

/// <summary>A fixed, single-entry linear region. No graph edges are rewritten.</summary>
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
                member.ResetRepeatState();
            foreach (STNodeOption output in Ports(node, false))
                foreach (STNodeOption connected in output.ConnectedOption)
                    if (connected.Owner != null) pending.Enqueue(connected.Owner);
        }
    }

    public static bool TryCreate(IEnumerable<STNode> selection, out FlowRepeatRegion region, out string error)
    {
        region = null;
        error = Text("请选择连续连接的一段流程，重复框内不能包含分支、开始、结束或旧循环节点。");
        STNode[] selected = selection.Distinct().ToArray();
        if (selected.Length == 0 || selected.Any(node => node is not CVCommonNode || node is BaseStartNode || node is CVEndNode || node is LoopNode || node is LoopNextNode)) return false;
        var members = selected.Cast<CVCommonNode>().ToHashSet();
        foreach (CVCommonNode node in members)
        {
            STNodeOption[] inputs = Ports(node, true), outputs = Ports(node, false);
            if (inputs.Length != 1 || outputs.Length != 1 || inputs[0].DataType != typeof(CVStartCFC) || outputs[0].DataType != typeof(CVStartCFC) || inputs[0].ConnectionCount > 1) return false;
            if (node.GetAllInputOptions().Concat(node.GetAllOutputOptions()).Any(port => port != STNodeOption.Empty
                && port.DataType != typeof(CVStartCFC) && port.DataType != typeof(CVMQTTRequest) && port.DataType != typeof(MQActionEvent))) return false;
        }
        CVCommonNode[] entries = members.Where(node => !Ports(node, true)[0].ConnectedOption.Any(port => members.Contains(port.Owner as CVCommonNode))).ToArray();
        if (entries.Length != 1) return false;
        var ordered = new List<CVCommonNode>();
        CVCommonNode current = entries[0];
        while (current != null && !ordered.Contains(current))
        {
            ordered.Add(current);
            STNodeOption[] targets = Ports(current, false)[0].ConnectedOption.ToArray();
            CVCommonNode[] inside = targets.Select(port => port.Owner as CVCommonNode).Where(members.Contains).ToArray();
            if (inside.Length == 0) break;
            if (targets.Length != 1 || inside.Length != 1) return false;
            current = inside[0];
        }
        if (ordered.Count != members.Count || Ports(ordered[ordered.Count - 1], false)[0].ConnectedOption.Any(port => members.Contains(port.Owner as CVCommonNode))) return false;
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
        }
        error = string.Empty;
        return true;
    }

    private bool HasConsistentSettings() => Count > 0 && Nodes.All(node => node.RepeatCount == Count && node.RepeatMemberCount == Nodes.Length);

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
        if (string.IsNullOrEmpty(node.RepeatRegionId) || e.Status != ConnectionStatus.Connected || e.TargetOption.Data is not CVStartCFC action || !action.IsRunning) return true;
        if (action.RuntimeResources.IsDisposed) return false;
        if (action.RepeatRun?.Region.Id == node.RepeatRegionId
            && (e.TargetOption.Owner == null || e.TargetOption.Owner is CVCommonNode source && source.RepeatRegionId == node.RepeatRegionId)) return action.RepeatRun.Accepts(action);
        if (!TryCreate(ConnectedMembers(node), out FlowRepeatRegion region, out string error) || !region.HasConsistentSettings() || region.Input != input)
        {
            Fail(action, node, string.IsNullOrEmpty(error) ? Text("重复框的节点或次数已改变，请取消该框后重新框选。") : error);
            return false;
        }
        var run = new FlowRepeatRegionRun(region, action);
        action.RuntimeResources.Set("RepeatRegion:" + region.Id, run);
        action.RepeatRun = run;
        action.RepeatRound = 1;
        run.Start();
        return true;
    }

    internal static bool BeforeOutput(CVCommonNode node, STNodeOption output)
    {
        if (string.IsNullOrEmpty(node.RepeatRegionId) || output.Data is not CVStartCFC action) return true;
        action.NormalizeStopStatus();
        if (!action.IsRunning)
        {
            if (action.RepeatRun?.Region.Id == node.RepeatRegionId) action.RepeatRun.ObserveStop(action);
            return true; // Preserve existing error/stop propagation and cleanup nodes.
        }
        if (action.RuntimeResources.IsDisposed) return false;
        FlowRepeatRegionRun run = action.RepeatRun;
        if (run == null || run.Region.Id != node.RepeatRegionId) return false;
        return output != run.Region.Output ? run.Accepts(action) : run.CompleteRound(action);
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
    private readonly object sync = new();
    private readonly object displayOwner = new();
    private readonly CVStartCFC input;
    private int round = 1;
    private bool finished;
    private readonly HashSet<string> resources = new(StringComparer.Ordinal);
    public FlowRepeatRegion Region { get; }

    internal FlowRepeatRegionRun(FlowRepeatRegion region, CVStartCFC action)
    {
        Region = region;
        input = new CVStartCFC(action);
    }

    internal bool Accepts(CVStartCFC action)
    {
        lock (sync) return !finished && action.RepeatRound == round && !input.RuntimeResources.IsDisposed;
    }

    internal void Start()
    {
        lock (sync)
        {
            if (finished) return;
            foreach (CVCommonNode node in Region.Nodes) node.BeginRepeatState(displayOwner);
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
            if (!Accepts(action)) return false;
            if (round == Region.Count)
            {
                finished = true;
                ShowState(STNodeGroupState.Completed);
                return true;
            }
            foreach (string key in resources) input.RuntimeResources.Remove(key);
            resources.Clear();
            round++;
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
