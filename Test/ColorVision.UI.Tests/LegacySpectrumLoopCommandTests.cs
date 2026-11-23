using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.Spectrum;
using FlowEngineLib.Spectum;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Threading.Channels;

namespace ColorVision.UI.Tests;

public sealed class LegacySpectrumLoopCommandTests
{
    [Fact]
    public Task MixedLoopSendsEachStepCommandAndAcceptsItsReply() => WpfTestHost.Invoke(async () =>
    {
        TimeSpan deadline = TimeSpan.FromSeconds(10);
        using var graph = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        var loop = new LoopNode();
        var spectrum = new SpectrumLoopNode();
        var next = new LoopNextNode();
        var end = new RuntimeTestEndNode();
        foreach (STNode node in new STNode[] { start, loop, spectrum, next, end })
        {
            node.Create();
            graph.Nodes.Add(node);
        }
        loop.EndVal = 2;
        spectrum.DeviceCode = "offline-spectrum-loop-device";
        spectrum.Params =
        [
            new SpectrumNodeProperty { Cmd = SPCommCmdType.校零 },
            new SpectrumNodeProperty { Cmd = SPCommCmdType.检测, Data = new SpectrumParamData { IntegralTime = 73 } },
        ];
        Connect(start, loop);
        Connect(loop, spectrum);
        Connect(spectrum, next);
        Connect(next, end);
        Assert.Equal(ConnectionStatus.Connected, start.GetAllOutputOptions().First(option => option.DataType == typeof(CVLoopCFC))
            .ConnectOption(Assert.Single(loop.GetAllInputOptions(), option => option.DataType == typeof(CVLoopCFC)), false));
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(next.GetAllOutputOptions(), option => option.DataType == typeof(CVLoopCFC))
            .ConnectOption(end.GetAllInputOptions().First(option => option.DataType == typeof(CVLoopCFC)), false));

        var requests = Channel.CreateUnbounded<MQActionEvent>();
        start.OnPublish = action => requests.Writer.TryWrite(action);
        var finished = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new FlowEngineControl(graph, false, new FlowNodeManager());
        engine.Finished += (_, args) => finished.TrySetResult(args);

        Assert.True(engine.TryStartNode(start.NodeName, "offline-spectrum-loop"));
        CVMQTTRequest dark = await ReadRequestAsync();
        Assert.Equal(("InitDark", 1), (dark.EventName, dark.ZIndex));
        Assert.Null(dark.Data);
        Assert.True(spectrum.DoServerStatusRecv(Reply(dark)));

        CVMQTTRequest sample = await ReadRequestAsync();
        Assert.Equal(("GetData", 2), (sample.EventName, sample.ZIndex));
        Assert.Equal(73f, Assert.IsType<JObject>(sample.Data).Value<float>("IntegralTime"));
        Assert.True(spectrum.DoServerStatusRecv(Reply(sample, new JObject { ["MasterId"] = 23, ["MasterResultType"] = 300 })));
        Assert.Equal(StatusTypeEnum.Completed, (await finished.Task.WaitAsync(deadline)).Status);
        Assert.Equal(2, start.PublishCount);

        async Task<CVMQTTRequest> ReadRequestAsync()
        {
            MQActionEvent action = await requests.Reader.ReadAsync().AsTask().WaitAsync(deadline);
            return JsonConvert.DeserializeObject<CVMQTTRequest>(action.Message)!;
        }

        CVBaseDataFlowResp Reply(CVMQTTRequest request, JObject? data = null) => new()
        {
            Code = 0, MsgID = request.MsgID, DeviceNodeCode = request.DeviceNodeCode, DeviceCode = request.DeviceCode,
            EventName = request.EventName, SerialNumber = request.SerialNumber, ZIndex = request.ZIndex,
            ReceivedTopic = spectrum.GetRecvTopic(), Data = data
        };
    });

    private static void Connect(STNode upstream, STNode downstream) =>
        Assert.Equal(ConnectionStatus.Connected,
            Assert.Single(upstream.GetAllOutputOptions(), option => option.DataType == typeof(CVStartCFC))
                .ConnectOption(Assert.Single(downstream.GetAllInputOptions(), option => option.DataType == typeof(CVStartCFC)), false));
}
