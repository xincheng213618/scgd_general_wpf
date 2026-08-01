using FlowEngineLib.Base;
using FlowEngineLib.Logical;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Text;
using Xunit;

namespace ColorVision.UI.Tests;

public class ManualBranchNodeTests
{
    [Fact]
    public void NodesExposeFixedABPortsAndActivePath()
    {
        ManualBranchNode branch = new();
        AnyPathMergeNode merge = new();

        branch.Create();
        merge.Create();

        Assert.Equal("ManualBranch", branch.NodeType);
        Assert.Equal(ManualBranchPath.A, branch.SelectedPath);
        Assert.Equal(new[] { "IN" }, branch.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(new[] { "OUT_A [ON]", "OUT_B" }, branch.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal(new[] { "IN_A", "IN_B" }, merge.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(new[] { "OUT" }, merge.GetAllOutputOptions().Select(option => option.Text));

        branch.SelectedPath = ManualBranchPath.B;

        Assert.Equal(new[] { "OUT_A", "OUT_B [ON]" }, branch.GetAllOutputOptions().Select(option => option.Text));
    }

    [Fact]
    public void SelectedPathAloneReachesMergeOutput()
    {
        FlowSourceNode source = new();
        ManualBranchNode branch = new();
        AnyPathMergeNode merge = new();
        FlowSinkNode sink = new();
        Create(source, branch, merge, sink);
        Connect(source.Output, branch.GetAllInputOptions()[0]);
        Connect(branch.GetAllOutputOptions()[0], merge.GetAllInputOptions()[0]);
        Connect(branch.GetAllOutputOptions()[1], merge.GetAllInputOptions()[1]);
        Connect(merge.GetAllOutputOptions()[0], sink.Input);

        CVStartCFC pathA = new("path-a");
        source.Send(pathA);

        Assert.Equal(new[] { pathA }, sink.Received);

        branch.SelectedPath = ManualBranchPath.B;
        CVStartCFC pathB = new("path-b");
        source.Send(pathB);

        Assert.Equal(new[] { pathA, pathB }, sink.Received);
    }

    [Fact]
    public void InactivePathReceivesNoTransferEvent()
    {
        FlowSourceNode source = new();
        ManualBranchNode branch = new();
        FlowSinkNode sinkA = new();
        FlowSinkNode sinkB = new();
        Create(source, branch, sinkA, sinkB);
        Connect(source.Output, branch.GetAllInputOptions()[0]);
        Connect(branch.GetAllOutputOptions()[0], sinkA.Input);
        Connect(branch.GetAllOutputOptions()[1], sinkB.Input);
        int initialAEvents = sinkA.TransferCount;
        int initialBEvents = sinkB.TransferCount;

        source.Send(new CVStartCFC("path-a"));

        Assert.Equal(initialAEvents + 1, sinkA.TransferCount);
        Assert.Equal(initialBEvents, sinkB.TransferCount);

        branch.SelectedPath = ManualBranchPath.B;
        source.Send(new CVStartCFC("path-b"));

        Assert.Equal(initialAEvents + 1, sinkA.TransferCount);
        Assert.Equal(initialBEvents + 1, sinkB.TransferCount);
    }

    [Fact]
    public void ClickingEmbeddedABButtonsChangesSelectedPath()
    {
        ManualBranchNode branch = new();
        branch.Create();
        MethodInfo? onMouseClick = typeof(ManualBranchNode).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onMouseClick);
        int selectorY = branch.TitleHeight + 35;

        onMouseClick.Invoke(branch, new object[] { new STNodeMouseEventArgs(STMouseButtons.Left, 1, branch.Width - 15, selectorY, 0) });
        Assert.Equal(ManualBranchPath.B, branch.SelectedPath);

        onMouseClick.Invoke(branch, new object[] { new STNodeMouseEventArgs(STMouseButtons.Left, 1, 15, selectorY, 0) });
        Assert.Equal(ManualBranchPath.A, branch.SelectedPath);
    }

    [Fact]
    public void MergeIgnoresEmptySignals()
    {
        FlowSourceNode sourceA = new();
        FlowSourceNode sourceB = new();
        AnyPathMergeNode merge = new();
        FlowSinkNode sink = new();
        Create(sourceA, sourceB, merge, sink);
        Connect(sourceA.Output, merge.GetAllInputOptions()[0]);
        Connect(sourceB.Output, merge.GetAllInputOptions()[1]);
        Connect(merge.GetAllOutputOptions()[0], sink.Input);

        sourceA.Send(null);
        Assert.Empty(sink.Received);

        CVStartCFC result = new("result");
        sourceB.Send(result);

        Assert.Equal(new[] { result }, sink.Received);
    }

    [Fact]
    public void SelectedPathRoundTripsThroughNodeState()
    {
        ManualBranchNode original = new();
        original.Create();
        original.SelectedPath = ManualBranchPath.B;
        Dictionary<string, byte[]> state = ParseState(original.GetSaveData());

        ManualBranchNode restored = new();
        restored.Create();
        restored.OnLoadNode(state);

        Assert.Equal(ManualBranchPath.B, restored.SelectedPath);
        Assert.Equal(new[] { "OUT_A", "OUT_B [ON]" }, restored.GetAllOutputOptions().Select(option => option.Text));
    }

    [Fact]
    public void LoadingNodeStateRepositionsPortsWithRestoredNode()
    {
        ManualBranchNode original = new();
        original.Create();
        original.Location = new System.Drawing.Point(420, 260);
        Dictionary<string, byte[]> state = ParseState(original.GetSaveData());

        ManualBranchNode restored = new();
        restored.Create();
        restored.OnLoadNode(state);

        STNodeOption input = restored.GetAllInputOptions()[0];
        STNodeOption outputA = restored.GetAllOutputOptions()[0];
        Assert.Equal(original.Location, restored.Location);
        Assert.Equal(restored.Left - input.DotSize / 2, input.DotLeft);
        Assert.Equal(restored.Right - outputA.DotSize / 2, outputA.DotLeft);
    }

    private static void Create(params STNode[] nodes)
    {
        foreach (STNode node in nodes) node.Create();
    }

    private static void Connect(STNodeOption output, STNodeOption input)
    {
        Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(input, isOwnerOfOwner: false));
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
            byte[] value = new byte[valueLength];
            Array.Copy(data, position, value, 0, valueLength);
            position += valueLength;
            state[key] = value;
        }
        return state;
    }

    private sealed class FlowSourceNode : STNode
    {
        public STNodeOption Output { get; private set; } = STNodeOption.Empty;

        public void Send(CVStartCFC? data) => Output.TransferData(data);

        protected override void OnCreate()
        {
            base.OnCreate();
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
        }
    }

    private sealed class FlowSinkNode : STNode
    {
        public STNodeOption Input { get; private set; } = STNodeOption.Empty;
        public List<CVStartCFC> Received { get; } = new();
        public int TransferCount { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
            Input.DataTransfer += (_, e) =>
            {
                TransferCount++;
                if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data is CVStartCFC start)
                {
                    Received.Add(start);
                }
            };
        }
    }
}
