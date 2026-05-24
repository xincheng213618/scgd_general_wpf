using ColorVision.Common.MVVM;

namespace ColorVision.UI.Plugins
{
    public class PluginLoaderrConfig : ViewModelBase, IConfig
    {
        public static PluginLoaderrConfig Instance =>  ConfigService.Instance.GetRequiredService<PluginLoaderrConfig>();

        // 用插件Id作为Key，保证唯一性
        public Dictionary<string, PluginInfo> Plugins { get; set; } = new Dictionary<string, PluginInfo>();
    }
}