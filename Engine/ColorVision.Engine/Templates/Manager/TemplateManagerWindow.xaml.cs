using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.Manager
{
    public class MenuTemplateManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;

        public override string Header { get; } = "模板管理窗口";

        public override void Execute()
        {
            new TemplateManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    /// <summary>
    /// TemplateManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateManagerWindow : Window
    {
        public TemplateManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = TemplateControl.GetInstance();
      }
    }
}
