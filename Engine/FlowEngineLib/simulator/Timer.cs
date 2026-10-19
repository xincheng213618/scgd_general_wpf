using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

[STNode("全局", CategoryOrder = 0)]
internal class Timer : CVCommonNode, ISTNodeHub
{
    private readonly STNodeHubPorts ports;
    private readonly object timerSync = new();
    private readonly Dictionary<(STNodeOption, FlowRuntimeResources), MyTimer> timers = new();
    private int time = 2000;

    [STNodeProperty("Timer.Time", "Time")]
    public int Time
    {
        get => time;
        set { time = value; OnPropertyChanged(); }
    }

    public Timer() : base("Timer", "Timer", "Timer")
    {
        // Keep the saved hub port count and indices, including the unused spare pair.
        ports = new STNodeHubPorts(this, true, "IN", "OUT", InputDataTransfer);
        TitleColor = Color.FromArgb(200, Color.DarkOrange);
    }

    private void InputDataTransfer(object sender, STNodeOptionEventArgs e)
    {
        int index = InputOptions.IndexOf((STNodeOption)sender);
        STNodeOption output = OutputOptions[index];
        if (e.Status != ConnectionStatus.Connected || e.TargetOption.Data is not CVStartCFC received)
        {
            lock (output) output.TransferData(e.Status == ConnectionStatus.Connected ? e.TargetOption.Data : null);
            return;
        }

        var action = new CVStartCFC(received);
        action.NormalizeStopStatus();
        var key = (output, action.RuntimeResources);
        MyTimer delay = null;
        lock (timerSync)
        {
            if (timers.Remove(key, out MyTimer previous)) previous.Dispose();
            if (action.IsRunning && !action.RuntimeResources.IsDisposed && Time > 0)
            {
                delay = new MyTimer(Time, output, action);
                timers.Add(key, delay);
            }
        }
        if (delay != null) _ = RunDelayAsync(key, delay);
        else
        {
            lock (output)
                if (!action.IsRunning || !action.RuntimeResources.IsDisposed) output.TransferData(action);
        }
    }

    private async Task RunDelayAsync((STNodeOption, FlowRuntimeResources) key, MyTimer delay)
    {
        try
        {
            await delay.WaitAsync().ConfigureAwait(false);
            lock (timerSync)
            {
                if (!timers.TryGetValue(key, out MyTimer current) || !ReferenceEquals(current, delay)) return;
            }
            // Forward outside the pending-delay lock. A synchronous next round may replace this entry;
            // cleanup below removes it only if it still belongs to this delay.
            delay.TransferData();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!delay.Action.RuntimeResources.IsDisposed) FlowRepeatRegion.Fail(delay.Action, this, ex.Message);
        }
        finally
        {
            lock (timerSync)
                if (timers.TryGetValue(key, out MyTimer current) && ReferenceEquals(current, delay)) timers.Remove(key);
            delay.Dispose();
        }
    }

    protected override void OnSaveNode(Dictionary<string, byte[]> data)
    {
        base.OnSaveNode(data);
        data["count"] = BitConverter.GetBytes(InputOptionsCount);
    }

    public override void OnLoadNode(Dictionary<string, byte[]> data)
    {
        base.OnLoadNode(data);
        ports.RestoreCount(BitConverter.ToInt32(data["count"], 0));
    }
}
