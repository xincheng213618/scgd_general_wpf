using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using System.Reflection;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public class FlowNodeTimingTests
{
    [Fact]
    public void LargeRawImageLogsWholeReadAndWriteOperationsWithoutPerChunkStages()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionStageTests"));
        string directory = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        try
        {
            const int length = 2048 * 1024;
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 2048, Height = 1024, Channels = 1, SourceBpp = 8, Exposure = [10], Gain = 1,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            }, length, 0);
            using (var lease = frame.Acquire()) Marshal.Copy(new byte[length], 0, lease.RawPointer, length);
            var timing = new FlowNodeTiming();
            using (timing.Activate())
            {
                LocalFrameFileService.SaveCapture(frame, directory, "test");
                using var loaded = FlowNodeTiming.Run("OpenImage", () => LocalFrameFileService.Load(frame.CvRawFilePath));
                using var lease = loaded.Acquire();
                Assert.Equal(length, lease.RawLength);
            }
            var stages = timing.Finish().Stages;
            Assert.Equal("Completed", Assert.Single(stages, stage => stage.Name == "WriteRawFile").Status);
            Assert.Equal("Completed", Assert.Single(stages, stage => stage.Name == "ReadImageBuffer").Status);
            Assert.DoesNotContain(stages, stage => stage.Name == "CopyRawBuffer");
            Assert.DoesNotContain(stages, stage => stage.Name == "CopyImageBuffer");
        }
        finally
        {
            Assert.StartsWith(parent + Path.DirectorySeparatorChar, Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"text\":\"quoted \\\" and }\",\"items\":[1,2]}")]
    [InlineData("[1,2]")]
    [InlineData("\"literal }\"")]
    public void AppendingTimingPreservesTheOriginalPayloadAndCompressionRoundTrip(string json)
    {
        var original = JToken.Parse(json);
        var timing = new FlowNodeTiming();
        string payload = timing.SerializePayload(original);
        var encoded = ColorVision.Database.GzipTextPayloadCodec.Encode(payload);
        Assert.Equal(payload, ColorVision.Database.GzipTextPayloadCodec.Decode(encoded.CompressedBytes, encoded.Utf8Length));
        var parsed = JObject.Parse(payload);
        Assert.NotNull(parsed["Timing"]);
        parsed.Remove("Timing");
        Assert.True(JToken.DeepEquals(original, original is JObject ? parsed : parsed["Data"]));
    }

    [Fact]
    public void NestedStagesKeepInclusiveDurationsAndTheSnapshotDoesNotChange()
    {
        var timing = new FlowNodeTiming();
        using (timing.Activate())
        {
            FlowNodeTiming.Run("OpenImage", () => FlowNodeTiming.Run("DecodeImage", () => 42));
            FlowNodeTiming.Skip("Calibration");
        }
        var snapshot = timing.Finish();
        Assert.Equal(new[] { "OpenImage", "DecodeImage", "Calibration" }, snapshot.Stages.Select(stage => stage.Name));
        Assert.Null(snapshot.Stages[0].ParentId);
        Assert.Equal(snapshot.Stages[0].Id, snapshot.Stages[1].ParentId);
        Assert.InRange(snapshot.Stages[1].ElapsedMs, 0, snapshot.Stages[0].ElapsedMs);
        Assert.InRange(snapshot.Stages[0].ElapsedMs, 0, snapshot.TotalMs);
        Assert.Equal("Skipped", snapshot.Stages[2].Status);
        Assert.Equal(0, snapshot.Stages[2].ElapsedMs);
        using (timing.Activate()) FlowNodeTiming.Run("Late", () => 1);
        Assert.Same(snapshot, timing.Finish());
        Assert.Equal(3, snapshot.Stages.Count);
    }

    [Fact]
    public async Task ConcurrentBranchesKeepTheirOwnParentsAndExecutionsStayIsolated()
    {
        async Task<FlowNodeTimingSnapshot> Execute(string name)
        {
            var timing = new FlowNodeTiming();
            using (timing.Activate())
            using (var parent = FlowNodeTiming.Measure(name))
            {
                await Task.Yield();
                await Task.WhenAll(Enumerable.Range(0, 3).Select(i => Task.Run(() => FlowNodeTiming.Run("child" + i, () => i))));
                parent!.Complete();
            }
            return timing.Finish();
        }
        var results = await Task.WhenAll(Execute("first"), Execute("second"));
        Assert.Equal("first", results[0].Stages[0].Name);
        Assert.Equal("second", results[1].Stages[0].Name);
        foreach (var result in results)
        {
            Assert.Equal(4, result.Stages.Count);
            Assert.All(result.Stages.Skip(1), child => Assert.Equal(0, child.ParentId));
        }
        Assert.Null(FlowNodeTiming.Measure("outside"));
    }

    [Fact]
    public void FailureCancellationAndStageLimitDoNotChangeBusinessExceptions()
    {
        var timing = new FlowNodeTiming();
        using (timing.Activate())
        {
            var failure = new InvalidOperationException("cannot open image");
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => FlowNodeTiming.Run("OpenImage", () => throw failure)));
            Assert.Throws<OperationCanceledException>(() => FlowNodeTiming.Run("Algorithm", () => throw new OperationCanceledException()));
            for (int i = 0; i < FlowNodeTiming.MaxStages; i++) FlowNodeTiming.Run("Repeated", () => 1);
        }
        var snapshot = timing.Finish();
        Assert.Equal("Failed", snapshot.Stages[0].Status);
        Assert.Equal("Canceled", snapshot.Stages[1].Status);
        Assert.Equal(FlowNodeTiming.MaxStages, snapshot.Stages.Count);
        Assert.Equal(2, snapshot.OmittedStages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealLocalNodeCompletionPublishesTimingJsonOnSuccessAndFailure(bool fail)
    {
        var node = new TimingNode(fail);
        node.Create();
        FlowEngineNodeEndEventArgs? completed = null;
        node.nodeEndEvent += (_, args) => completed = args;
        var action = new CVStartCFC("timing-test");
        try
        {
            typeof(LocalFlowNodeBase).GetMethod("ExecuteCore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(node, [new CVTransAction(action)]);
            Assert.NotNull(completed);
            Assert.Equal(fail ? -1 : 0, completed.RecvStatusCode);
            var payload = JObject.Parse(completed.RecvPayload);
            Assert.Equal("ms", (string?)payload["Timing"]?["Unit"]);
            var stages = (JArray)payload["Timing"]!["Stages"]!;
            Assert.Contains(stages, stage => (string?)stage["Name"] == "OpenImage" && (string?)stage["Status"] == (fail ? "Failed" : "Completed"));
            if (!fail) Assert.Equal(7, (int?)payload["Answer"]);
            Assert.Null(FlowNodeTiming.Measure("outside"));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    private sealed class TimingNode(bool fail) : LocalFlowNodeBase("Timing", "Test", "Test")
    {
        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            FlowNodeTiming.Run("OpenImage", () => { if (fail) throw new InvalidOperationException("open failed"); });
            return new LocalNodeExecutionResult { Data = new { Answer = 7 } };
        }
    }
}
