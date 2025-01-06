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
        public int ExecuteNonQuery(string sqlBatch)
        {
            // 将整个SQL批次按照分号拆分为单个SQL语句
            var statements = sqlBatch.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            int totalCount = 0;
            foreach (var sql in statements)
            {
                try
                {
                    // 去除SQL语句两端的空白字符
                    string trimmedSql = sql.Trim();
                    if (string.IsNullOrEmpty(trimmedSql))
                        continue;

                    using (MySqlCommand command = new(trimmedSql, MySqlControl.MySqlConnection))
                    {
                        int count = command.ExecuteNonQuery();
                        totalCount += count;
                        SqlResultText.Text += $"SQL执行成功。\n受影响的行数: {count}\n执行的SQL语句: {trimmedSql}\n";
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误信息，但不影响后续语句的执行
                    SqlResultText.Text += $"SQL执行失败。\n错误信息: {ex.Message}\n出错的SQL语句: {sql.Trim()}\n";
                    // 您也可以选择记录到日志或其他处理方式
                }
            }
            SqlResultText.Text += $"总共受影响的行数: {totalCount}\n";
            return totalCount;
        }

        private static readonly char[] separator = new[] { ';' };

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string Sql = MySqlText.Text;
            ExecuteNonQuery(Sql);
        }
    }
}
