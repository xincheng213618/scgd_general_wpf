using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
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
    [Theory]
    [InlineData(typeof(FlowEngineLib.Start.MQTTStartNode))]
    [InlineData(typeof(FlowEngineLib.Start.ManualFlowStartNode))]
    [InlineData(typeof(FlowEngineLib.End.CVEndNode))]
    public void TerminalNodesOnlyExposeTitleAndPreserveStoredConnectionIdentity(Type type) => StaTest.Run(() =>
    {
        var node = (STNode)Activator.CreateInstance(type)!;
        try
        {
            node.Create();
            var legacy = new Dictionary<string, byte[]>
            {
                ["Title"] = Encoding.UTF8.GetBytes("旧节点名称"),
                ["NodeName"] = Encoding.UTF8.GetBytes("legacy-terminal"),
                ["DeviceCode"] = Encoding.UTF8.GetBytes("legacy-device")
            };
            if (node is FlowEngineLib.Start.BaseStartNode)
                legacy["FlowTimeout"] = Encoding.UTF8.GetBytes("300");
            if (node is FlowEngineLib.Start.MQTTStartNode)
            {
                legacy["Server"] = Encoding.UTF8.GetBytes("192.0.2.10");
                legacy["Port"] = Encoding.UTF8.GetBytes("2883");
            }
            node.OnLoadNode(legacy);
            var metadata = FlowNodePropertyMetadataProvider.Instance;
            Assert.Equal(new[] { nameof(STNode.Title) }, type.GetProperties()
                .Where(metadata.IsPropertyManaged).Where(metadata.IsBrowsable).Select(property => property.Name));
            Assert.Null(type.GetProperty("FlowTimeout"));
            Assert.Equal("旧节点名称", node.Title);
            node.Title = "新节点名称";

            using var reader = new BinaryReader(new MemoryStream(node.GetSaveData()));
            reader.ReadBytes(reader.ReadByte()); reader.ReadBytes(reader.ReadByte());
            var saved = ReadProperties(reader);
            Assert.Equal(Encoding.UTF8.GetBytes(node.Title), saved["Title"]);
            Assert.DoesNotContain("FlowTimeout", saved.Keys);
            foreach (string key in legacy.Keys.Where(key => key is not "Title" and not "FlowTimeout"))
                Assert.Equal(legacy[key], saved[key]);
        }
        finally { (node as IDisposable)?.Dispose(); }
    });

    [Theory]
    [InlineData(typeof(FlowEngineLib.Algorithm.AlgorithmNode), 7)]
    [InlineData(typeof(FlowEngineLib.Algorithm.AlgorithmNode), 8)]
    [InlineData(typeof(FlowEngineLib.Algorithm.AlgorithmNode), 9)]
    [InlineData(typeof(FlowEngineLib.Algorithm.AlgorithmNode), 10)]
    [InlineData(typeof(FlowEngineLib.Node.OLED.Algorithm2InNode), 2)]
    [InlineData(typeof(FlowEngineLib.Node.OLED.OLEDJNDCalVasNode), 2)]
    public void RetiredAlgorithmIdsLoadWithoutBecomingOtherAlgorithms(Type type, int value) => StaTest.Run(() =>
    {
        var node = (STNode)Activator.CreateInstance(type)!;
        node.Create();
        node.OnLoadNode(new Dictionary<string, byte[]> { ["Algorithm"] = Encoding.UTF8.GetBytes(value.ToString()) });
        PropertyInfo property = type.GetProperty("Algorithm")!;
        Assert.Equal(value, Convert.ToInt32(property.GetValue(node)));
        Assert.False(Enum.IsDefined(property.PropertyType, value));
        Assert.Equal(11, (int)FlowEngineLib.Algorithm.AlgorithmType.双目融合);
        Assert.Equal(13, (int)FlowEngineLib.Algorithm.AlgorithmType.AA布点);
        Assert.Equal(3, (int)FlowEngineLib.Algorithm.Algorithm2Type.SFR);
        Assert.Equal(6, (int)FlowEngineLib.Algorithm.Algorithm2Type.灯带检测);
        var error = Assert.Throws<TargetInvocationException>(() => node.GetType()
            .GetMethod("getBaseEventData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(node, [new CVStartCFC("SYNTHETIC-RETIRED-ALGORITHM")]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Contains("已退役", error.InnerException!.Message);
    });

    // These fixtures were saved by the compiled FlowEngineLib nodes before migration.
    // Retired nodes are excluded; the remaining 25 node records retain their original bytes.
    // The synthetic chain connections are reindexed and linked across excluded nodes.
    // They contain synthetic template names/ranges, never user data.
    private static byte[] ReadCanvas() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "Flow", "EditorNodes.before.stn"));
    private static JArray ReadContracts() => JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Flow", "EditorNodes.before.json")));

    private static readonly string[] RetiredCameraPoiProperties = ["POITempName", "POIFilterTempName", "POIReviseTempName"];
    private static readonly string[] RetiredCalibrationProperties = [.. RetiredCameraPoiProperties, "OutputTemplateName", "ExpTempName", "IsSaveCIE"];

    [Theory]
    [InlineData(typeof(FlowEngineLib.Node.Camera.CommCameraNode))]
    [InlineData(typeof(FlowEngineLib.CVCameraNode))]
    [InlineData(typeof(FlowEngineLib.LVCameraNode))]
    [InlineData(typeof(FlowEngineLib.Algorithm.CalibrationNode))]
    public void CameraAndCalibrationNodesIgnoreAndDoNotResaveOrSendRetiredSettings(Type type) => StaTest.Run(() =>
    {
        var node = (STNode)Activator.CreateInstance(type)!;
        node.Create();
        string[] retiredProperties = type == typeof(FlowEngineLib.Algorithm.CalibrationNode) ? RetiredCalibrationProperties : RetiredCameraPoiProperties;
        var legacy = retiredProperties.ToDictionary(key => key, key => Encoding.UTF8.GetBytes("missing-" + key));
        if (type == typeof(FlowEngineLib.Algorithm.CalibrationNode)) legacy["IsSaveCIE"] = Encoding.UTF8.GetBytes(bool.FalseString);
        node.OnLoadNode(legacy);
        foreach (string key in retiredProperties)
            Assert.Null(type.GetProperty(key));
        using var reader = new BinaryReader(new MemoryStream(node.GetSaveData()));
        reader.ReadBytes(reader.ReadByte()); reader.ReadBytes(reader.ReadByte());
        var saved = ReadProperties(reader);
        foreach (string key in retiredProperties)
            Assert.DoesNotContain(key, saved.Keys);
        object request = type.GetMethod("getBaseEventData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(node, [new CVStartCFC("SYNTHETIC-CAMERA-WITHOUT-POI")])!;
        JObject payload = JObject.FromObject(request);
        Assert.Null(payload.Property("POIParam"));
        if (request is FlowEngineLib.Algorithm.CalibrationData calibration)
        {
            Assert.Null(payload.Property("POI_MasterId"));
            Assert.Null(payload.Property("ExpTemplateParam"));
            Assert.True(calibration.IsSaveCIE);
        }
    });

    [Fact]
    public void PoiNodeAlwaysEnablesCctWaveWhenLoadingAndSavingLegacyFalse() => StaTest.Run(() =>
    {
        var node = new FlowEngineLib.POINode();
        node.Create();
        node.OnLoadNode(new Dictionary<string, byte[]> { [nameof(node.IsCCTWave)] = Encoding.UTF8.GetBytes("False") });
        Assert.True(node.IsCCTWave);
        node.IsCCTWave = false;
        Assert.True(node.IsCCTWave);
        PropertyInfo property = typeof(FlowEngineLib.POINode).GetProperty(nameof(node.IsCCTWave))!;
        Assert.True(property.GetCustomAttribute<ReadOnlyAttribute>()!.IsReadOnly);
        Assert.True(property.GetCustomAttribute<STNodePropertyAttribute>()!.IsReadOnly);
        var request = (FlowEngineLib.POIDataParam)typeof(FlowEngineLib.POINode)
            .GetMethod("getBaseEventData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(node, [new CVStartCFC("SYNTHETIC-POI-CCT-WAVE")])!;
        Assert.True(request.IsCCTWave);
        using var reader = new BinaryReader(new MemoryStream(node.GetSaveData()));
        reader.ReadBytes(reader.ReadByte()); reader.ReadBytes(reader.ReadByte());
        Assert.Equal(Encoding.UTF8.GetBytes(bool.TrueString), ReadProperties(reader)[nameof(node.IsCCTWave)]);
    });

    [Theory]
    [InlineData(typeof(LocalRealPoiNode), "IN_CIE")]
    [InlineData(typeof(LocalCalibrationRealPoiNode), "IN_IMG")]
    public void LocalRealtimePoiLoadsLegacyOverridesWithoutRestoringOrResavingThem(Type type, string imageInput) => StaTest.Run(() =>
    {
        var node = (STNode)Activator.CreateInstance(type)!;
        node.Create();
        var legacy = new Dictionary<string, byte[]>
        {
            ["Title"] = Encoding.UTF8.GetBytes("自定义测量"),
            ["POITempName"] = Encoding.UTF8.GetBytes("旧备用模板"),
            ["POIType"] = Encoding.UTF8.GetBytes("Circle"),
            ["POIWidth"] = Encoding.UTF8.GetBytes("40"),
            ["POIHeight"] = Encoding.UTF8.GetBytes("20"),
            ["POIFilterTempName"] = Encoding.UTF8.GetBytes("旧过滤模板"),
            ["POIReviseTempName"] = Encoding.UTF8.GetBytes("旧修正模板")
        };
        if (type == typeof(LocalCalibrationRealPoiNode))
        {
            legacy["UseROI"] = Encoding.UTF8.GetBytes(bool.TrueString);
            Assert.Null(type.GetProperty("UseROI"));
        }
        node.OnLoadNode(legacy);
        Assert.Equal("自定义测量", node.Title);
        Assert.Equal(new[] { imageInput, "IN_POI" }, node.GetAllInputOptions().Select(input => input.Text));
        using var reader = new BinaryReader(new MemoryStream(node.GetSaveData()));
        Assert.EndsWith("|" + type.FullName, Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte())));
        reader.ReadBytes(reader.ReadByte());
        var saved = ReadProperties(reader);
        Assert.Equal(legacy["Title"], saved["Title"]);
        foreach (string key in legacy.Keys.Where(key => key != "Title"))
            Assert.DoesNotContain(key, saved.Keys);
        InvalidOperationException missingInput = Assert.Throws<InvalidOperationException>(() => LocalRealPoiInputResolver.Resolve(-1, -1, imageInput));
        Assert.Contains("连接上游关注点布点节点", missingInput.Message);
        InvalidOperationException swappedInput = Assert.Throws<InvalidOperationException>(() => LocalRealPoiInputResolver.Resolve(1, (int)CVCommCore.CVResultType.Camera_Img, imageInput));
        Assert.Contains($"图像应连接 {imageInput}", swappedInput.Message);
    });

    [Fact]
    public void MigratedEditorNodesPreservePersistedPropertiesConnectionsAndServiceRequests() => StaTest.Run(() =>
    {
        byte[] before = ReadCanvas();
        JArray contracts = ReadContracts();
        using var container = new CVNodeContainer();
        container.LoadCanvas(before);
        Assert.Equal(25, container.Nodes.Count);
        var nodes = container.Nodes.Cast<STNode>().ToArray();
        for (int i = 0; i < nodes.Length; i++)
        {
            Type type = nodes[i].GetType();
            Assert.Same(typeof(DeviceCamera).Assembly, type.Assembly);
            Assert.Equal((string?)contracts[i]["type"], type.FullName);
            Assert.Null(typeof(CVBaseServerNode).Assembly.GetType(type.FullName!));
            var action = new CVStartCFC("SYNTHETIC-EDITOR-MIGRATION");
            object? request = type.GetMethod("getBaseEventData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(nodes[i], [action]);
            JToken? expected = contracts[i]["request"]?.DeepClone();
            if (request is FlowEngineLib.CameraData or FlowEngineLib.Node.Camera.CommCameraData)
                ((JObject)expected!).Remove("POIParam");
            if (request is FlowEngineLib.Algorithm.CalibrationData)
            {
                ((JObject)expected!).Remove("POIParam");
                ((JObject)expected!).Remove("POI_MasterId");
                ((JObject)expected!).Remove("ExpTemplateParam");
            }
            if (request is FlowEngineLib.Node.POI.POIReviseData)
            {
                ((JObject)expected!)["OutputParam"] = new JObject
                {
                    ["ID"] = -1,
                    ["Name"] = ((FlowEngineLib.Node.POI.POIReviseNode)nodes[i]).Output ?? string.Empty,
                };
            }
            Assert.True(JToken.DeepEquals(expected, request == null ? JValue.CreateNull() : JToken.FromObject(request)), type.FullName);
            if (i + 1 < nodes.Length)
                Assert.Same(nodes[i + 1].GetAllInputOptions()[0], Assert.Single(nodes[i].GetAllOutputOptions()[0].ConnectedOption));
        }

        byte[] saved = container.GetCanvasData();
        AssertPropertiesAndConnections(before, saved);
        using var editor = new STNodeEditor();
        editor.LoadCanvas(saved);
        AssertPropertiesAndConnections(saved, editor.GetCanvasData(), editorLayout: true);
        NeutralCanvas neutral = StnV1NeutralCodec.Decode(saved, new());
        Assert.Equal(25, neutral.Nodes.Count);
        Assert.Equal(24, neutral.Connections.Count);
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
            string? legacyMenuPath = (string?)contracts[i]["menu"];
            STNodeAttribute? nodeAttribute = type.GetCustomAttribute<STNodeAttribute>();
            if (legacyMenuPath == null)
            {
                Assert.Null(nodeAttribute);
            }
            else
            {
                Assert.NotNull(nodeAttribute);
                string expectedPath = type == typeof(FlowEngineLib.Node.Algorithm.AlgorithmKBNode) ? "Algorithm"
                    : type == typeof(FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode) ? "Algorithm/OLED"
                    : type == typeof(FlowEngineLib.Algorithm.CalibrationNode) ? "相机"
                    : RemoveLegacyMenuSortPrefix(legacyMenuPath);
                int expectedOrder = type == typeof(FlowEngineLib.Node.Algorithm.AlgorithmKBNode)
                    || type == typeof(FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode) ? 320
                    : type == typeof(FlowEngineLib.Algorithm.CalibrationNode) ? 200 : GetExpectedCategoryOrder(legacyMenuPath);
                Assert.Equal(expectedPath, nodeAttribute.Path);
                Assert.Equal(expectedOrder, nodeAttribute.CategoryOrder);
            }
            PropertyInfo[] edited = type.GetProperties().Where(p => p.DeclaringType == type && p.GetCustomAttribute<PropertyEditorTypeAttribute>() != null).ToArray();
            Assert.Contains(edited, p => p.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType?.Assembly == typeof(DeviceCamera).Assembly);
            Assert.All(edited, p => Assert.DoesNotContain("FlowEngineLib.PropertyEditor", p.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!.FullName!));
        }
        Assert.Null(typeof(CVBaseServerNode).Assembly.GetType("FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute"));
        Assert.Null(typeof(CVBaseServerNode).Assembly.GetType("FlowEngineLib.PropertyEditor.FlowNodePropertyEditorSelector"));
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.Start.BaseStartNode).Assembly);
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.SMUBaseNode).Assembly);
        Assert.Same(typeof(CVBaseServerNode).Assembly, typeof(FlowEngineLib.Node.Algorithm.AlgDataConvertNode).Assembly);
    });

    private static string RemoveLegacyMenuSortPrefix(string path)
    {
        int separator = path.IndexOf(' ');
        return separator >= 0 ? path[(separator + 1)..] : path;
    }

    private static int GetExpectedCategoryOrder(string legacyPath)
    {
        string prefix = legacyPath.Split(' ', 2)[0];
        return prefix switch
        {
            "00" => 0,
            "01" => 100,
            "02" => 200,
            "03_1" => 310,
            "03_2" => 320,
            "03_3" when legacyPath.EndsWith("Image", StringComparison.Ordinal) => 330,
            "03_3" => 331,
            "03_4" => 340,
            "03_5" => 350,
            "04" => 400,
            "05" => 500,
            "06" => 600,
            "07" => 700,
            "09" => 900,
            "10" => 1000,
            "11" => 1100,
            "12" => 1200,
            _ => int.MaxValue,
        };
    }

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
            string modelKey = Encoding.UTF8.GetString(oldNode.ReadBytes(oldNode.ReadByte()));
            Assert.Equal(modelKey, Encoding.UTF8.GetString(newNode.ReadBytes(newNode.ReadByte())));
            oldNode.ReadBytes(oldNode.ReadByte()); newNode.ReadBytes(newNode.ReadByte());
            var oldProperties = ReadProperties(oldNode);
            var newProperties = ReadProperties(newNode);
            // Framework port metadata is additive; the original business bytes still match below.
            Assert.NotNull(STNodeSavedState.ReadPortLayout(newProperties));
            oldProperties.Remove(STNodeSavedState.PortLayoutKey);
            newProperties.Remove(STNodeSavedState.PortLayoutKey);
            if (modelKey is "FlowEngineLib.dll|FlowEngineLib.CVCameraNode"
                or "FlowEngineLib.dll|FlowEngineLib.LVCameraNode"
                or "FlowEngineLib.dll|FlowEngineLib.Node.Camera.CommCameraNode")
            {
                foreach (string key in RetiredCameraPoiProperties)
                {
                    oldProperties.Remove(key);
                    Assert.DoesNotContain(key, newProperties.Keys);
                }
            }
            if (modelKey == "FlowEngineLib.dll|FlowEngineLib.Algorithm.CalibrationNode")
            {
                foreach (string key in RetiredCalibrationProperties)
                {
                    oldProperties.Remove(key);
                    Assert.DoesNotContain(key, newProperties.Keys);
                }
            }
            // Historical CV camera records predate local capture's persisted SaveFiles switch.
            // Missing values keep the current default and are written on the first save.
            if (modelKey == "FlowEngineLib.dll|" + typeof(FlowEngineLib.CVCameraNode).FullName
                && !oldProperties.ContainsKey(nameof(FlowEngineLib.CVCameraNode.SaveFiles)))
            {
                Assert.True(newProperties.Remove(nameof(FlowEngineLib.CVCameraNode.SaveFiles), out byte[]? saveFiles));
                Assert.Equal(Encoding.UTF8.GetBytes(bool.TrueString), saveFiles);
            }
            Assert.Equal(oldProperties.Keys, newProperties.Keys);
            foreach (var property in oldProperties)
            {
                // Removed calibration controls change the default height of auto-sized runtime nodes.
                if (modelKey == "FlowEngineLib.dll|FlowEngineLib.Algorithm.CalibrationNode"
                    && oldProperties["AutoSize"][0] == 1 && property.Key == "Height")
                    continue;
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
