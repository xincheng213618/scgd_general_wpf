using System.Windows.Controls;
namespace ColorVision.UI.Configs
{
    public class ConfigSettingMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public string BindingName { get; set; }
        public object Source { get; set; }

        public ConfigSettingType Type { get; set; }

        public UserControl UserControl { get; set; }

        public ComboBox ComboBox { get; set; }
    }

    public interface IConfigSettingProvider
    {
        IEnumerable<ConfigSettingMetadata> GetConfigSettings();
    }
}
