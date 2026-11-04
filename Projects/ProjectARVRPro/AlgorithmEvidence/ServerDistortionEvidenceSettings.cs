using ColorVision.Engine.Templates.Jsons.Distortion2;
using System.IO;
using System.Text.Json;

namespace ProjectARVRPro.AlgorithmEvidence;

internal sealed record DistortionEvidenceParameters(string Name, string Json);

internal static class ServerDistortionEvidenceSettings
{
    internal static IReadOnlyList<DistortionEvidenceParameters> ReadParameters()
    {
        var assembly = typeof(TemplateDistortion2).Assembly;
        string resource = assembly.GetManifestResourceNames().First(name => name.Replace('\\', '/') == "Templates/Jsons/Distortion2/distortion.schema.json");
        using Stream schema = assembly.GetManifestResourceStream(resource) ?? throw new FileNotFoundException("服务端畸变参数定义缺失。");
        using JsonDocument document = JsonDocument.Parse(schema);
        List<DistortionEvidenceParameters> choices = [new("畸变参考参数（非历史参数）", document.RootElement.GetProperty("default").GetRawText())];
        choices.AddRange(TemplateDistortion2.Params.Select(template => new DistortionEvidenceParameters("当前模板 · " + template.Key, template.Value.JsonValue)));
        return choices;
    }
}
