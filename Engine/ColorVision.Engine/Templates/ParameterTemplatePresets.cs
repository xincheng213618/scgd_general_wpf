using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates;

/// <summary>Portable parameter payloads use symbols or JSON, never feedback database IDs.</summary>
internal static class ParameterTemplatePresets
{
    internal const string JsonKind = "json";
    internal const string DetailsKind = "mod-details";

    internal static IReadOnlyList<BuiltInTemplatePreset> Read(string code, string kind) => BuiltInTemplateCatalog.Read(code)
        .Where(preset => (string?)JObject.Parse(preset.PayloadJson)["kind"] == kind).ToArray();

    private static JObject ReadPayload(BuiltInTemplatePreset preset, string kind)
    {
        var payload = JObject.Parse(preset.PayloadJson);
        if ((int?)payload["version"] != 1 || (string?)payload["kind"] != kind)
            throw new InvalidDataException($"基础模板“{preset.Source.Title}”的参数格式不兼容。");
        return payload;
    }

    internal static string ReadJson(BuiltInTemplatePreset preset)
    {
        if (ReadPayload(preset, JsonKind)["parameters"] is not JObject parameters)
            throw new InvalidDataException("基础模板缺少 JSON 参数对象。");
        return parameters.ToString(Formatting.None);
    }

    internal static List<ModDetailModel> CreateDetails(BuiltInTemplatePreset preset, IReadOnlyList<SysDictionaryModDetaiModel> definitions)
    {
        if (ReadPayload(preset, DetailsKind)["details"] is not JArray values || values.Count == 0)
            throw new InvalidDataException("基础模板缺少参数明细。");
        var details = definitions.Select(definition => new ModDetailModel { SysPid = definition.Id, Pid = -1, ValueA = definition.DefaultValue }).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken value in values)
        {
            string? symbol = (string?)value["symbol"];
            if (symbol == null || !seen.Add(symbol)) throw new InvalidDataException("基础模板的参数标识缺失或重复。");
            var matches = definitions.Select((definition, index) => (definition, index)).Where(item => item.definition.Symbol == symbol).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"当前模板字典不支持参数“{symbol}”，请检查数据库版本。");
            var target = details[matches[0].index];
            target.ValueA = (string?)value["valueA"];
            target.ValueB = (string?)value["valueB"];
        }
        return details;
    }
}
