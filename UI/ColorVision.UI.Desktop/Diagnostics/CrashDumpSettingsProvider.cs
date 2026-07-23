namespace ColorVision.UI.Desktop.Diagnostics
{
    public sealed class CrashDumpSettingsProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new()
                {
                    Name = "崩溃转储",
                    Description = "配置 Windows Error Reporting 转储，并保存或收集当前进程的诊断文件。",
                    Order = 85,
                    Type = ConfigSettingType.TabItem,
                    ViewType = typeof(CrashDumpSettingsControl)
                }
            };
        }
    }
}
