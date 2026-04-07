namespace ColorVision.UI.Serach
{
    public class SearchConfigSettingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.Search,
                    Order = 20,
                    Type = ConfigSettingType.Property,
                    BindingName = nameof(SearchConfig.SearchEngine),
                    Source = SearchConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 21,
                    Type = ConfigSettingType.Property,
                    BindingName = nameof(SearchConfig.EnableBrowserSearch),
                    Source = SearchConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 22,
                    Type = ConfigSettingType.Property,
                    BindingName = nameof(SearchConfig.EnableEverythingSearch),
                    Source = SearchConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Order = 23,
                    Type = ConfigSettingType.Property,
                    BindingName = nameof(SearchConfig.EverythingPath),
                    Source = SearchConfig.Instance,
                },
            };
        }
    }
}
