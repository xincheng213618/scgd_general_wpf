using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Themes;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.MySql
{
    public class ExportMySQLTool : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "MySQLTool";
        public override string Header => "MySQLTool";
        public override int Order => 2;

        public override void Execute()
        {
            new MySQLToolWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    /// <summary>
    /// MySQLToolWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MySQLToolWindow : Window
    {
        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public MySQLToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {

        }
    }
}
