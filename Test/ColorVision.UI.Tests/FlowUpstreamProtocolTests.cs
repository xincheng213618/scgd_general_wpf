using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Spectrum;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

// Protect the wire contract shared with separately delivered algorithm/device services.
public sealed class FlowUpstreamProtocolTests
{
    [Fact]
    public void CalcEqeKeepsBothInputResultsAndTheLegacySmuField() => StaTest.Run(() =>
    {
        var spectrum = new CVStartCFC("eqe-protocol");
        using var spectrumResources = spectrum.RuntimeResources;
        spectrum.Data["MasterId"] = 301;
        spectrum.Data["MasterResultType"] = 300;
        var electrical = new CVStartCFC("eqe-protocol");
        using var electricalResources = electrical.RuntimeResources;
        electrical.Data["MasterId"] = 201;
        electrical.Data["MasterResultType"] = 200;
        var node = new CalcEqeProbe();
        node.Create();

        var request = node.Build(spectrum, electrical);
        var data = Assert.IsType<CalcEQEParam>(request.Data);
        var json = JObject.FromObject(data);
        Assert.Equal("CalcEQE", request.EventName);
        Assert.Equal(301, data.MasterId);
        Assert.Equal(300, data.MasterResultType);
        Assert.Equal(201, json["SMUData"]!["MasterId"]!.Value<int>());
        Assert.Equal(200, json["SMUData"]!["MasterResultType"]!.Value<int>());
        Assert.Equal(201, json["SMU_MasterId"]!.Value<int>());
        var legacy = JObject.Parse("{\"SMU_MasterId\":202}").ToObject<CalcEQEParam>()!;
        Assert.Equal(202, legacy.SMUData.MasterId);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RemoteEqeUsesThePrecedingResultWithoutChangingMeasuredInput(bool hasSmu, bool custom) => StaTest.Run(() =>
    {
        var action = new CVStartCFC("remote-eqe-protocol");
        using var resources = action.RuntimeResources;
        action.Data["MasterId"] = 456;
        action.Data["MasterResultType"] = 200;
        var upstream = new SMUResultData(SMUChannelType.B, 4.5, 25, 123, 200);
        if (hasSmu) action.Data["SMUResult"] = upstream;
        var node = new RemoteEqeProbe();
        node.Create();
        node.IsCustomVI = custom;
        node.Voltage = 3.25f;
        node.Current = 12.5f;

        var data = node.Build(action);
        Assert.Equal(456, data.SMUData.MasterId);
        Assert.Equal(200, data.SMUData.MasterResultType);
        Assert.Equal(custom ? 3.25 : hasSmu ? 4.5 : 0, data.SMUData.V);
        Assert.Equal(custom ? 12.5 : hasSmu ? 25 : 0, data.SMUData.I);
        Assert.Equal(123, upstream.MasterId);
        Assert.Equal(4.5, upstream.V);
        Assert.Equal(25, upstream.I);
    });

    [Theory]
    [InlineData(LightingCommand.控制, true, "PM_LightTurnOn")]
    [InlineData(LightingCommand.控制, false, "PM_LightTurnOff")]
    [InlineData(LightingCommand.查询, false, "PM_GetValue")]
    public void LightingBuildsTheUpstreamCommandAndStringChannel(LightingCommand command, bool turnOn, string eventName) => StaTest.Run(() =>
    {
        var action = new CVStartCFC("lighting-protocol");
        using var resources = action.RuntimeResources;
        var node = new LightingProbe();
        node.Create();
        node.Command = command;
        node.TurnOn = turnOn;
        node.Channel = LightingChannel.B;

        var request = node.Build(action);
        Assert.Equal(eventName, request.EventName);
        Assert.Equal("B", JObject.FromObject(request.Data)["Channel"]!.Value<string>());
        Assert.Equal("LightingControl", node.NodeType);
        Assert.Equal("SVR.Lighting.Default", node.NodeName);
        Assert.Equal("DEV.LightingControl.Default", node.DeviceCode);
    });

    [Fact]
    public void LightingKeepsTheUpstreamModelAndConfigurationAcrossCanvasSave() => StaTest.Run(() =>
    {
        using var container = new CVNodeContainer();
        var node = new LightingSensorNode();
        node.Create();
        Assert.Equal(LightingCommand.控制, node.Command);
        Assert.Equal(LightingChannel.A, node.Channel);
        Assert.True(node.TurnOn);
        node.Command = LightingCommand.查询;
        node.Channel = LightingChannel.B;
        node.TurnOn = false;
        node.DeviceCode = "SYNTHETIC.LIGHT.B";
        container.Nodes.Add(node);
        string nodeId = node.NodeID;

        container.LoadCanvas(container.GetCanvasData());
        var restored = Assert.IsType<LightingSensorNode>(Assert.Single(container.Nodes.Cast<STNode>()));
        Assert.Equal("FlowEngineLib.dll|FlowEngineLib.LightingSensorNode", STNodeTypeRegistry.GetModelByType(restored.GetType()));
        Assert.Equal(nodeId, restored.NodeID);
        Assert.Equal(LightingCommand.查询, restored.Command);
        Assert.Equal(LightingChannel.B, restored.Channel);
        Assert.False(restored.TurnOn);
        Assert.Equal("SYNTHETIC.LIGHT.B", restored.DeviceCode);
    });

    private sealed class CalcEqeProbe : AlgorithmEQENode
    {
        internal CVBaseEventObj Build(CVStartCFC spectrum, CVStartCFC electrical)
        {
            masterInput[0] = spectrum;
            masterInput[1] = electrical;
            return getBaseEvent(spectrum);
        }
    }

    private sealed class RemoteEqeProbe : SpectrumEQENode
    {
        public override bool RequiresRemoteService => true;
        internal SpectrumEQEParamData Build(CVStartCFC action) => (SpectrumEQEParamData)getBaseEventData(action);
    }

    private sealed class LightingProbe : LightingSensorNode
    {
        internal CVBaseEventObj Build(CVStartCFC action) => getBaseEvent(action);
    }
}
