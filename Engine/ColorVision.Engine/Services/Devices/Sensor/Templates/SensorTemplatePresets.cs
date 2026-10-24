using ColorVision.Engine.Templates;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates;

public sealed record SensorPresetCommand(string ValueA, string? ValueB);
public sealed record SensorTemplatePreset(TemplateCreatePreset Source, string ModuleGuide, string DeviceGuide, string Purpose, IReadOnlyList<SensorPresetCommand> Commands);

/// <summary>Detached creation examples; selecting one never creates database rows or sends commands.</summary>
public static class SensorTemplatePresets
{
    public static IReadOnlyList<SensorTemplatePreset> All => BuiltInTemplateCatalog.Read("Sensor.Default").Select(ReadPreset).ToArray();

    public static SensorTemplatePreset? Find(string id) => All.FirstOrDefault(preset => preset.Source.Id == id);

    private static SensorTemplatePreset ReadPreset(BuiltInTemplatePreset preset)
    {
        var payload = JsonConvert.DeserializeObject<SensorPresetPayload>(preset.PayloadJson);
        if (payload?.Version != 1 || payload.Commands == null || payload.Commands.Count == 0 ||
            payload.Commands.Any(command => command == null || string.IsNullOrWhiteSpace(command.ValueA) || command.ValueA.Split(',').Length != 5))
            throw new InvalidDataException($"基础模板“{preset.Source.Title}”的指令格式不兼容。");
        return new(preset.Source, preset.ModuleGuide, preset.DeviceGuide, preset.Purpose, payload.Commands.AsReadOnly());
    }

    internal static SensorParam CreateDraft(SensorParam? source = null, SensorTemplatePreset? preset = null)
    {
        var draft = new SensorParam { CreationPreset = preset ?? source?.CreationPreset };
        if (preset != null)
            foreach (var command in preset.Commands)
                draft.ModDetailModels.Add(new ModDetailModel { ValueA = command.ValueA, ValueB = command.ValueB, Pid = -1 });
        else if (source != null)
            foreach (ModDetailModel detail in source.ModDetailModels)
                draft.ModDetailModels.Add(new ModDetailModel { SysPid = detail.SysPid, Pid = -1, ValueA = detail.ValueA, ValueB = detail.ValueB, IsEnable = detail.IsEnable, IsDelete = detail.IsDelete });
        else
            draft.ModDetailModels.Add(new ModDetailModel { Pid = -1, ValueA = SensorTemplateDictionaryService.DefaultCommandValue });
        return draft;
    }

    private sealed record SensorPresetPayload(int Version, List<SensorPresetCommand> Commands);
}
