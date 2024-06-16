using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Themes;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.MySql
{
    public class ExportMySqlTool : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "MySqlTool";
        public override string Header => "MySqlTool";
        public override int Order => 2;

        public override void Execute()
        {
            new MySqlToolWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    /// <summary>
    /// MySqlToolWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MySqlToolWindow : Window
    {
        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public MySqlToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {

        }
    }
}
