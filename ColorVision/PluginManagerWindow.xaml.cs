using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision
{
    public class PluginManagerExport : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => nameof(PluginManagerExport);
        public override int Order => 10000;
        public override string Header => "插件管理";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new PluginManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// PluginManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        public PluginManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PluginLoader.LoadPluginsUS("Plugins");
        }
    }
}
