using ColorVision.Common.MVVM;
using System.IO;

namespace ColorVision.UI.Plugins
{
    public class PluginLoaderrConfig : ViewModelBase, IConfig
    {
        public static PluginLoaderrConfig Instance =>  ConfigService.Instance.GetRequiredService<PluginLoaderrConfig>();

        public string PluginUpdatePath { get => _PluginUpdatePath; set { _PluginUpdatePath = value; OnPropertyChanged(); } }
        private string _PluginUpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Plugins/";


        // 用插件Id作为Key，保证唯一性
        public Dictionary<string, PluginInfo> Plugins { get; set; } = new Dictionary<string, PluginInfo>();
    }
}