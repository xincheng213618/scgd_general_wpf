using ColorVision.Engine.Services.Devices.Sensor.Templates;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.ToolPlugins.ThirdPartyApps;

internal static class SscomSensorPresets
{
    internal static IReadOnlyList<SscomCommandPreset> BuildDefaults() => SensorTemplatePresets.All
        .SelectMany(preset => Build(preset.Source.SuggestedName, preset.Commands.Select(command => command.ValueA)))
        .ToArray();

    internal static IReadOnlyList<SscomCommandPreset> Build(string templateName, IEnumerable<string?> commands)
    {
        string[] values = commands.OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return values.Select((value, index) => BuildCommand(values.Length == 1 ? templateName : $"{templateName} #{index + 1}", value)).ToArray();
    }

    private static SscomCommandPreset BuildCommand(string name, string value)
    {
        byte[] bytes = SensorCommandTextFormatter.GetTemplateRequestBytes(value);
        if (bytes.Length == 0) throw new InvalidDataException(DisplayText.Format($"模板指令 {name} 的发送内容为空。"));
        return new SscomCommandPreset(name, bytes);
    }
}
