using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using FlowEngineLib.Repeat;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public partial class CVCommonNode : ISTNodeGroupMember
{
    [Browsable(false)] public string RepeatRegionId { get; private set; } = string.Empty;
    [Browsable(false)] public int RepeatCount { get; private set; } = 1;
    [Browsable(false)] public int RepeatMemberCount { get; private set; }
    private int repeatRound;

    string ISTNodeGroupMember.GroupId
    {
        get => RepeatRegionId;
        set => SetRepeatRegion(value, RepeatCount, RepeatMemberCount);
    }
    int ISTNodeGroupMember.GroupMemberCount => RepeatMemberCount;
    string ISTNodeGroupMember.GroupTitle => repeatRound > 0
        ? string.Format(FlowRepeatRegion.Text("第 {0} / {1} 次"), repeatRound, RepeatCount)
        : string.Format(FlowRepeatRegion.Text("重复 {0} 次"), RepeatCount);

    public void SetRepeatRegion(string id, int count, int memberCount)
    {
        if (!string.IsNullOrEmpty(id) && (count < 1 || memberCount < 1)) throw new ArgumentOutOfRangeException(nameof(count));
        RepeatRegionId = id ?? string.Empty;
        RepeatCount = string.IsNullOrEmpty(id) ? 1 : count;
        RepeatMemberCount = string.IsNullOrEmpty(id) ? 0 : memberCount;
        repeatRound = 0;
        OnPropertyChanged(nameof(RepeatRegionId));
        Owner?.Invalidate();
    }

    internal void ShowRepeatRound(int round)
    {
        repeatRound = round;
        Owner?.Invalidate();
    }

    protected override void OnSaveNode(Dictionary<string, byte[]> data)
    {
        base.OnSaveNode(data);
        if (string.IsNullOrEmpty(RepeatRegionId)) return;
        data[nameof(RepeatRegionId)] = Encoding.UTF8.GetBytes(RepeatRegionId);
        data[nameof(RepeatCount)] = BitConverter.GetBytes(RepeatCount);
        data[nameof(RepeatMemberCount)] = BitConverter.GetBytes(RepeatMemberCount);
    }

    public override void OnLoadNode(Dictionary<string, byte[]> data)
    {
        base.OnLoadNode(data);
        SetRepeatRegion(data.TryGetValue(nameof(RepeatRegionId), out byte[] id) ? Encoding.UTF8.GetString(id) : string.Empty,
            data.TryGetValue(nameof(RepeatCount), out byte[] count) ? BitConverter.ToInt32(count, 0) : 1,
            data.TryGetValue(nameof(RepeatMemberCount), out byte[] size) ? BitConverter.ToInt32(size, 0) : 0);
    }

    protected override bool OnInputDataTransfer(STNodeOption input, STNodeOptionEventArgs e) =>
        FlowRepeatRegion.BeforeInput(this, input, e);

    protected override bool OnOutputDataTransfer(STNodeOption output) => FlowRepeatRegion.BeforeOutput(this, output);
}
