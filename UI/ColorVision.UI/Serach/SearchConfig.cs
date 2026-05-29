using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.UI.Serach
{
    public enum SearchEngine
    {
        Google,
        Baidu,
        Bing
    }

    public class SearchConfig : ViewModelBase, IConfig
    {
        public static SearchConfig Instance => ConfigService.Instance.GetRequiredService<SearchConfig>();

        [ConfigSetting(Order = 10)]
        [DisplayName("EnableMenuIndex")]
        public bool EnableMenuIndex { get => _enableMenuIndex; set { _enableMenuIndex = value; OnPropertyChanged(); } }
        private bool _enableMenuIndex = true;

        [ConfigSetting(Order = 11)]
        [DisplayName("EnableTemplateIndex")]
        public bool EnableTemplateIndex { get => _enableTemplateIndex; set { _enableTemplateIndex = value; OnPropertyChanged(); } }
        private bool _enableTemplateIndex = true;

        [ConfigSetting(Order = 12)]
        [DisplayName("EnableThirdPartyAppIndex")]
        public bool EnableThirdPartyAppIndex { get => _enableThirdPartyAppIndex; set { _enableThirdPartyAppIndex = value; OnPropertyChanged(); } }
        private bool _enableThirdPartyAppIndex = true;

        [ConfigSetting(Order = 20)]
        [DisplayName("SearchEngine")]
        public SearchEngine SearchEngine { get => _SearchEngine; set { _SearchEngine = value; OnPropertyChanged(); } }
        private SearchEngine _SearchEngine = SearchEngine.Google;

        [ConfigSetting(Order = 23)]
        [DisplayName("EverythingPath")]
        public string EverythingPath { get => _EverythingPath; set { _EverythingPath = value; OnPropertyChanged(); } }
        private string _EverythingPath = @"C:\Program Files\Everything\Everything.exe";

        [ConfigSetting(Order = 22)]
        [DisplayName("EnableEverythingSearch")]
        public bool EnableEverythingSearch { get => _EnableEverythingSearch; set { _EnableEverythingSearch = value; OnPropertyChanged(); } }
        private bool _EnableEverythingSearch = true;

        [ConfigSetting(Order = 21)]
        [DisplayName("EnableBrowserSearch")]
        public bool EnableBrowserSearch { get => _EnableBrowserSearch; set { _EnableBrowserSearch = value; OnPropertyChanged(); } }
        private bool _EnableBrowserSearch = true;

        public bool IsIndexedTypeEnabled(SearchType searchType)
        {
            return searchType switch
            {
                SearchType.Menu => EnableMenuIndex,
                SearchType.File => EnableTemplateIndex,
                SearchType.ThirdPartyApp => EnableThirdPartyAppIndex,
                _ => true,
            };
        }
    }
}
