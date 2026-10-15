using ColorVision.Core;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Images.FileFusion;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LocalFileFusionNodeTests
{
    [Fact]
    public void NodePortsAndSettingsRoundTripWithoutSplittingCommaInPaths()
    {
        LocalFileFusionNode node = new()
        {
            InputMode = FileFusionInputMode.FileList, InputDirectory = @"C:\images",
            Files = [@"C:\图像\plane,2.png", @"C:\图像\plane1.png"],
            Mode = FileFusionMode.GPUAsync, ResultDirectory = @"C:\results",
        };
        node.Create();
        Assert.Equal("LocalFileFusion", node.NodeType);
        Assert.Equal(["IN"], node.GetAllInputOptions().Select(port => port.Text));
        Assert.Equal(["OUT"], node.GetAllOutputOptions().Select(port => port.Text));
        LocalFileFusionNode restored = new();
        restored.Create();
        restored.OnLoadNode(ParseState(node.GetSaveData()));
        Assert.Equal(node.InputMode, restored.InputMode);
        Assert.Equal(node.InputDirectory, restored.InputDirectory);
        Assert.Equal(node.Files, restored.Files);
        Assert.Equal(node.Mode, restored.Mode);
        Assert.Equal(node.ResultDirectory, restored.ResultDirectory);
    }

    [Theory]
    [InlineData(FileFusionInputMode.Folder)]
    [InlineData(FileFusionInputMode.FileList)]
    public void SuccessSavesImageAndHandsFrameAndImageRecordToDownstream(FileFusionInputMode inputMode)
    {
        using var fixture = new FusionFixture();
        string ten = fixture.Add("plane10.png"), two = fixture.Add("plane2.png");
        var services = new FakeServices();
        var node = new LocalFileFusionNode(services)
        {
            InputMode = inputMode, InputDirectory = fixture.Directory, Files = [ten, two],
            ResultDirectory = Path.Combine(fixture.Directory, "results"), Mode = FileFusionMode.CPU,
        };
        var action = new CVStartCFC("fusion-test");
        try
        {
            LocalFileFusionResultData result = node.ExecuteSynchronously(action);
            Assert.Equal(inputMode == FileFusionInputMode.Folder ? new[] { two, ten } : new[] { ten, two }, services.Files);
            Assert.True(File.Exists(result.ImageFilePath));
            Assert.Equal(42, result.MasterId);
            Assert.Equal(100, result.MasterResultType);
            Assert.Equal(result.ImageFilePath, action.Data["MasterValue"]);
            Assert.Equal(42, action.Data["MasterId"]);
            Assert.Equal(100, action.Data["MasterResultType"]);
            Assert.True(action.TryAcquireCurrentFrame(out LocalFlowFrameLease? lease));
            using (lease)
            {
                Assert.NotNull(lease);
                Assert.Equal(42, lease.MasterId);
                Assert.Equal(4, lease.Metadata.Width);
                Assert.All(lease.CopyRawToArray(), pixel => Assert.Equal(123, pixel));
            }
            Assert.Equal(["Batch", "Fusion", "Save", "Publish"], services.Events);
            Assert.NotNull(services.Model);
            Assert.Null(services.Model.DeviceCode);
            Assert.Equal(result.ImageFilePath, services.Model.FileUrl);
            Assert.Equal(17, services.Model.BatchId);
            JObject parameters = JObject.Parse(services.Model.Params!);
            Assert.Equal(services.Files, parameters["InputFiles"]!.ToObject<string[]>());
            Assert.Equal("CPU", parameters.Value<string>("ActualMode"));
            // Output in a child directory never enters the next folder snapshot.
            Assert.Equal(2, FileFusion.GetFolderFiles(fixture.Directory).Length);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void StoppedFlowDiscardsLateFusionWithoutSavingOrPublishing()
    {
        using var fixture = new FusionFixture();
        var action = new CVStartCFC("stopped");
        var services = new FakeServices { DuringFusion = () => action.RuntimeResources.Dispose() };
        var node = new LocalFileFusionNode(services)
        {
            InputMode = FileFusionInputMode.FileList, Files = [fixture.Add("a.png"), fixture.Add("b.png")],
            ResultDirectory = Path.Combine(fixture.Directory, "results"),
        };
        Assert.Throws<OperationCanceledException>(() => node.ExecuteSynchronously(action));
        Assert.Equal(["Batch", "Fusion"], services.Events);
        Assert.False(Directory.Exists(node.ResultDirectory));
        Assert.False(action.Data.ContainsKey("MasterId"));
    }

    [Fact]
    public void PersistenceFailureDoesNotReplaceUpstreamResultOrPublish()
    {
        using var fixture = new FusionFixture();
        var action = new CVStartCFC("failed");
        action.MasterValue("upstream", 9, 100);
        var services = new FakeServices { PersistedId = 0 };
        var node = new LocalFileFusionNode(services)
        {
            InputMode = FileFusionInputMode.FileList, Files = [fixture.Add("a.png"), fixture.Add("b.png")],
            ResultDirectory = Path.Combine(fixture.Directory, "results"),
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));
            Assert.Equal(9, action.Data["MasterId"]);
            Assert.False(action.TryAcquireCurrentFrame(out _));
            Assert.DoesNotContain("Publish", services.Events);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void OutputCannotPolluteInputFolder()
    {
        using var fixture = new FusionFixture();
        Assert.Throws<InvalidOperationException>(() => LocalFileFusionNode.ResolveOutputDirectory(
            fixture.Directory + Path.DirectorySeparatorChar, [fixture.Add("a.png")]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalNodeBaseDoesNotPublishCompletionAfterFlowResourcesAreReleased(bool fail)
    {
        var node = new StoppedNode(fail);
        node.Create();
        bool published = false;
        node.nodeEndEvent += (_, _) => published = true;
        var action = new CVStartCFC("late-completion");
        typeof(LocalFlowNodeBase).GetMethod("ExecuteCore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(node, [new CVTransAction(action)]);
        Assert.False(published);
        Assert.False(action.Data.ContainsKey("ErrorNodeName"));
    }

    private sealed class StoppedNode(bool fail) : LocalFlowNodeBase("Stopped", "Stopped", "Stopped")
    {
        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            action.RuntimeResources.Dispose();
            if (fail) throw new OperationCanceledException();
            return new();
        }
    }

    private sealed class FakeServices : IFileFusionServices
    {
        public List<string> Events { get; } = new();
        public string[] Files { get; private set; } = [];
        public MeasureResultImgModel? Model { get; private set; }
        public int PersistedId { get; init; } = 42;
        public Action? DuringFusion { get; init; }
        public int ResolveBatchId(string serialNumber) { Events.Add("Batch"); return 17; }
        public FileFusionResult Execute(IReadOnlyList<string> files, FileFusionMode mode, CancellationToken cancellationToken)
        {
            Events.Add("Fusion");
            Files = files.ToArray();
            DuringFusion?.Invoke();
            BitmapSource image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Gray8, null, Enumerable.Repeat((byte)123, 16).ToArray(), 4);
            image.Freeze();
            return new(image, Files, FileFusionMode.CPU, 1, 2, 3, 6);
        }
        public int SaveResult(MeasureResultImgModel model) { Events.Add("Save"); Model = model; return PersistedId; }
        public void Publish(CVStartCFC action, string nodeId, int zIndex, MeasureResultImgModel model)
        {
            Events.Add("Publish");
            Assert.Equal(PersistedId, model.Id);
            Assert.True(action.TryGetCurrentFrame(out _));
        }
    }

    private static Dictionary<string, byte[]> ParseState(byte[] data)
    {
        int position = 0;
        position += data[position] + 1;
        position += data[position] + 1;
        Dictionary<string, byte[]> state = new();
        while (position < data.Length)
        {
            int keyLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            string key = Encoding.UTF8.GetString(data, position, keyLength);
            position += keyLength;
            int valueLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            state.Add(key, data.AsSpan(position, valueLength).ToArray());
            position += valueLength;
        }
        return state;
    }
}
