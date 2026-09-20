using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Core;

namespace CameraTest.Tests;

public sealed class JudgmentAndFocusTests
{
    private static FrameAnalysis Frame(double? mtf50 = 0.25, string[]? warnings = null, bool valid = true, SfrInputEncoding encoding = SfrInputEncoding.Linear)
    {
        var channel = new SfrChannelAnalysis
        {
            Channel = "L", Valid = valid, Reason = valid ? "ok" : "low_contrast_or_no_edge", Warnings = warnings ?? [], Mtf50 = mtf50,
            Frequencies = [0, 0.25, 0.5], Mtf = [1, 0.5, 0.1]
        };
        var edges = Enum.GetValues<BmwEdgeId>().Select(id => new BmwEdgeAnalysis(id, new(0, 0, 100, 80), valid, channel.Reason, new() { Channels = [channel] })).ToArray();
        return new(Guid.NewGuid(), "test", DateTimeOffset.Parse("2026-09-20T00:00:00Z"), 100, 100, 8, 1, 0.25,
            new() { InputEncoding = encoding }, 0, [new("A", new(0, 0, 100, 100), true, "", default, 50, 50, edges)]);
    }

    [Fact]
    public void RulesRequireExplicitThresholdsAndNeverTreatUnknownOrMissingValuesAsPass()
    {
        Assert.Equal(JudgmentState.Unconfigured, FrameJudgment.Evaluate(Frame(), new()).State);
        var rules = new JudgmentRules { MinimumMtf50 = 0.2 };
        Assert.Equal(JudgmentState.Pass, FrameJudgment.Evaluate(Frame(), rules).State);
        Assert.Equal(JudgmentState.Fail, FrameJudgment.Evaluate(Frame(0.1), rules).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(Frame(null), rules).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(Frame(valid: false), rules).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(Frame(encoding: SfrInputEncoding.Unknown), rules).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(Frame(warnings: ["clipped_pixels"]), rules).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(Frame(), rules with { CheckY = false, CheckR = true }).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(Frame(), new() { MaximumRedGreenShift = 1 }).State);
    }

    [Fact]
    public void UnlocatedTargetAndNullCrossingCannotPassConfiguredChecks()
    {
        var frame = Frame();
        var target = frame.Targets[0] with { Located = false, Reason = "target_not_found" };
        var judgment = FrameJudgment.Evaluate(frame with { Targets = [target] }, new() { MinimumResponse = 0.2 });
        Assert.Equal(JudgmentState.Indeterminate, judgment.State);
        Assert.Equal(4, judgment.Items.Count);
        Assert.All(judgment.Items, item => Assert.Equal("target_not_found", item.Reason));
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(frame with { Targets = [target with { Reason = "" }] }, new() { MinimumResponse = 0.2 }).State);
        var invalidEdges = target.Edges.Select(e => e with { Analysis = new() { Channels = [e.Analysis!.Channels[0] with { Valid = false, Reason = "" }] } }).ToArray();
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(frame with { Targets = [target with { Located = true, Edges = invalidEdges }] }, new() { MinimumMtf50 = 0.2 }).State);
        Assert.Throws<ArgumentException>(() => (new JudgmentRules { MinimumMtf50 = double.NaN }).Validate());
        Assert.Throws<ArgumentException>(() => (new JudgmentRules { MinimumMtf50 = 0.2, CheckY = false }).Validate());
    }

    [Fact]
    public void ColorLimitsUseAbsoluteNormalShiftAndMissingPairsCannotPass()
    {
        var frame = Frame();
        SfrChromaticAberrationResult color = new("1.0", "X", 1, 0, [], [new("R-G", true, "ok", [], -0.8, -0.8)]);
        frame = frame with { ColorShifts = frame.Targets[0].Edges.Select(e => new EdgeColorAnalysis("A", e.Id.ToString(), color)).ToArray() };
        Assert.Equal(JudgmentState.Pass, FrameJudgment.Evaluate(frame, new() { MaximumRedGreenShift = 1 }).State);
        Assert.Equal(JudgmentState.Fail, FrameJudgment.Evaluate(frame, new() { MaximumRedGreenShift = 0.5 }).State);
        Assert.Equal(JudgmentState.Indeterminate, FrameJudgment.Evaluate(frame, new() { MaximumRedGreenShift = 1, MaximumRedBlueShift = 1 }).State);
    }

    [Fact]
    public void FocusEvictsOldPeaksPreservesMissingFramesAndDoesNotDuplicateAFrame()
    {
        var history = new FocusHistory(3);
        var key = new FocusKey("A", "Left", "L");
        var first = Frame(0.45);
        history.Add(first);
        history.Add(first);
        Assert.Equal(1, history.Count);
        history.Add(Frame(0.2) with { CapturedAt = first.CapturedAt.AddSeconds(1) });
        history.AddMissing(Guid.NewGuid(), first.CapturedAt.AddSeconds(2), 0.25, "lost");
        var samples = history.Series(key, FocusMetric.Mtf50);
        Assert.Equal(3, samples.Count);
        Assert.Null(samples[^1].Value);
        history.Add(Frame(0.3) with { CapturedAt = first.CapturedAt.AddSeconds(3) });
        samples = history.Series(key, FocusMetric.Mtf50);
        Assert.Equal(0.3, samples.Where(s => s.Value.HasValue).Max(s => s.Value));
        Assert.Null(samples[1].Value);
        Assert.Equal(0, samples[0].Seconds);
        history.Clear();
        Assert.Empty(history.Series(key, FocusMetric.Mtf50));
    }
}
