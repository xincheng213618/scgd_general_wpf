using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using FlowEngineLib.Repeat;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public partial class CVCommonNode : ISTNodeGroupMember
{
    [Browsable(false)] public string RepeatRegionId { get; private set; } = string.Empty;
    [Browsable(false)] public int RepeatCount { get; private set; } = 1;
    [Browsable(false)] public int RepeatMemberCount { get; private set; }
    private readonly object repeatStateSync = new();
    private object repeatStateOwner;
    private int repeatRound;
    private STNodeGroupState repeatState;

    string ISTNodeGroupMember.GroupId
    {
        get => RepeatRegionId;
        set => SetRepeatRegion(value, RepeatCount, RepeatMemberCount);
    }
    int ISTNodeGroupMember.GroupMemberCount => RepeatMemberCount;
    STNodeGroupState ISTNodeGroupMember.GroupState
    {
        get { lock (repeatStateSync) return repeatState; }
    }
    string ISTNodeGroupMember.GroupTitle
    {
        get
        {
            lock (repeatStateSync) return repeatState switch
            {
                STNodeGroupState.Running => string.Format(FlowRepeatRegion.Text("第 {0} / {1} 次"), repeatRound, RepeatCount),
                STNodeGroupState.Completed => string.Format(FlowRepeatRegion.Text("已完成 {0} / {1} 次"), repeatRound, RepeatCount),
                STNodeGroupState.Stopped => string.Format(FlowRepeatRegion.Text("已停止 · 第 {0} / {1} 次"), repeatRound, RepeatCount),
                STNodeGroupState.Failed => string.Format(FlowRepeatRegion.Text("失败 · 第 {0} / {1} 次"), repeatRound, RepeatCount),
                _ => string.Format(FlowRepeatRegion.Text("重复 {0} 次"), RepeatCount)
            };
        }
    }

    public void SetRepeatRegion(string id, int count, int memberCount)
    {
        if (!string.IsNullOrEmpty(id) && (count < 1 || memberCount < 1)) throw new ArgumentOutOfRangeException(nameof(count));
        RepeatRegionId = id ?? string.Empty;
        RepeatCount = string.IsNullOrEmpty(id) ? 1 : count;
        RepeatMemberCount = string.IsNullOrEmpty(id) ? 0 : memberCount;
        if (string.IsNullOrEmpty(id)) SetRepeatParameterValues(null);
        ResetRepeatState();
        OnPropertyChanged(nameof(RepeatRegionId));
    }

    internal void ResetRepeatState()
    {
        lock (repeatStateSync)
        {
            repeatStateOwner = null;
            repeatRound = 0;
            repeatState = STNodeGroupState.Idle;
        }
        Owner?.Invalidate();
    }

    internal void BeginRepeatState(object owner)
    {
        lock (repeatStateSync)
        {
            repeatStateOwner = owner;
            repeatRound = 1;
            repeatState = STNodeGroupState.Running;
        }
        Owner?.Invalidate();
    }

    internal void ShowRepeatState(object owner, int round, STNodeGroupState state)
    {
        lock (repeatStateSync)
        {
            if (!ReferenceEquals(repeatStateOwner, owner)) return;
            repeatRound = round;
            repeatState = state;
        }
        Owner?.Invalidate();
    }

    protected override void OnSaveNode(Dictionary<string, byte[]> data)
    {
        base.OnSaveNode(data);
        if (string.IsNullOrEmpty(RepeatRegionId)) return;
        data[nameof(RepeatRegionId)] = Encoding.UTF8.GetBytes(RepeatRegionId);
        data[nameof(RepeatCount)] = BitConverter.GetBytes(RepeatCount);
        data[nameof(RepeatMemberCount)] = BitConverter.GetBytes(RepeatMemberCount);
        if (RepeatParameterValues.Count > 0)
            data[nameof(RepeatParameterValues)] = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(RepeatParameterValues));
    }

    public override void OnLoadNode(Dictionary<string, byte[]> data)
    {
        base.OnLoadNode(data);
        SetRepeatRegion(data.TryGetValue(nameof(RepeatRegionId), out byte[] id) ? Encoding.UTF8.GetString(id) : string.Empty,
            data.TryGetValue(nameof(RepeatCount), out byte[] count) ? BitConverter.ToInt32(count, 0) : 1,
            data.TryGetValue(nameof(RepeatMemberCount), out byte[] size) ? BitConverter.ToInt32(size, 0) : 0);
        SetRepeatParameterValues(!string.IsNullOrEmpty(RepeatRegionId) && data.TryGetValue(nameof(RepeatParameterValues), out byte[] parameters)
            ? JsonConvert.DeserializeObject<Dictionary<string, double[]>>(Encoding.UTF8.GetString(parameters))
            : null);
    }

    protected override bool OnInputDataTransfer(STNodeOption input, STNodeOptionEventArgs e) =>
        FlowRepeatRegion.BeforeInput(this, input, e);

    protected override bool OnOutputDataTransfer(STNodeOption output) => FlowRepeatRegion.BeforeOutput(this, output);
}
