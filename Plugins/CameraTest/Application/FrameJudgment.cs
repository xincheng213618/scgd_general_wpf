using CameraTest.Models;
using ColorVision.Core;

namespace CameraTest.Application;

public enum JudgmentState { Unconfigured, Pass, Fail, Indeterminate }
public sealed record JudgmentItem(string Target, string Edge, string Channel, string Metric, double? Value, double Limit, string Unit,
    JudgmentState State, string Reason)
{
    public string Status => FrameJudgment.Label(State);
}
public sealed record FrameJudgment(JudgmentState State, IReadOnlyList<JudgmentItem> Items)
{
    public string Status => Label(State);
    public static string Label(JudgmentState state) => state switch
    {
        JudgmentState.Pass => "合格", JudgmentState.Fail => "不合格", JudgmentState.Indeterminate => "无法判定", _ => "未设置标准"
    };

    public static FrameJudgment Evaluate(FrameAnalysis frame, JudgmentRules rules)
    {
        rules.Validate();
        var items = new List<JudgmentItem>();
        foreach (var target in frame.Targets)
            foreach (var edge in target.Edges)
            {
                void Check(string channel, string metric, double? limit, double? value, bool valid, string reason, string[] warnings, bool maximum, string unit)
                {
                    if (!limit.HasValue) return;
                    string unavailable = !target.Located ? string.IsNullOrWhiteSpace(target.Reason) ? "target_not_found" : target.Reason
                        : !valid ? string.IsNullOrWhiteSpace(reason) ? "测量无效" : reason : frame.Options.InputEncoding == SfrInputEncoding.Unknown ? "输入编码未知，仅供诊断"
                        : warnings.Length != 0 ? string.Join("; ", warnings) : !value.HasValue || !double.IsFinite(value.Value) ? "指标不可测或未找到交点" : "";
                    var itemState = unavailable.Length != 0 ? JudgmentState.Indeterminate : (maximum ? Math.Abs(value!.Value) <= limit : value >= limit) ? JudgmentState.Pass : JudgmentState.Fail;
                    items.Add(new(target.Id, edge.Id.ToString(), channel, metric, value, limit.Value, unit, itemState,
                        unavailable.Length != 0 ? unavailable : itemState == JudgmentState.Pass ? "满足阈值" : maximum ? "位移绝对值超过上限" : "低于下限"));
                }
                foreach (string name in rules.SelectedChannels())
                {
                    var c = edge.Analysis?.Channels.FirstOrDefault(c => c.Channel == name);
                    string reason = c?.Reason ?? (edge.Analysis == null ? edge.Reason : "channel_missing");
                    Check(name, "MTF50", rules.MinimumMtf50, c?.Mtf50, c?.Valid == true, reason, c?.Warnings ?? [], false, "cycles/pixel");
                    Check(name, "MTF10", rules.MinimumMtf10, c?.Mtf10, c?.Valid == true, reason, c?.Warnings ?? [], false, "cycles/pixel");
                    double? response = c?.Valid == true ? SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, frame.Frequency) : null;
                    Check(name, $"MTF@{frame.Frequency:G}", rules.MinimumResponse, response, c?.Valid == true, reason, c?.Warnings ?? [], false, "ratio");
                }
                var ca = frame.ColorShifts.FirstOrDefault(c => c.Target == target.Id && c.Edge == edge.Id.ToString());
                foreach (var (pair, limit) in new[] { ("R-G", rules.MaximumRedGreenShift), ("R-B", rules.MaximumRedBlueShift), ("G-B", rules.MaximumGreenBlueShift) })
                {
                    var p = ca?.Analysis.Pairs.FirstOrDefault(p => p.Pair == pair);
                    Check(pair, "50% 边缘位移", limit, p?.NormalShiftPixels, p?.Valid == true, p?.Reason ?? "色差未测量", p?.Warnings ?? [], true, "pixel");
                }
            }
        JudgmentState state = items.Count == 0 ? JudgmentState.Unconfigured : items.Any(i => i.State == JudgmentState.Fail) ? JudgmentState.Fail
            : items.Any(i => i.State == JudgmentState.Indeterminate) ? JudgmentState.Indeterminate : JudgmentState.Pass;
        return new(state, items);
    }
}
