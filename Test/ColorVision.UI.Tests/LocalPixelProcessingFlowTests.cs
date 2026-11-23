using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib.Base;
using OpenCvSharp;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LocalPixelProcessingFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectedNodesSavePngWithSelectedRangeAndPreserveTheSourceBranch(bool fixedRange)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-PixelFlow-");
        var action = new CVStartCFC("pixel-flow") { PersistResults = false };
        byte[] originalPixels = [10, 20, 30, 40, 50, 60];
        LocalFlowFrame original = SetSourceFrame(action, originalPixels);
        var source = new SourceNode();
        var adjust = new LocalBasicImageAdjustmentNode { Exposure = 1 };
        var rgb = new LocalRgbChannelProcessingNode { RedScale = 1.5, OutputChannel = LocalRgbOutputChannel.R };
        var pseudo = new LocalPseudoColorOutputNode { SavePng = true, OutputDirectory = directory.FullName };
        Assert.Equal(LocalPseudoColorRangeMode.Auto, pseudo.RangeMode);
        if (fixedRange)
        {
            pseudo.RangeMode = LocalPseudoColorRangeMode.Fixed;
            pseudo.Minimum = 0;
            pseudo.Maximum = 255;
        }
        var sourceBranch = new SinkNode();
        var processedBranch = new SinkNode();
        try
        {
            foreach (STNode node in new STNode[] { source, adjust, rgb, pseudo, sourceBranch, processedBranch }) node.Create();
            Connect(source, sourceBranch);
            Connect(source, adjust);
            Connect(adjust, rgb);
            Connect(rgb, pseudo);
            Connect(pseudo, processedBranch);

            Assert.Single(source.GetAllOutputOptions()).TransferData(action);
            CVStartCFC result = await processedBranch.Received.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result.IsRunning, result.Data.GetValueOrDefault("Msg")?.ToString());
            CVStartCFC unprocessed = await sourceBranch.Received.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(action, unprocessed);
            Assert.True(unprocessed.TryGetCurrentFrame(out LocalFlowFrame? sourceFrame));
            Assert.Same(original, sourceFrame);
            using LocalFlowFrameLease originalLease = original.Acquire();
            Assert.Equal(originalPixels, originalLease.CopyRawToArray());

            Assert.True(result.TryGetCurrentFrame(out LocalFlowFrame? outputFrame));
            Assert.NotNull(outputFrame);
            Assert.NotEqual(original.FrameId, outputFrame.FrameId);
            Assert.Equal((2, 1, 8, 3), (outputFrame.Metadata.Width, outputFrame.Metadata.Height, outputFrame.Metadata.SourceBpp, outputFrame.Metadata.Channels));
            using LocalFlowFrameLease outputLease = outputFrame.Acquire();
            using var indices = new Mat(1, 2, MatType.CV_8UC1);
            byte[] expectedIndices = fixedRange ? [90, 180] : [0, 255];
            Marshal.Copy(expectedIndices, 0, indices.Data, expectedIndices.Length);
            using var expected = new Mat();
            Cv2.ApplyColorMap(indices, expected, ColormapTypes.Jet);
            byte[] expectedPixels = new byte[6];
            Marshal.Copy(expected.Data, expectedPixels, 0, expectedPixels.Length);
            Assert.Equal(expectedPixels, outputLease.CopyRawToArray());

            FileInfo png = Assert.Single(directory.GetFiles());
            Assert.Equal(".png", png.Extension);
            Assert.Equal(png.FullName, Assert.IsType<string>(result.Data["LocalPseudoColorPngPath"]));
            using Mat decoded = Cv2.ImRead(png.FullName, ImreadModes.Unchanged);
            Assert.Equal((1, 2, MatType.CV_8UC3), (decoded.Rows, decoded.Cols, decoded.Type()));
            Assert.Equal(0d, Cv2.Norm(expected, decoded, NormTypes.INF));
        }
        finally
        {
            action.RuntimeResources.Dispose();
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task FixedRangeDistinguishesNeighboringSixteenBitValues()
    {
        var action = new CVStartCFC("narrow-pseudo-range") { PersistResults = false };
        using FlowRuntimeResources resources = action.RuntimeResources;
        ushort[] samples = [59999, 60000, 60001, 60002, 60003];
        SetSourceFrame(action, MemoryMarshal.AsBytes(samples.AsSpan()).ToArray(), samples.Length, sourceBpp: 16, channels: 1);
        var pseudo = new LocalPseudoColorOutputNode
        {
            RangeMode = LocalPseudoColorRangeMode.Fixed,
            Minimum = 60000,
            Maximum = 60002,
        };
        var sink = new SinkNode();
        pseudo.Create();
        sink.Create();
        Connect(pseudo, sink);
        Assert.Single(pseudo.GetAllInputOptions()).ReceiveData(action);

        CVStartCFC result = await sink.Received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.IsRunning, result.Data.GetValueOrDefault("Msg")?.ToString());
        Assert.True(result.TryAcquireCurrentFrame(out LocalFlowFrameLease? output));
        Assert.NotNull(output);
        using (output)
        {
            Assert.Equal((5, 1, 8, 3), (output.Metadata.Width, output.Metadata.Height, output.Metadata.SourceBpp, output.Metadata.Channels));
            using var indices = new Mat(1, 3, MatType.CV_8UC1);
            byte[] paletteIndices = [0, 128, 255];
            Marshal.Copy(paletteIndices, 0, indices.Data, paletteIndices.Length);
            using var jet = new Mat();
            Cv2.ApplyColorMap(indices, jet, ColormapTypes.Jet);
            byte[] expected = new byte[15];
            Marshal.Copy(jet.Data, expected, 3, 9);
            expected.AsSpan(12).Fill(byte.MaxValue);
            Assert.Equal(expected, output.CopyRawToArray());
        }
    }

    [Fact]
    public async Task InvalidFixedRangeFailsThroughOutputWithoutSavingOrReplacingTheFrame()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-PixelFlow-");
        var action = new CVStartCFC("invalid-pseudo-range") { PersistResults = false };
        LocalFlowFrame original = SetSourceFrame(action, [10, 20, 30, 40, 50, 60]);
        var pseudo = new LocalPseudoColorOutputNode
        {
            RangeMode = LocalPseudoColorRangeMode.Fixed,
            Minimum = 200,
            Maximum = 100,
            SavePng = true,
            OutputDirectory = directory.FullName,
        };
        var sink = new SinkNode();
        try
        {
            pseudo.Create();
            sink.Create();
            Connect(pseudo, sink);
            Assert.Single(pseudo.GetAllInputOptions()).ReceiveData(action);

            CVStartCFC failed = await sink.Received.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(StatusTypeEnum.Failed, failed.FlowStatus);
            Assert.True(failed.TryGetCurrentFrame(out LocalFlowFrame? frame));
            Assert.Same(original, frame);
            Assert.Empty(directory.GetFiles());
        }
        finally
        {
            action.RuntimeResources.Dispose();
            directory.Delete(recursive: true);
        }
    }

    private static LocalFlowFrame SetSourceFrame(CVStartCFC action, byte[] pixels, int width = 2, int sourceBpp = 8, int channels = 3)
    {
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = width,
            Height = 1,
            SourceBpp = sourceBpp,
            Channels = channels,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
        }, rawLength: pixels.Length, cieLength: 0);
        using LocalFlowFrameLease lease = frame.Acquire();
        Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
        action.SetCurrentFrame(frame);
        return frame;
    }

    private static void Connect(STNode output, STNode input)
    {
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(output.GetAllOutputOptions())
            .ConnectOption(Assert.Single(input.GetAllInputOptions()), isOwnerOfOwner: false));
    }

    private sealed class SourceNode : STNode
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
        }
    }

    private sealed class SinkNode : STNode
    {
        private readonly TaskCompletionSource<CVStartCFC> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CVStartCFC> Received => completion.Task;

        protected override void OnCreate()
        {
            base.OnCreate();
            STNodeOption input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
            input.DataTransfer += (_, e) =>
            {
                if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data is CVStartCFC action)
                    completion.TrySetResult(action);
            };
        }
    }
}
