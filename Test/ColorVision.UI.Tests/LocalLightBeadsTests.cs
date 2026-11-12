using ColorVision.Core;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Output;
using FlowEngineLib.Algorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.POI;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LocalLightBeadsTests
{
    // The handles are branch-local while their payload is run-owned; stale/cross-run handles must never fall back to SQL.
    [Fact]
    public void PointSetsStayOutOfMessagesAndExpireWithTheirRun()
    {
        var action = new CVStartCFC("led-runtime");
        var set = new LocalFlowPoiPointSet([new(1, 2), new(3, 4)], Metadata(), PoiShape.Circle, 3, 3);
        string handle = set.Publish(action);
        var branch = new CVStartCFC(action);
        Assert.True(LocalFlowPoiPointSet.TryResolve(branch, handle, out var resolved));
        Assert.Same(set, resolved);
        string json = JsonConvert.SerializeObject(action);
        Assert.DoesNotContain("centers", json, StringComparison.OrdinalIgnoreCase);
        var unrelated = new CVStartCFC("led-runtime");
        using var unrelatedResources = unrelated.RuntimeResources;
        Assert.Throws<InvalidOperationException>(() => LocalFlowPoiPointSet.TryResolve(unrelated, handle, out _));
        action.RuntimeResources.Dispose();
        Assert.Throws<InvalidOperationException>(() => LocalFlowPoiPointSet.TryResolve(branch, handle, out _));
    }

    // Exercise actual native POI sampling and both input snapshots with persistence enabled globally.
    // A runtime point set must still bypass every result/template DAO and avoid putting all points in diagnostic JSON.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealPoiUsesItsPointPortAndKeepsDenseResultsInMemory(bool singleInput)
    {
        var image = new CVStartCFC("led-memory-poi");
        using var resources = image.RuntimeResources;
        image.SetCurrentFrame(CreateCieFrame(7));
        image.MasterValue(null, 301, 30);
        var points = new CVStartCFC(image);
        points.SetCurrentFrame(CreateCieFrame(99));
        var set = new LocalFlowPoiPointSet([new(2, 3), new(4, 5)], Metadata(), PoiShape.Point, 1, 1);
        set.Publish(points);
        string csvDirectory = Path.Combine(Path.GetTempPath(), "cv-led-poi-" + Guid.NewGuid().ToString("N"));
        LocalFlowNodeBase node = singleInput ? new LocalPoiNode { ResultDirectory = csvDirectory } : new LocalRealPoiNode();
        try
        {
            using var execution = node.CreateCompatibilityExecution(new CVMQTTRequest
            {
                SerialNumber = image.SerialNumber, DeviceNodeCode = "led-poi", EventName = "POI"
            }, null, singleInput ? [points] : [image, points]);
            execution.Bind(points);
            execution.Execute();
            JObject summary = JObject.FromObject(execution.Complete(points)!);
            Assert.Equal(!singleInput, (bool)summary["MemoryOnly"]!);
            Assert.False((bool)summary["DatabasePersisted"]!);
            Assert.Equal(0, (int)summary["MasterId"]!);
            Assert.Equal(2, (int)summary["PointCount"]!);
            Assert.Equal(JTokenType.Null, summary["POIResult"]!.Type);
            string handle = (string)summary["ResultHandle"]!;
            Assert.True(resources.TryGet(handle, out LocalPoiResultSet? result));
            Assert.Equal(2, result!.Points.Count);
            Assert.All(result.Points, point => Assert.Equal(singleInput ? 99 : 7, Assert.IsType<POIResultDataCIEY>(point.Value).Y));
            Assert.Equal(handle, points.Data["MasterValue"]);
            if (singleInput)
            {
                string csvFile = (string)summary["ResultCsvFile"]!;
                string[] rows = File.ReadAllLines(csvFile);
                Assert.Equal(3, rows.Length);
                Assert.Equal(15, rows[1].Split(',').Length);
                Assert.Equal("99", rows[1].Split(',')[7]);
            }
        }
        finally
        {
            if (Directory.Exists(csvDirectory))
            {
                foreach (string file in Directory.GetFiles(csvDirectory)) File.Delete(file);
                Directory.Delete(csvDirectory);
            }
        }
    }

    [Fact]
    public void RuntimeTemplateRejectsDifferentImageCoordinates()
    {
        var set = new LocalFlowPoiPointSet([new(2, 3)], Metadata(), PoiShape.Circle, 3, 3);
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 8, Height = 8, SourceBpp = 8, Channels = 1, FlipMode = CVImageFlipMode.X
        }, 64, 0);
        Assert.Throws<InvalidOperationException>(() => set.CreateTemplate(frame));
    }

    [Fact]
    public void RuntimePointConnectionSelectsLocalExecutionForBothLegacyPoiNodes() => StaTest.Run(() =>
    {
        var detector = new LocalLightBeadsNode();
        var poi = new POINode();
        var real = new RealPOINode();
        foreach (STNode node in new STNode[] { detector, poi, real }) node.Create();
        detector.GetAllOutputOptions()[0].ConnectOption(poi.GetAllInputOptions()[0], false);
        detector.GetAllOutputOptions()[0].ConnectOption(real.GetAllInputOptions()[1], false);
        Assert.False(poi.RequiresRemoteService);
        Assert.False(real.RequiresRemoteService);
    });

    [Fact]
    public void DetectorAndPoiCompleteAnOfflineFlowWithoutPoiTemplates() => StaTest.Run(() =>
    {
        using var canvas = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        var seed = new CieSeedNode();
        var detector = new LocalLightBeadsNode { Threshold = 1, PointShape = LocalLightBeadsPoiShape.Point, InspectDefects = false };
        var poi = new LocalPoiNode();
        var end = new CVEndNode();
        STNode[] nodes = [start, seed, detector, poi, end];
        foreach (STNode node in nodes) { node.Create(); canvas.Nodes.Add(node); }
        for (int index = 0; index < nodes.Length - 1; index++)
            nodes[index].GetAllOutputOptions()[0].ConnectOption(nodes[index + 1].GetAllInputOptions()[0], false);
        using var control = new FlowEngineControl(canvas, false, new FlowNodeManager()) { PersistResults = false };
        var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Finished += (_, result) => completed.TrySetResult(result);
        Assert.True(control.TryStartNode(start.NodeName, "led-offline-flow"));
        Assert.Equal(StatusTypeEnum.Completed, completed.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult().Status);
        Assert.Equal(0, start.PublishCount);
    });

    // Static detections must not enter the viewport scaling/rebuild path or accumulate history entries on zoom.
    [Fact]
    public void DenseOverlayRemainsFrozenAcrossViewportChangesAndKeepsSingleUndo() => StaTest.Run(() =>
    {
        var points = Enumerable.Range(0, 10000).Select(index => new Point(20 + index % 100 * 32, 20 + index / 100 * 24)).ToArray();
        var visual = new DensePointVisual(points, [], 2, 100);
        DrawingGroup drawing = visual.FrozenDrawing;
        using var canvas = new DrawCanvas { Scale = 100 };
        canvas.AddVisualCommand(visual);
        Assert.Single(canvas.Visuals);
        Assert.Single(canvas.UndoStack);
        Assert.Equal(points.Length, visual.PointCount);
        Assert.True(visual.TileCount > 1);
        Assert.True(visual.TileCount < visual.PointCount);
        Assert.True(drawing.IsFrozen);
        Assert.False((object)visual is ILayoutScaleDrawingVisual);
        Assert.Equal(points.Length, visual.RenderedPointCount);
        canvas.Scale = 1;
        canvas.ApplyLayoutScaleToVisuals();
        visual.ApplyLayoutScale(new DrawingVisualScaleContext(true, 1, 0));
        canvas.Scale = 0.5;
        canvas.ApplyLayoutScaleToVisuals();
        Assert.Same(drawing, visual.FrozenDrawing);
        Assert.Equal(points.Length, visual.RenderedPointCount);
        canvas.Undo();
        Assert.Empty(canvas.Visuals);
        canvas.Redo();
        Assert.Single(canvas.Visuals);
    });

    // The display uses child tiles for culling; a rendered/background save must still include the complete frozen scene.
    [Fact]
    public void StaticPointTilesDoNotInterceptInputAndSurviveBackgroundSnapshot() => StaTest.Run(() =>
    {
        var source = new WriteableBitmap(1024, 64, 96, 96, PixelFormats.Bgra32, null);
        using var canvas = new DrawCanvas { Source = source };
        var visual = new DensePointVisual([new Point(510, 20)], [new Point(530, 44)], 6, normalBrush: Brushes.LimeGreen, defectBrush: Brushes.Red);
        canvas.AddVisualCommand(visual);
        Assert.Null(VisualTreeHelper.HitTest(visual, new Point(516, 20)));
        var capture = new ImageSnapshotCapture();
        try
        {
            using var snapshot = capture.CaptureForBackgroundSave(canvas, source, 96, 96, true, () => { });
            Assert.NotNull(snapshot);
            var scene = new DrawingVisual();
            using (DrawingContext context = scene.RenderOpen()) context.DrawDrawing(snapshot.Scene);
            foreach (Visual output in new Visual[] { visual, scene })
            {
                var bitmap = new RenderTargetBitmap(1024, 64, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(output);
                byte[] pixels = new byte[1024 * 64 * 4];
                bitmap.CopyPixels(pixels, 1024 * 4, 0);
                Assert.Contains(Enumerable.Range(504, 14), x => pixels[(20 * 1024 + x) * 4 + 1] > 0);
                Assert.Contains(Enumerable.Range(524, 14), x => pixels[(44 * 1024 + x) * 4 + 2] > 0);
            }
        }
        finally { capture.Dispose(); }
    });

    [Fact]
    public void DetectorRejectsOutOfBoundsRoiBeforeCallingNative()
    {
        var image = new HImage { pData = (IntPtr)1, cols = 10, rows = 10, depth = 8, channels = 1, stride = 10, isDispose = true };
        Assert.Throws<ArgumentOutOfRangeException>(() => LightBeadsDetector.Detect(image, new RoiRect(9, 9, 5, 5)));
    }

    private static LocalFrameMetadata Metadata() => new() { Width = 8, Height = 8, CieBpp = 32, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvCie };

    private static LocalFlowFrame CreateCieFrame(float value)
    {
        var frame = LocalFlowFrame.Allocate(Metadata(), 0, 8 * 8 * sizeof(float));
        using var lease = frame.Acquire();
        Marshal.Copy(Enumerable.Repeat(value, 64).ToArray(), 0, lease.CiePointer, 64);
        return frame;
    }

    private sealed class CieSeedNode() : LocalFlowNodeBase("seed", "seed", "seed")
    {
        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            action.SetCurrentFrame(CreateCieFrame(7));
            return new LocalNodeExecutionResult();
        }
    }
}
