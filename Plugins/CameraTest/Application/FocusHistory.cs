using System.Globalization;
using System.IO;
using System.Text;
using ColorVision.Core;

namespace CameraTest.Application;

public enum FocusMetric { FrequencyResponse, Mtf50 }
public sealed record FocusKey(string Target, string Edge, string Channel)
{
    public override string ToString() => $"{Target} / {Edge} / {(Channel == "L" ? "Y" : Channel)}";
}
public sealed record FocusValue(FocusKey Key, double? Mtf50, double? Response, string Status);
public sealed record FocusFrame(Guid FrameId, DateTimeOffset Time, double Frequency, IReadOnlyList<FocusValue> Values);
public sealed record FocusSample(Guid FrameId, DateTimeOffset Time, double Seconds, double? Value);

/// <summary>Bounded scalar history; never retains source pixels, ESF arrays or old analysis jobs.</summary>
public sealed class FocusHistory(int capacity = 300)
{
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Queue<FocusFrame> _frames = new();
    public int Count => _frames.Count;
    public void Clear() => _frames.Clear();
    public void AddMissing(Guid frameId, DateTimeOffset time, double frequency, string reason)
    {
        var values = _frames.LastOrDefault()?.Values.Select(v => v with { Mtf50 = null, Response = null, Status = reason }).ToArray() ?? [];
        _frames.Enqueue(new(frameId, time, frequency, values));
        while (_frames.Count > _capacity) _frames.Dequeue();
    }
    public void Add(FrameAnalysis frame)
    {
        if (_frames.Any(f => f.FrameId == frame.FrameId)) return;
        var values = frame.Targets.SelectMany(target => target.Edges.SelectMany(edge => new[] { "L", "R", "G", "B" }.Select(name =>
        {
            var c = edge.Analysis?.Channels.FirstOrDefault(c => c.Channel == name);
            return new FocusValue(new(target.Id, edge.Id.ToString(), name), c?.Valid == true ? c.Mtf50 : null,
                c?.Valid == true ? SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, frame.Frequency) : null,
                c == null ? edge.Analysis == null ? edge.Reason : "channel_missing" : c.Valid ? string.Join("; ", c.Warnings) : c.Reason);
        }))).ToArray();
        _frames.Enqueue(new(frame.FrameId, frame.CapturedAt, frame.Frequency, values));
        while (_frames.Count > _capacity) _frames.Dequeue();
    }
    public IReadOnlyList<FocusSample> Series(FocusKey key, FocusMetric metric)
    {
        if (_frames.Count == 0) return [];
        var first = _frames.Peek().Time;
        return _frames.Select(f =>
        {
            var value = f.Values.FirstOrDefault(v => v.Key == key);
            return new FocusSample(f.FrameId, f.Time, (f.Time - first).TotalSeconds, metric == FocusMetric.Mtf50 ? value?.Mtf50 : value?.Response);
        }).ToArray();
    }
    public void Export(string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("FrameId,Time,Target,Edge,Channel,MTF50_cyclesPerPixel,MTF_at_frequency,Frequency_cyclesPerPixel,Status");
        static string Q(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        static string N(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? "";
        foreach (var f in _frames)
            foreach (var v in f.Values)
                writer.WriteLine(string.Join(",", f.FrameId, f.Time.ToString("O"), Q(v.Key.Target), Q(v.Key.Edge), v.Key.Channel, N(v.Mtf50), N(v.Response), N(f.Frequency), Q(v.Status)));
    }
}
