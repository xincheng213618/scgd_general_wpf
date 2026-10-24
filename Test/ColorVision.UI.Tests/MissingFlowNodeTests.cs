using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

// Protects unavailable plugin/removed-node loading, opaque data preservation and
// the execution boundary. Unknown business properties must never be interpreted.
public sealed class MissingFlowNodeTests
{
    private const string MissingModel = "Absent.Plugin.dll|Absent.Plugin.InspectionNode";
    private const string MissingType = "33333333-4444-5555-6666-777777777777";
    private const string LegacyConnectionsKey = "__STNode.LegacyConnections";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingNodePreservesIdentityOpaqueDataAndConnectionsAcrossColdReload(bool legacy) => StaTest.Run(() =>
    {
        byte[] original = CreateGraph(legacy);
        using var first = new CVNodeContainer();
        first.LoadCanvas(original);
        STMissingNode missing = Assert.Single(first.Nodes.OfType<STMissingNode>());
        Assert.Equal(MissingModel, missing.OriginalModelKey);
        Assert.Equal(MissingType, missing.OriginalTypeKey);
        Assert.Equal("原始检查", missing.Title);
        Assert.Contains("原模板-Ω", missing.SavedContent);
        Assert.Contains("Base64:", missing.SavedContent);
        Assert.Equal(!legacy, missing.HasSavedPortLayout);
        Assert.Equal(legacy ? 0 : 3, STNodeCanvasWriter.GetConnections(first.Nodes.Cast<STNode>()).Length);
        Guid originalId = missing.Guid;
        missing.Left += 70;
        missing.Top += 30;

        byte[] saved = first.GetCanvasData();
        using var second = new CVNodeContainer();
        second.LoadCanvas(saved);
        STMissingNode restored = Assert.Single(second.Nodes.OfType<STMissingNode>());
        Assert.Equal(originalId, restored.Guid);
        Assert.Equal(missing.Left, restored.Left);
        Assert.Equal(missing.Top, restored.Top);
        var before = STNodeSavedState.Parse(GetPayloads(original)[1]);
        var after = STNodeSavedState.Parse(restored.GetSaveData());
        Assert.Equal(before.ModelKey, after.ModelKey);
        Assert.Equal(before.TypeKey, after.TypeKey);
        foreach (var property in before.Properties.Where(property => property.Key is not "Left" and not "Top"))
            Assert.Equal(property.Value, after.Properties[property.Key]);
        Assert.Equal(legacy ? 0 : 3, STNodeCanvasWriter.GetConnections(second.Nodes.Cast<STNode>()).Length);
        if (legacy)
            Assert.Contains(GetPayloads(second.GetCanvasData()), payload => STNodeSavedState.Parse(payload).Properties.ContainsKey(LegacyConnectionsKey));

        // Simulate availability of the original plugin by restoring its registered identity.
        byte[] recovered = RestoreFixtureIdentity(second.GetCanvasData());
        using var recoveredContainer = new CVNodeContainer();
        recoveredContainer.LoadCanvas(recovered);
        MissingNodeFixtureNode fixture = Assert.Single(recoveredContainer.Nodes.OfType<MissingNodeFixtureNode>(), node => node.Guid == originalId);
        Assert.Equal("原模板-Ω", fixture.TemplateName);
        Assert.Equal(3, STNodeCanvasWriter.GetConnections(recoveredContainer.Nodes.Cast<STNode>()).Length);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditorClipboardAndCatalogKeepMissingNodesButExecutionCodecRejectsThem(bool legacy) => WpfTestHost.Invoke(() =>
    {
        using var editor = new STNodeEditor();
        editor.LoadCanvas(CreateGraph(legacy));
        STMissingNode missing = Assert.Single(editor.Nodes.OfType<STMissingNode>());
        byte[] selection = editor.GetNodesData(new[] { missing });
        STMissingNode copy = Assert.IsType<STMissingNode>(Assert.Single(editor.ImportSelectionData(selection)));
        Assert.NotEqual(missing.Guid, copy.Guid);
        Assert.Equal(missing.OriginalModelKey, copy.OriginalModelKey);
        Assert.Equal(STNodeSavedState.Parse(missing.GetSaveData()).Properties["Vendor.Binary"],
            STNodeSavedState.Parse(copy.GetSaveData()).Properties["Vendor.Binary"]);
        if (!legacy)
        {
            int received = 0;
            copy.GetAllInputOptions()[0].DataTransfer += (_, _) => received++;
            copy.GetAllInputOptions()[0].ReceiveData("ignored");
            Assert.Equal(0, received);
        }

        var catalog = new FlowCanvasCatalogBuilder().Build(editor.GetCanvasData());
        Assert.Equal(editor.Nodes.Count, catalog.SemanticDocument.Nodes.Count);
        Assert.Contains(catalog.SearchDocuments, node => node.NodeTypeKey.Contains("InspectionNode", StringComparison.Ordinal));
        FlowCompilationException rejected = Assert.Throws<FlowCompilationException>(() => StnV1NeutralCodec.Decode(editor.GetCanvasData(), new()));
        Assert.Equal(FlowCompilationError.UnknownNodeType, rejected.Error);

        if (legacy)
        {
            editor.Nodes.Remove(missing);
            STNode source = Assert.Single(editor.Nodes.Cast<STNode>(), node => node.Title == "来源");
            STNode sink = Assert.Single(editor.Nodes.Cast<STNode>(), node => node.Title == "后续");
            Assert.Equal(ConnectionStatus.Connected, source.GetAllOutputOptions()[0].ConnectOption(sink.GetAllInputOptions()[0]));
            editor.SetActiveNode(source); // Move it after the legacy copy in saved drawing order.
            using var recovered = new CVNodeContainer();
            byte[] recoveredData = RestoreFixtureIdentity(editor.GetCanvasData());
            recovered.LoadCanvas(recoveredData);
            Assert.Single(new FlowCanvasCatalogBuilder().Build(recoveredData).SemanticDocument.Edges);
            var connection = Assert.Single(STNodeCanvasWriter.GetConnections(recovered.Nodes.Cast<STNode>()));
            Assert.Equal("来源", connection.Output.Owner.Title);
            Assert.Equal("后续", connection.Input.Owner.Title);
        }

        using var inspector = new FlowEditorCanvas(new FlowEditorSettings());
        inspector.NodeEditor.LoadCanvas(CreateGraph(false));
        STMissingNode inspected = Assert.Single(inspector.NodeEditor.Nodes.OfType<STMissingNode>());
        inspected.SetSelected(true, false);
        inspector.NodeEditor.SetActiveNode(inspected);
        inspector.RefreshNodePropertyPanel();
        StackPanel panel = inspector.NodePropertyPanel;
        TextBox content = Assert.Single(panel.Children.OfType<TextBox>());
        Assert.True(content.IsReadOnly);
        Assert.Contains("原模板-Ω", content.Text);
    });

    [Fact]
    public void MissingNodeBlocksBothEngineAndDirectStartUntilRemoved() => StaTest.Run(() =>
    {
        var start = new HeadlessTestStartNode();
        var end = new CVEndNode();
        var fixture = new MissingNodeFixtureNode();
        fixture.Create();
        var missingPayload = STNodeSavedState.Parse(fixture.GetSaveData());
        using var container = new CVNodeContainer();
        start.Create(); end.Create();
        container.Nodes.Add(start); container.Nodes.Add(end);
        start.m_op_start.ConnectOption(end.m_in_start, isOwnerOfOwner: false);
        var payloads = GetPayloads(container.GetCanvasData()).Append(STNodeSavedState.Write(MissingModel, MissingType, missingPayload.Properties)).ToArray();
        using var data = new MemoryStream();
        // The known start/end ports keep their original packed connection.
        var raw = ReadCanvas(container.GetCanvasData());
        STNodeCanvasWriter.WriteRaw(data, payloads, raw.Connections, 10, 10, 1);
        container.LoadCanvas(data.ToArray());
        using var control = new FlowEngineControl(container, false, new FlowNodeManager());
        int finished = 0;
        control.Finished += (_, _) => finished++;
        Assert.Contains(MissingModel, control.ExecutionBlockReason);
        Assert.False(control.CanStartNode("HeadlessStart"));
        Assert.False(control.TryStartNode("HeadlessStart", "SYNTHETIC-MISSING"));
        var loadedStart = Assert.Single(container.Nodes.OfType<HeadlessTestStartNode>());
        Assert.False(loadedStart.TryStart("SYNTHETIC-DIRECT"));
        Assert.False(control.IsRunning);
        Assert.Equal(0, finished);
        container.Nodes.Remove(Assert.Single(container.Nodes.OfType<STMissingNode>()));
        Assert.Null(control.ExecutionBlockReason);
        Assert.True(control.TryStartNode("HeadlessStart", "SYNTHETIC-REPAIRED"));
        Assert.Equal(1, finished);
    });

    [Fact]
    public void InvalidKnownNodeAndCorruptMissingPayloadStillRejectWithoutReplacingCanvas() => StaTest.Run(() =>
    {
        using var container = new CVNodeContainer();
        container.LoadCanvas(CreateGraph(false));
        STNode[] original = container.Nodes.Cast<STNode>().ToArray();
        byte[] corrupted = CreateGraph(false);
        corrupted[^1] ^= 0x5a;
        Assert.ThrowsAny<InvalidDataException>(() => container.LoadCanvas(corrupted));
        Assert.Equal(original, container.Nodes.Cast<STNode>());
        var invalidState = STNodeSavedState.Parse(GetPayloads(CreateGraph(false))[1]);
        invalidState.Properties[STNodeSavedState.PortLayoutKey] = Encoding.UTF8.GetBytes("{broken");
        byte[][] payloads = GetPayloads(CreateGraph(false));
        payloads[1] = STNodeSavedState.Write(MissingModel, MissingType, invalidState.Properties);
        using var output = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(output, payloads, [], 10, 10, 1);
        Assert.ThrowsAny<InvalidDataException>(() => container.LoadCanvas(output.ToArray()));
        Assert.Equal(original, container.Nodes.Cast<STNode>());

        var knownInvalid = new InvalidKnownFixtureNode();
        knownInvalid.Create();
        using var invalidKnownData = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(invalidKnownData, [knownInvalid.GetSaveData()], [], 10, 10, 1);
        Assert.ThrowsAny<InvalidDataException>(() => container.LoadCanvas(invalidKnownData.ToArray()));
        Assert.Equal(original, container.Nodes.Cast<STNode>());
    });

    private static byte[] CreateGraph(bool legacy)
    {
        using var container = new CVNodeContainer();
        var source = new MissingNodeFixtureNode { Title = "来源", Left = 20, Top = 70 };
        var missing = new MissingNodeFixtureNode { Title = "原始检查", TemplateName = "原模板-Ω", Left = 300, Top = 70 };
        var sink = new MissingNodeFixtureNode { Title = "后续", Left = 580, Top = 70 };
        foreach (var node in new[] { source, missing, sink }) { node.Create(); container.Nodes.Add(node); }
        Assert.Equal(ConnectionStatus.Connected, source.GetAllOutputOptions()[0].ConnectOption(missing.GetAllInputOptions()[0], false));
        Assert.Equal(ConnectionStatus.Connected, source.GetAllOutputOptions()[0].ConnectOption(missing.GetAllInputOptions()[1], false));
        Assert.Equal(ConnectionStatus.Connected, missing.GetAllOutputOptions()[0].ConnectOption(sink.GetAllInputOptions()[0], false));
        var raw = ReadCanvas(container.GetCanvasData());
        var state = STNodeSavedState.Parse(raw.Payloads[1]);
        if (legacy) state.Properties.Remove(STNodeSavedState.PortLayoutKey);
        raw.Payloads[1] = STNodeSavedState.Write(MissingModel, MissingType, state.Properties);
        using var result = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(result, raw.Payloads, raw.Connections, 10, 10, 1);
        return result.ToArray();
    }

    private static byte[] RestoreFixtureIdentity(byte[] data)
    {
        var raw = ReadCanvas(data);
        for (int i = 0; i < raw.Payloads.Length; i++)
        {
            var state = STNodeSavedState.Parse(raw.Payloads[i]);
            if (state.ModelKey == MissingModel)
                raw.Payloads[i] = STNodeSavedState.Write(STNodeTypeRegistry.GetModelByType(typeof(MissingNodeFixtureNode)),
                    typeof(MissingNodeFixtureNode).GUID.ToString(), state.Properties);
        }
        using var result = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(result, raw.Payloads, raw.Connections, 10, 10, 1);
        return result.ToArray();
    }

    private static byte[][] GetPayloads(byte[] data) => ReadCanvas(data).Payloads;

    private static (byte[][] Payloads, long[] Connections) ReadCanvas(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(FlowPackageStnValidator.ValidateAndDecompress(data)));
        reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle();
        byte[][] payloads = Enumerable.Range(0, reader.ReadInt32()).Select(_ => reader.ReadBytes(reader.ReadInt32())).ToArray();
        long[] connections = Enumerable.Range(0, reader.ReadInt32()).Select(_ => reader.ReadInt64()).ToArray();
        return (payloads, connections);
    }
}

public sealed class MissingNodeFixtureNode : STNode
{
    [STNodeProperty("模板", "模板", true)]
    public string TemplateName { get; set; } = string.Empty;

    protected override void OnCreate()
    {
        InputOptions.Add("IN_IMG", typeof(string), true);
        InputOptions.Add("IN_CAD", typeof(string), true);
        OutputOptions.Add("OUT", typeof(string), false);
    }

    protected override void OnSaveNode(Dictionary<string, byte[]> data) => data["Vendor.Binary"] = [0xff, 0, 1, 0xfe];
}

public sealed class InvalidKnownFixtureNode : STNode
{
    public override void OnLoadNode(Dictionary<string, byte[]> data) => throw new InvalidOperationException("Synthetic known-node load failure.");
}
