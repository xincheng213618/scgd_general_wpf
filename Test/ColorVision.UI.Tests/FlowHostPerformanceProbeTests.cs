using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Runtime;
using FlowEngineLib.Start;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

[Trait("Category", "PerformanceProbe")]
public sealed class FlowHostPerformanceProbeTests
{
    private readonly ITestOutputHelper _output;

    public FlowHostPerformanceProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    public void CompareEditorAndHeadlessRuntimeHosts(int passThroughNodeCount)
    {
        RunInSta(() =>
        {
            byte[] canvas = CreateCanvas(passThroughNodeCount);
            HostProbeResult editor = Measure(
                canvas,
                passThroughNodeCount,
                headless: false);
            HostProbeResult headless = Measure(
                canvas,
                passThroughNodeCount,
                headless: true);

            _output.WriteLine(
                $"nodes={passThroughNodeCount + 2}, canvas={canvas.Length:N0} bytes");
            WriteResult("editor", editor);
            WriteResult("headless", headless);
            _output.WriteLine(
                $"load ratio(headless/editor)={headless.LoadMedianMs / editor.LoadMedianMs:F3}, "
                + $"load alloc ratio={headless.LoadAllocatedBytes / editor.LoadAllocatedBytes:F3}, "
                + $"run ratio={headless.RunMedianMicroseconds / editor.RunMedianMicroseconds:F3}");

            Assert.True(editor.LoadMedianMs > 0);
            Assert.True(headless.LoadMedianMs > 0);
            Assert.True(editor.RunMedianMicroseconds > 0);
            Assert.True(headless.RunMedianMicroseconds > 0);
        });
    }

    private void WriteResult(string name, HostProbeResult result)
    {
        _output.WriteLine(
            $"{name}: load median={result.LoadMedianMs:F3}ms "
            + $"p95={result.LoadP95Ms:F3}ms "
            + $"alloc={result.LoadAllocatedBytes:N0}B; "
            + $"run median={result.RunMedianMicroseconds:F2}us "
            + $"p95={result.RunP95Microseconds:F2}us "
            + $"alloc={result.RunAllocatedBytes:N0}B");
    }

    private static HostProbeResult Measure(
        byte[] canvas,
        int passThroughNodeCount,
        bool headless)
    {
        const int loadIterations = 20;
        int runIterations = passThroughNodeCount >= 500 ? 300 : 1_000;
        for (int i = 0; i < 3; i++)
            MeasureOneLoad(canvas, headless);

        var loadSamples = new double[loadIterations];
        long loadAllocations = 0;
        for (int i = 0; i < loadIterations; i++)
        {
            (loadSamples[i], long allocatedBytes) =
                MeasureOneLoad(canvas, headless);
            loadAllocations += allocatedBytes;
        }

        (double[] runSamples, long runAllocatedBytes) =
            MeasureRuns(canvas, headless, runIterations);
        Array.Sort(loadSamples);
        Array.Sort(runSamples);
        return new HostProbeResult(
            Median(loadSamples),
            Percentile95(loadSamples),
            loadAllocations / (double)loadIterations,
            Median(runSamples),
            Percentile95(runSamples),
            runAllocatedBytes / (double)runIterations);
    }

    private static (double ElapsedMs, long AllocatedBytes) MeasureOneLoad(
        byte[] canvas,
        bool headless)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        if (headless)
        {
            using var container = new CVNodeContainer();
            using var control = new FlowEngineControl(
                container,
                isAutoStartName: false,
                new FlowNodeManager());
            control.Load(canvas, waitReady: false);
        }
        else
        {
            using var editor = new STNodeEditor();
            using var control = new FlowEngineControl(
                editor,
                isAutoStartName: false,
                new FlowNodeManager());
            control.Load(canvas, waitReady: false);
        }
        long elapsed = Stopwatch.GetTimestamp() - started;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return (
            elapsed * 1_000d / Stopwatch.Frequency,
            allocated);
    }

    private static (double[] Samples, long AllocatedBytes) MeasureRuns(
        byte[] canvas,
        bool headless,
        int iterations)
    {
        STNodeEditor? editor = null;
        CVNodeContainer? container = null;
        FlowEngineControl control;
        if (headless)
        {
            container = new CVNodeContainer();
            control = new FlowEngineControl(
                container,
                isAutoStartName: false,
                new FlowNodeManager());
        }
        else
        {
            editor = new STNodeEditor();
            control = new FlowEngineControl(
                editor,
                isAutoStartName: false,
                new FlowNodeManager());
        }

        using (control)
        {
            control.Load(canvas, waitReady: false);
            var runner = new FlowEngineRunner(control);
            for (int i = 0; i < 20; i++)
            {
                FlowEngineRunResult warmResult = runner.RunAsync(
                        "BenchmarkStart",
                        $"warm-{i}",
                        TimeSpan.FromSeconds(1))
                    .GetAwaiter()
                    .GetResult();
                Assert.Equal(
                    FlowEngineRunTermination.Completed,
                    warmResult.Termination);
            }

            var samples = new double[iterations];
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                long started = Stopwatch.GetTimestamp();
                FlowEngineRunResult result = runner.RunAsync(
                        "BenchmarkStart",
                        $"run-{i}",
                        TimeSpan.FromSeconds(1))
                    .GetAwaiter()
                    .GetResult();
                samples[i] = (Stopwatch.GetTimestamp() - started)
                    * 1_000_000d
                    / Stopwatch.Frequency;
                Assert.Equal(
                    FlowEngineRunTermination.Completed,
                    result.Termination);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread()
                - allocatedBefore;
            editor?.Dispose();
            container?.Dispose();
            return (samples, allocated);
        }
    }

    private static byte[] CreateCanvas(int passThroughNodeCount)
    {
        using var editor = new STNodeEditor();
        var start = new BenchmarkStartNode();
        var end = new CVEndNode();
        start.Create();
        end.Create();
        editor.Nodes.Add(start);
        STNodeOption output = start.m_op_start;
        for (int i = 0; i < passThroughNodeCount; i++)
        {
            var node = new BenchmarkPassThroughNode();
            node.Create();
            node.Left = 200 + i * 10;
            editor.Nodes.Add(node);
            Assert.Equal(
                ConnectionStatus.Connected,
                output.ConnectOption(node.Input));
            output = node.Output;
        }
        editor.Nodes.Add(end);
        Assert.Equal(
            ConnectionStatus.Connected,
            output.ConnectOption(end.m_in_start));
        return editor.GetCanvasData();
    }

    private static double Median(double[] sorted)
    {
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
    }

    private static double Percentile95(double[] sorted)
    {
        int index = (int)Math.Ceiling(sorted.Length * 0.95d) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed record HostProbeResult(
        double LoadMedianMs,
        double LoadP95Ms,
        double LoadAllocatedBytes,
        double RunMedianMicroseconds,
        double RunP95Microseconds,
        double RunAllocatedBytes);
}

public sealed class BenchmarkStartNode : BaseStartNode
{
    public BenchmarkStartNode()
        : base("Benchmark start")
    {
        NodeName = "BenchmarkStart";
    }
}

public sealed class BenchmarkPassThroughNode : STNode
{
    public STNodeOption Input { get; private set; } = STNodeOption.Empty;

    public STNodeOption Output { get; private set; } = STNodeOption.Empty;

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
        Output = OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
        Input.DataTransfer += Input_DataTransfer;
    }

    private void Input_DataTransfer(
        object sender,
        STNodeOptionEventArgs e)
    {
        Output.TransferData(e.TargetOption.Data);
    }
}
