using System.Windows.Controls;
namespace ColorVision.UI.Configs
{
    public interface IConfigSetting
    {
        public string Name { get; }
        public string Description { get; }

        public string BindingName { get; }
        public object Source { get; }

        public ConfigSettingType Type { get; }

        public UserControl UserControl { get; }

        public ComboBox ComboBox { get; }

    }
}
