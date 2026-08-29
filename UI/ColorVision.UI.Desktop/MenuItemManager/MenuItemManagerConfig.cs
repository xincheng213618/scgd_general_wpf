using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class MenuItemManagerConfig : IConfig
    {
        public static MenuItemManagerConfig Instance => ConfigService.Instance.GetRequiredService<MenuItemManagerConfig>();

        public ObservableCollection<MenuItemOverride> Overrides { get; set; } = new();

        /// <summary>
        /// Legacy full menu snapshot. It is read only for migration and removed on the next save.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ObservableCollection<MenuItemSetting>? Settings { get; set; }

        public string? LastSelectedTargetName { get; set; }

        public string? LastSelectedTreeNodeTargetName { get; set; }

        public string? LastSelectedTreeNode { get; set; }
    }
}
