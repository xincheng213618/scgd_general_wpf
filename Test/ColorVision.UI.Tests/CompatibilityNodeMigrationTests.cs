using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CompatibilityNodeMigrationTests
{
    // These fixtures were saved by the compiled FlowEngineLib nodes before migration.
    // They contain synthetic template names/ranges and a chain of 30 nodes, never user data.
    private static byte[] ReadCanvas() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "Flow", "EditorNodes.before.stn"));
    private static JArray ReadContracts() => JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Flow", "EditorNodes.before.json")));

    [Fact]
    public void MigratedEditorNodesPreservePersistedPropertiesConnectionsAndServiceRequests() => StaTest.Run(() =>
    {
        byte[] before = ReadCanvas();
        JArray contracts = ReadContracts();
        using var container = new CVNodeContainer();
        container.LoadCanvas(before);
        Assert.Equal(30, container.Nodes.Count);
        var nodes = container.Nodes.Cast<STNode>().ToArray();
        for (int i = 0; i < nodes.Length; i++)
        {
            Type type = nodes[i].GetType();
            Assert.Same(typeof(DeviceCamera).Assembly, type.Assembly);
            Assert.Equal((string?)contracts[i]["type"], type.FullName);
            Assert.Null(typeof(CVBaseServerNode).Assembly.GetType(type.FullName!));
            var action = new CVStartCFC("SYNTHETIC-EDITOR-MIGRATION");
            object? request = type.GetMethod("getBaseEventData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(nodes[i], [action]);
            Assert.True(JToken.DeepEquals(contracts[i]["request"], request == null ? JValue.CreateNull() : JToken.FromObject(request)), type.FullName);
            if (i + 1 < nodes.Length)
                Assert.Same(nodes[i + 1].GetAllInputOptions()[0], Assert.Single(nodes[i].GetAllOutputOptions()[0].ConnectedOption));
        }

        byte[] saved = container.GetCanvasData();
        AssertPropertiesAndConnections(before, saved);
        using var editor = new STNodeEditor();
        editor.LoadCanvas(saved);
        AssertPropertiesAndConnections(saved, editor.GetCanvasData(), editorLayout: true);
        NeutralCanvas neutral = StnV1NeutralCodec.Decode(saved, new());
        Assert.Equal(30, neutral.Nodes.Count);
        Assert.Equal(29, neutral.Connections.Count);
        Assert.All(neutral.Nodes, node => Assert.Equal("FlowEngineLib.dll|" + node.Schema.NodeType.FullName, node.ModelKey));
        container.LoadCanvas(StnV1NeutralCodec.Encode(neutral, new()));
        AssertPropertiesAndConnections(saved, container.GetCanvasData());
    });

    [Fact]
    public void MigrationPreservesMenuVisibilityAndUsesPropertyEditorsDirectly() => StaTest.Run(() =>
    {
        using var container = new CVNodeContainer();
        container.LoadCanvas(ReadCanvas());
        JArray contracts = ReadContracts();
        for (int i = 0; i < container.Nodes.Count; i++)
        {
            Type type = container.Nodes[i].GetType();
            Assert.Equal((string?)contracts[i]["menu"], type.GetCustomAttribute<STNodeAttribute>()?.Path);
            PropertyInfo[] edited = type.GetProperties().Where(p => p.DeclaringType == type && p.GetCustomAttribute<PropertyEditorTypeAttribute>() != null).ToArray();
            Assert.Contains(edited, p => p.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType?.Assembly == typeof(DeviceCamera).Assembly);
            Assert.All(edited, p => Assert.DoesNotContain("FlowEngineLib.PropertyEditor", p.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!.FullName!));
        }
        Assert.Null(typeof(CVBaseServerNode).Assembly.GetType("FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute"));
        Assert.Null(typeof(CVBaseServerNode).Assembly.GetType("FlowEngineLib.PropertyEditor.FlowNodePropertyEditorSelector"));
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.Start.BaseStartNode).Assembly);
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.SMUBaseNode).Assembly);
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.Node.Algorithm.AlgDataLoadNode2).Assembly);
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.Node.Algorithm.AlgDataConvertNode).Assembly);
    });

    private static void AssertPropertiesAndConnections(byte[] before, byte[] after, bool editorLayout = false)
    {
        var original = ReadRaw(before);
        var saved = ReadRaw(after);
        Assert.Equal(original.View, saved.View);
        Assert.Equal(original.Connections, saved.Connections);
        Assert.Equal(original.Nodes.Length, saved.Nodes.Length);
        for (int i = 0; i < original.Nodes.Length; i++)
        {
            using var oldNode = new BinaryReader(new MemoryStream(original.Nodes[i]));
            using var newNode = new BinaryReader(new MemoryStream(saved.Nodes[i]));
            Assert.Equal(Encoding.UTF8.GetString(oldNode.ReadBytes(oldNode.ReadByte())), Encoding.UTF8.GetString(newNode.ReadBytes(newNode.ReadByte())));
            oldNode.ReadBytes(oldNode.ReadByte()); newNode.ReadBytes(newNode.ReadByte());
            var oldProperties = ReadProperties(oldNode);
            var newProperties = ReadProperties(newNode);
            Assert.Equal(oldProperties.Keys, newProperties.Keys);
            foreach (var property in oldProperties)
            {
                // STNode.BuildSize measures AutoSize nodes when an editor owns them.
                // Runtime canvases have no graphics context; their stored size is not a business parameter.
                if (editorLayout && oldProperties["AutoSize"][0] == 1 && property.Key is "Width" or "Height")
                    continue;
                Assert.True(property.Value.SequenceEqual(newProperties[property.Key]), $"Node {i}: {property.Key}");
            }
        }
    }

    private static Dictionary<string, byte[]> ReadProperties(BinaryReader reader)
    {
        var properties = new Dictionary<string, byte[]>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
            properties.Add(Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())), reader.ReadBytes(reader.ReadInt32()));
        return properties;
    }

    private static (float[] View, byte[][] Nodes, long[] Connections) ReadRaw(byte[] canvas)
    {
        using var reader = new BinaryReader(new MemoryStream(FlowPackageStnValidator.ValidateAndDecompress(canvas)));
        float[] view = [reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()];
        byte[][] nodes = Enumerable.Range(0, reader.ReadInt32()).Select(_ => reader.ReadBytes(reader.ReadInt32())).ToArray();
        long[] connections = Enumerable.Range(0, reader.ReadInt32()).Select(_ => reader.ReadInt64()).ToArray();
        return (view, nodes, connections);
    }
}
