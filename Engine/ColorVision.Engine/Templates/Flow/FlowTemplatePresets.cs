using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.MQTT;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.POI;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow;

public sealed record FlowPresetStage(string Type, string Name, float? ExposureMs = null);
public sealed record FlowTemplatePreset(TemplateCreatePreset Source, string Start, IReadOnlyList<FlowPresetStage> Stages, IReadOnlyList<string> Setup);

/// <summary>Builds actual connected canvases from a small, release-owned node vocabulary.</summary>
public static class FlowTemplatePresets
{
    public static IReadOnlyList<FlowTemplatePreset> All => BuiltInTemplateCatalog.Read("flow").Select(Read).ToArray();

    public static FlowTemplatePreset? Find(string id) => All.FirstOrDefault(preset => preset.Source.Id == id);

    private static FlowTemplatePreset Read(BuiltInTemplatePreset preset)
    {
        var payload = JsonConvert.DeserializeObject<FlowRecipe>(preset.PayloadJson);
        if (payload?.Version != 1 || payload.Kind != "flow-recipe" || payload.Start is not ("manual" or "mqtt") || payload.Stages == null || payload.Setup == null ||
            payload.Stages.Count is < 1 or > 12 || payload.Stages.Any(stage => stage == null || string.IsNullOrWhiteSpace(stage.Name) || stage.Name is "开始" or "结束") ||
            payload.Stages.Select(stage => stage.Name).Distinct(StringComparer.Ordinal).Count() != payload.Stages.Count ||
            payload.Setup.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"基础流程“{preset.Source.Title}”格式不兼容。");
        if (payload.Stages.Any(stage => stage.Type == "analysis") && payload.Start != "mqtt")
            throw new InvalidDataException("服务端 POI 分析流程需要 MQTT 开始节点。");
        return new(preset.Source, payload.Start, payload.Stages.AsReadOnly(), payload.Setup.AsReadOnly());
    }

    internal static FlowParam CreateDraft(FlowTemplatePreset preset)
    {
        var nodes = new List<STNode>();
        try
        {
            nodes.Add(new MQTTStartNode { Server = MQTTControl.Host, Port = MQTTControl.Port, DeviceCode = string.Empty });
            foreach (var stage in preset.Stages) nodes.Add(CreateNode(stage));
            nodes.Add(new CVEndNode { DeviceCode = string.Empty });
            for (int index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                // No live canvas or connected options are used while preparing the example.
                // In particular, connection events cannot subscribe services or send messages.
                node.Create();
                ((CVCommonNode)node).NodeName = index == 0 ? "开始" : index == nodes.Count - 1 ? "结束" : preset.Stages[index - 1].Name;
                int row = nodes.Count <= 4 ? 0 : index / 3;
                int column = nodes.Count <= 4 ? index : row % 2 == 0 ? index % 3 : 2 - index % 3;
                node.Left = 30 + 220 * column;
                node.Top = 30 + 150 * row;
            }
            var connections = Enumerable.Range(1, nodes.Count - 1).Select(index => new ConnectionInfo
            {
                Output = nodes[index - 1].GetAllOutputOptions()[0],
                Input = nodes[index].GetAllInputOptions()[0]
            }).ToArray();
            using var stream = new MemoryStream();
            STNodeCanvasWriter.Write(stream, nodes, connections, 0, 0, 1);
            byte[] bytes = stream.ToArray();
            FlowPackageStnValidator.ValidateAndDecompress(bytes);
            return new FlowParam { Id = -1, Name = preset.Source.Title, DataBase64 = Convert.ToBase64String(bytes), CreationPreset = preset };
        }
        finally
        {
            foreach (var disposable in nodes.OfType<IDisposable>()) disposable.Dispose();
        }
    }

    private static STNode CreateNode(FlowPresetStage stage)
    {
        if (stage.ExposureMs != null && (stage.Type != "camera" || !float.IsFinite(stage.ExposureMs.Value) || stage.ExposureMs <= 0 || stage.ExposureMs > 60000))
            throw new InvalidDataException("基础流程曝光参数无效。");
        // No reflection-based type loading or arbitrary property assignment from the catalog.
        return stage.Type switch
        {
            "camera" => new LocalCameraNode { DeviceCode = string.Empty, CalibTempName = string.Empty, ExpTime = stage.ExposureMs ?? 15.67f, Gain = 0, AvgCount = 1, IsAutoExp = false },
            "image" => new TestMessageBoxNode { ImageFileUrl = string.Empty },
            "poi" => new LocalPoiNode { POITempName = string.Empty },
            "analysis" => new POIAnalysisNode { DeviceCode = string.Empty, TempName = string.Empty, ContinueOnFail = false },
            "mtf" => new LocalMtfNode { ResultName = "MTF", ResultDirectory = string.Empty },
            "luminous" => new LocalFindLuminousAreaNode { SavePOITempName = string.Empty },
            "sensor" => new LocalSensorNode { DeviceCode = string.Empty, TemplateName = string.Empty },
            _ => throw new InvalidDataException($"不支持的基础流程步骤：{stage.Type}")
        };
    }

    private sealed record FlowRecipe(int Version, string Kind, string Start, List<FlowPresetStage> Stages, List<string> Setup);
}
