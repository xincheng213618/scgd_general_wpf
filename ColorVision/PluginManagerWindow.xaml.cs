using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision
{
    public class PluginManagerExport : IMenuItem
    {
        public string? OwnerGuid => "Help";
        public string? GuidId => "Wizard";
        public int Order => 10000;
        public string? Header => "PluginManager";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            new PluginManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public Visibility Visibility => Visibility.Visible;
    }


    /// <summary>
    /// PluginManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        public PluginManagerWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PluginLoader.LoadPluginsUS("Plugins");
        }
    }
}
