using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

/// <summary>Per-execution, in-memory stage log. Only the completion payload is persisted.</summary>
internal sealed class FlowNodeTiming
{
    internal sealed record Context(FlowNodeTiming Owner, int? ParentId);
    private static readonly AsyncLocal<Context?> Current = new();
    private readonly object sync = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly List<Stage> stages = new();
    private FlowNodeTimingSnapshot? snapshot;
    private int omitted;
    internal const int MaxStages = 256;

    internal IDisposable Activate()
    {
        var activation = new Activation(Current.Value);
        Current.Value = new(this, null);
        return activation;
    }

    private sealed class Activation(Context? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }

    internal static Stage? Measure(string name)
    {
        Context? context = Current.Value;
        if (context == null) return null;
        FlowNodeTiming owner = context.Owner;
        lock (owner.sync)
        {
            if (owner.snapshot != null) return null;
            if (owner.stages.Count == MaxStages)
            {
                owner.omitted++;
                return null;
            }
            var stage = new Stage(owner, context, owner.stages.Count, name);
            owner.stages.Add(stage);
            Current.Value = new(owner, stage.Id);
            return stage;
        }
    }

    internal static T Run<T>(string name, Func<T> work)
    {
        using Stage? stage = Measure(name);
        try
        {
            T result = work();
            stage?.Complete();
            return result;
        }
        catch (OperationCanceledException) { stage?.Cancel(); throw; }
    }

    internal static void Run(string name, Action work)
    {
        using Stage? stage = Measure(name);
        try { work(); stage?.Complete(); }
        catch (OperationCanceledException) { stage?.Cancel(); throw; }
    }

    internal static void Skip(string name)
    {
        using Stage? stage = Measure(name);
        stage?.Skip();
    }

    internal FlowNodeTimingSnapshot Finish()
    {
        lock (sync)
        {
            long end = Stopwatch.GetTimestamp();
            return snapshot ??= new(1, "ms", Milliseconds(started, end), omitted,
                stages.Select(stage => stage.ToSnapshot(started, end)).ToArray());
        }
    }

    internal string SerializePayload(object? data)
    {
        FlowNodeTimingSnapshot timing = Finish();
        string result = data == null ? "{}" : JsonConvert.SerializeObject(data, Formatting.None);
        string timingJson = JsonConvert.SerializeObject(timing, Formatting.None);
        ReadOnlySpan<char> json = result.AsSpan().Trim();
        // Preserve existing response fields without building a second JSON object tree for large POI results.
        if (json.Length >= 2 && json[0] == '{' && json[^1] == '}')
        {
            string separator = json[1..^1].Trim().IsEmpty ? "\"Timing\":" : ",\"Timing\":";
            return string.Concat(json[..^1], separator, timingJson, "}");
        }
        return $"{{\"Data\":{result},\"Timing\":{timingJson}}}";
    }

    private static double Milliseconds(long start, long end) => Math.Round((end - start) * 1000d / Stopwatch.Frequency, 3);

    internal sealed class Stage : IDisposable
    {
        private readonly FlowNodeTiming owner;
        private readonly Context previous;
        private readonly string name;
        private readonly long start = Stopwatch.GetTimestamp();
        private long? end;
        private string status = "Running";
        internal int Id { get; }

        internal Stage(FlowNodeTiming owner, Context previous, int id, string name)
        {
            this.owner = owner;
            this.previous = previous;
            this.name = name;
            Id = id;
        }

        internal void Complete() => Stop("Completed");
        internal void Cancel() => Stop("Canceled");
        internal void Skip() => Stop("Skipped");

        private void Stop(string state)
        {
            lock (owner.sync)
            {
                if (end.HasValue || owner.snapshot != null) return;
                end = Stopwatch.GetTimestamp();
                status = state;
            }
        }

        internal FlowNodeTimingStage ToSnapshot(long origin, long now) => new(Id, previous.ParentId, name,
            Milliseconds(origin, start), status == "Skipped" ? 0 : Milliseconds(start, end ?? now), status);

        public void Dispose()
        {
            Stop("Failed"); // Leaving a stage without Complete preserves the failing boundary.
            Current.Value = previous;
        }
    }
}

internal sealed record FlowNodeTimingSnapshot(int Version, string Unit, double TotalMs, int OmittedStages, IReadOnlyList<FlowNodeTimingStage> Stages);
internal sealed record FlowNodeTimingStage(int Id, int? ParentId, string Name, double StartMs, double ElapsedMs, string Status);
