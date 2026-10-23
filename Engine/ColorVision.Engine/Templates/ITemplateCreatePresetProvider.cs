using System.Collections.Generic;

namespace ColorVision.Engine.Templates;

public sealed record TemplateCreatePresetGuide(string Purpose, string ModuleGuide, string DeviceGuide);

public sealed record TemplateCreatePreset(string Id, string Title, string Description, string SourceLabel, string SuggestedName)
{
    public TemplateCreatePresetGuide? Guide { get; init; }
}

public interface ITemplateCreatePresetProvider
{
    IEnumerable<TemplateCreatePreset> GetCreatePresets();
    bool ApplyCreatePreset(string presetId);
}
