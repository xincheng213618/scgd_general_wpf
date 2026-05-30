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

        [DisplayName("EnableMenuIndex")]
        public bool EnableMenuIndex { get => _enableMenuIndex; set { _enableMenuIndex = value; OnPropertyChanged(); } }
        private bool _enableMenuIndex = true;

        [DisplayName("EnableTemplateIndex")]
        public bool EnableTemplateIndex { get => _enableTemplateIndex; set { _enableTemplateIndex = value; OnPropertyChanged(); } }
        private bool _enableTemplateIndex = true;

        [DisplayName("EnableThirdPartyAppIndex")]
        public bool EnableThirdPartyAppIndex { get => _enableThirdPartyAppIndex; set { _enableThirdPartyAppIndex = value; OnPropertyChanged(); } }
        private bool _enableThirdPartyAppIndex = true;

        [DisplayName("SearchEngine")]
        public SearchEngine SearchEngine { get => _SearchEngine; set { _SearchEngine = value; OnPropertyChanged(); } }
        private SearchEngine _SearchEngine = SearchEngine.Google;

        [DisplayName("EverythingPath")]
        public string EverythingPath { get => _EverythingPath; set { _EverythingPath = value; OnPropertyChanged(); } }
        private string _EverythingPath = @"C:\Program Files\Everything\Everything.exe";

        [DisplayName("EnableEverythingSearch")]
        public bool EnableEverythingSearch { get => _EnableEverythingSearch; set { _EnableEverythingSearch = value; OnPropertyChanged(); } }
        private bool _EnableEverythingSearch = true;

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
