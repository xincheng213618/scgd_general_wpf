using FlowEngineLib.Base;
using FlowEngineLib.Logical;
using ST.Library.UI.NodeEditor;
using System.Text;
using Xunit;

namespace ColorVision.UI.Tests;

public class ConventionalFlowNodeTests
{
    private static readonly string[] SingleInputPort = ["IN"];
    private static readonly string[] ConditionOutputPorts = ["OUT_TRUE", "OUT_FALSE", "OUT_ERROR"];
    private static readonly string[] RerouteOutputPort = ["OUT"];

    [Fact]
    public void NodesExposeFixedFlowPorts()
    {
        ConditionBranchNode condition = new();
        FlowTerminationNode termination = new();
        FlowRerouteNode reroute = new();
        Create(condition, termination, reroute);

        Assert.Equal("ConditionBranch", condition.NodeType);
        Assert.Equal(SingleInputPort, condition.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(ConditionOutputPorts, condition.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal("FlowTermination", termination.NodeType);
        Assert.Equal(SingleInputPort, termination.GetAllInputOptions().Select(option => option.Text));
        Assert.Empty(termination.GetAllOutputOptions());
        Assert.Equal("FlowReroute", reroute.NodeType);
        Assert.Equal(SingleInputPort, reroute.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(RerouteOutputPort, reroute.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal(96, reroute.Width);
    }

    [Fact]
    public void StatusConditionTriggersOnlySelectedOutput()
    {
        FlowSourceNode source = new();
        ConditionBranchNode condition = new() { ExpectedStatus = StatusTypeEnum.Failed };
        FlowSinkNode trueSink = new();
        FlowSinkNode falseSink = new();
        FlowSinkNode errorSink = new();
        Create(source, condition, trueSink, falseSink, errorSink);
        Connect(source.Output, condition.GetAllInputOptions()[0]);
        Connect(condition.GetAllOutputOptions()[0], trueSink.Input);
        Connect(condition.GetAllOutputOptions()[1], falseSink.Input);
        Connect(condition.GetAllOutputOptions()[2], errorSink.Input);
        int trueBaseline = trueSink.TransferCount;
        int falseBaseline = falseSink.TransferCount;
        int errorBaseline = errorSink.TransferCount;

        CVStartCFC failed = new("failed");
        failed.SetStatusType(StatusTypeEnum.Failed);
        source.Send(failed);

        Assert.Equal(trueBaseline + 1, trueSink.TransferCount);
        Assert.Equal(falseBaseline, falseSink.TransferCount);
        Assert.Equal(errorBaseline, errorSink.TransferCount);
        Assert.Same(failed, trueSink.Received[^1]);

        CVStartCFC running = new("running");
        source.Send(running);

        Assert.Equal(trueBaseline + 1, trueSink.TransferCount);
        Assert.Equal(falseBaseline + 1, falseSink.TransferCount);
        Assert.Equal(errorBaseline, errorSink.TransferCount);
        Assert.Same(running, falseSink.Received[^1]);
    }

    [Fact]
    public void DataConditionSupportsNumericComparisonAndErrorOutput()
    {
        FlowSourceNode source = new();
        ConditionBranchNode condition = new()
        {
            ConditionSource = FlowConditionSource.DataField,
            DataKey = "Score",
            ConditionOperator = FlowConditionOperator.GreaterThan,
            CompareValue = "10"
        };
        FlowSinkNode trueSink = new();
        FlowSinkNode falseSink = new();
        FlowSinkNode errorSink = new();
        Create(source, condition, trueSink, falseSink, errorSink);
        Connect(source.Output, condition.GetAllInputOptions()[0]);
        Connect(condition.GetAllOutputOptions()[0], trueSink.Input);
        Connect(condition.GetAllOutputOptions()[1], falseSink.Input);
        Connect(condition.GetAllOutputOptions()[2], errorSink.Input);
        int trueBaseline = trueSink.TransferCount;
        int falseBaseline = falseSink.TransferCount;
        int errorBaseline = errorSink.TransferCount;

        CVStartCFC highScore = new("high");
        highScore.Data["Score"] = 12.5;
        source.Send(highScore);
        Assert.Equal(trueBaseline + 1, trueSink.TransferCount);

        CVStartCFC lowScore = new("low");
        lowScore.Data["Score"] = 5;
        source.Send(lowScore);
        Assert.Equal(falseBaseline + 1, falseSink.TransferCount);

        condition.CompareValue = "not-a-number";
        CVStartCFC invalidComparison = new("invalid");
        invalidComparison.Data["Score"] = 12.5;
        source.Send(invalidComparison);

        Assert.Equal(errorBaseline + 1, errorSink.TransferCount);
        Assert.Same(invalidComparison, errorSink.Received[^1]);
        Assert.Contains("不是有效数字", Assert.IsType<string>(invalidComparison.Data["ConditionError"]));
    }

    [Fact]
    public void ConditionConfigurationRoundTripsThroughNodeState()
    {
        ConditionBranchNode original = new();
        original.Create();
        original.ConditionSource = FlowConditionSource.DataField;
        original.DataKey = "MasterResultType";
        original.ConditionOperator = FlowConditionOperator.NotEqual;
        original.CompareValue = "7";
        original.ExpectedStatus = StatusTypeEnum.OverTime;
        Dictionary<string, byte[]> state = ParseState(original.GetSaveData());

        ConditionBranchNode restored = new();
        restored.Create();
        restored.OnLoadNode(state);

        Assert.Equal(FlowConditionSource.DataField, restored.ConditionSource);
        Assert.Equal("MasterResultType", restored.DataKey);
        Assert.Equal(FlowConditionOperator.NotEqual, restored.ConditionOperator);
        Assert.Equal("7", restored.CompareValue);
        Assert.Equal(StatusTypeEnum.OverTime, restored.ExpectedStatus);
    }

    [Theory]
    [InlineData(FlowTerminationStatus.Completed, StatusTypeEnum.Completed)]
    [InlineData(FlowTerminationStatus.Failed, StatusTypeEnum.Failed)]
    [InlineData(FlowTerminationStatus.Canceled, StatusTypeEnum.Canceled)]
    [InlineData(FlowTerminationStatus.OverTime, StatusTypeEnum.OverTime)]
    public void TerminationMapsStatusAndFinishesExactlyOnce(FlowTerminationStatus terminationStatus, StatusTypeEnum expectedStatus)
    {
        FlowSourceNode source = new();
        FlowTerminationNode termination = new()
        {
            TerminationStatus = terminationStatus,
            Reason = "test reason"
        };
        Create(source, termination);
        Connect(source.Output, termination.GetAllInputOptions()[0]);
        CVStartCFC start = new("terminate");

        source.Send(start);

        Assert.Equal(expectedStatus, start.FlowStatus);
        Assert.True(start.IsDel);
        Assert.Equal("test reason", start.Data["TerminationReason"]);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(start.Data["TerminationNodeId"])));
        DateTime firstEndTime = start.EndTime;

        termination.TerminationStatus = FlowTerminationStatus.Completed;
        termination.Reason = "late duplicate";
        source.Send(start);

        Assert.Equal(expectedStatus, start.FlowStatus);
        Assert.Equal("test reason", start.Data["TerminationReason"]);
        Assert.Equal(firstEndTime, start.EndTime);
    }

    [Fact]
    public void RerouteTransparentlyForwardsSignalAndDisconnectState()
    {
        FlowSourceNode source = new();
        FlowRerouteNode reroute = new();
        FlowSinkNode sink = new();
        Create(source, reroute, sink);
        Connect(source.Output, reroute.GetAllInputOptions()[0]);
        Connect(reroute.GetAllOutputOptions()[0], sink.Input);
        int baseline = sink.TransferCount;
        CVStartCFC start = new("reroute");

        source.Send(start);

        Assert.Equal(baseline + 1, sink.TransferCount);
        Assert.Same(start, sink.Received[^1]);

        source.Send(null);

        Assert.Equal(baseline + 2, sink.TransferCount);
        Assert.Null(sink.Received[^1]);
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
        public List<CVStartCFC?> Received { get; } = new();
        public int TransferCount { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
            Input.DataTransfer += (_, e) =>
            {
                TransferCount++;
                Received.Add(e.Status == ConnectionStatus.Connected ? e.TargetOption.Data as CVStartCFC : null);
            };
        }
    }
}
