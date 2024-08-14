using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using MySql.Data.MySqlClient;
using System;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public class ExportMySqlTool : MenuItemBase
    {
        public override string OwnerGuid => "Log";
        public override string GuidId => "MySqlTool";
        public override string Header => "MySqlTool";
        public override int Order => 2;

        [RequiresPermission(PermissionMode.Administrator)]
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
        public int ExecuteNonQuery(string sql)
        {
            int count = -1;
            try
            {
                MySqlCommand command = new(sql, MySqlControl.MySqlConnection);
                count = command.ExecuteNonQuery();
                SqlResultText.Text += $"SQL执行成功。\n受影响的行数: {count}\n执行的SQL语句: {sql}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return count;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string Sql = MySqlText.Text;
            ExecuteNonQuery(Sql);
        }
    }
}
