using ColorVision.UI;

namespace Spectrum.Configs;

public sealed class CalibrationFilePickerConfig : IConfig
{
    public string LastOpenDirectory { get; set; } = string.Empty;
}
