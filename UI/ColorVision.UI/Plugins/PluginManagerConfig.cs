using ColorVision.Common.MVVM;
using System.IO;

namespace ColorVision.UI.Plugins
{
    public class PluginManagerConfig : ViewModelBase, IConfig
    {
        public static PluginManagerConfig Instance =>  ConfigService.Instance.GetRequiredService<PluginManagerConfig>();

        public string PluginUpdatePath { get => _PluginUpdatePath; set { _PluginUpdatePath = value; OnPropertyChanged(); } }
        private string _PluginUpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Plugins/";

        public string PluginPath { get => _PluginPath; set { _PluginPath = value; OnPropertyChanged(); } } 
        private string _PluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");


        // 用插件Id作为Key，保证唯一性
        public Dictionary<string, PluginInfo> Plugins { get; set; } = new Dictionary<string, PluginInfo>();
    }
}