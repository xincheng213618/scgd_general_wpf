using ColorVision.UI;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    // Preserve paths saved by earlier versions when the application saves its configuration.
    public class ExternalToolsConfig : IConfig
    {
        public static ExternalToolsConfig Instance => ConfigService.Instance.GetRequiredService<ExternalToolsConfig>();

        public string ImageJPath { get; set; } = string.Empty;
        public string BeyondComparePath { get; set; } = string.Empty;
    }
}
