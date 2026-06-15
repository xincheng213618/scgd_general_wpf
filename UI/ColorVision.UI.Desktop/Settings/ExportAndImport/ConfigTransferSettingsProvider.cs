namespace ColorVision.UI.Desktop.Settings.ExportAndImport
{
    public class ConfigTransferSettingsProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new()
                {
                    Name = ColorVision.UI.Desktop.Properties.Resources.ImportExportSettings,
                    Order = 900,
                    Type = ConfigSettingType.TabItem,
                    ViewType = typeof(ConfigTransferSettingsControl)
                }
            };
        }
    }
}
